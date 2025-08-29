using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

public static class TreeOptimizerExtensions
{
    private static readonly HashSet<string> NumericOperators = new(StringComparer.Ordinal) { "+", "-", "*", "/", "^" };

    /// <summary>
    /// Optimizes commutative chains (+, *) by grouping numeric subtrees first, if both numeric and non-numeric operands exist.
    /// Classification uses variableTypes (identifiers) and functionReturnTypes (functions).
    /// Returns a cloned optimized tree plus before/after non all-numeric counts.
    /// </summary>
    public static TreeOptimizerResult OptimizeForDataTypes(
        this Tree<Token> originalTree,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null)
    {
        variableTypes ??= [];
        functionReturnTypes ??= [];

        int before = CountNonAllNumericOperations(originalTree.Root, variableTypes, functionReturnTypes);

        var optimizedTree = originalTree.DeepClone();
        OptimizeNode(optimizedTree.Root, variableTypes, functionReturnTypes);

        int after = CountNonAllNumericOperations(optimizedTree.Root, variableTypes, functionReturnTypes);

        return new TreeOptimizerResult
        {
            Tree = optimizedTree,
            NonAllNumericBefore = before,
            NonAllNumericAfter = after
        };
    }

    // ---- Optimization traversal ----
    private static void OptimizeNode(Node<Token>? node,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        if (node is null) return;

        if (node.Left is Node<Token> l) OptimizeNode(l, variableTypes, functionReturnTypes);
        if (node.Right is Node<Token> r) OptimizeNode(r, variableTypes, functionReturnTypes);

        if (IsCommutativeOperation(node))
            RearrangeCommutativeOperation(node, variableTypes, functionReturnTypes);
    }

    private static bool IsCommutativeOperation(Node<Token> node) =>
        node.Value is Token t &&
        t.TokenType == TokenType.Operator &&
        (t.Text == "+" || t.Text == "*");

    private static void RearrangeCommutativeOperation(
        Node<Token> node,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        var opText = GetOperatorText(node);
        var operands = CollectCommutativeOperands(node, opText);
        if (operands.Count <= 2) return;

        bool anyNumeric = false;
        bool anyNonNumeric = false;

        foreach (var o in operands)
        {
            if (IsNumericSubtree(o, variableTypes, functionReturnTypes))
                anyNumeric = true;
            else
                anyNonNumeric = true;

            if (anyNumeric && anyNonNumeric) break;
        }

        if (!(anyNumeric && anyNonNumeric)) return;

        var grouped = GroupOperandsByTypePriority(operands, variableTypes, functionReturnTypes);
        if (grouped.Count <= 1) return;

        var optimized = BuildOptimizedTree(grouped, opText);
        node.Left = optimized.Left;
        node.Right = optimized.Right;
    }

    // ---- Operand collection ----
    private static List<Node<Token>> CollectCommutativeOperands(Node<Token> node, string operatorText)
    {
        var list = new List<Node<Token>>();
        CollectOperandsRecursive(node, operatorText, list);
        return list;
    }

    private static void CollectOperandsRecursive(Node<Token>? node, string operatorText, List<Node<Token>> operands)
    {
        if (node is null) return;
        var currentOp = GetOperatorText(node);
        if (currentOp == operatorText)
        {
            CollectOperandsRecursive(node.Left as Node<Token>, operatorText, operands);
            CollectOperandsRecursive(node.Right as Node<Token>, operatorText, operands);
        }
        else
        {
            operands.Add(node);
        }
    }

    // ---- Grouping / priority ----
    private static List<List<Node<Token>>> GroupOperandsByTypePriority(
        List<Node<Token>> operands,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        var map = new Dictionary<int, List<Node<Token>>>();
        foreach (var o in operands)
        {
            int p = GetTypePriority(o, variableTypes, functionReturnTypes);
            if (!map.TryGetValue(p, out var bucket))
            {
                bucket = [];
                map[p] = bucket;
            }
            bucket.Add(o);
        }
        return map.OrderBy(k => k.Key).Select(k => k.Value).ToList();
    }

    private static int GetTypePriority(
        Node<Token> operand,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        if (operand.Value is not Token t) return int.MaxValue;
        return t.TokenType switch
        {
            TokenType.Literal => GetLiteralTypePriority(t.Text),
            TokenType.Identifier when variableTypes.TryGetValue(t.Text, out var vt) => GetTypePriorityValue(vt),
            TokenType.Function => GetFunctionPriority(t, functionReturnTypes),
            _ => int.MaxValue
        };
    }

    private static int GetFunctionPriority(Token token, Dictionary<string, Type> functionReturnTypes)
    {
        var name = ExtractFunctionName(token.Text);
        if (functionReturnTypes.TryGetValue(name, out var ret))
            return GetTypePriorityValue(ret);
        return 1000; // unknown -> treat as non-numeric (far right)
    }

    private static string ExtractFunctionName(string text)
    {
        int idx = text.IndexOf('(');
        if (idx > 0) return text[..idx];
        idx = text.IndexOfAny([' ', '\t']);
        if (idx > 0) return text[..idx];
        return text;
    }

    private static int GetLiteralTypePriority(string literal)
    {
        if (int.TryParse(literal, out _)) return 2;
        if (double.TryParse(literal, System.Globalization.CultureInfo.InvariantCulture, out _)) return 1;
        return 0;
    }

    private static int GetTypePriorityValue(Type type)
    {
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return 1;
        if (type == typeof(int) || type == typeof(long)) return 2;
        return 50;
    }

    // ---- Tree rebuild ----
    private static Node<Token> BuildOptimizedTree(List<List<Node<Token>>> grouped, string opText)
    {
        var flat = grouped.SelectMany(g => g).ToList();
        return BuildLeftAssociativeTree(flat, opText);
    }

    private static Node<Token> BuildLeftAssociativeTree(List<Node<Token>> operands, string opText)
    {
        if (operands.Count == 1) return operands[0];
        var result = operands[0];
        for (int i = 1; i < operands.Count; i++)
        {
            var opNode = CreateOperatorNode(opText);
            opNode.Left = result;
            opNode.Right = operands[i];
            result = opNode;
        }   
        return result;
    }

    private static Node<Token> CreateOperatorNode(string opText) =>
        new(new Token(TokenType.Operator, opText, -1));

    private static string GetOperatorText(Node<Token> node) =>
        node.Value?.Text ?? string.Empty;

    // ---- Counting non all-numeric ----
    private static int CountNonAllNumericOperations(
        Node<Token>? root,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        int count = 0;
        IsNumericSubtreeAndAccumulate(root, variableTypes, functionReturnTypes, ref count);
        return count;
    }

    private static bool IsNumericSubtree(
        Node<Token>? node,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        int dummy = 0;
        return IsNumericSubtreeAndAccumulate(node, variableTypes, functionReturnTypes, ref dummy);
    }

    private static bool IsNumericSubtreeAndAccumulate(
        Node<Token>? node,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes,
        ref int count)
    {
        if (node is null) return false;
        if (node.Value is not Token token) return false;

        return token.TokenType switch
        {
            TokenType.Literal => IsNumericLiteral(token.Text),
            TokenType.Identifier => variableTypes.TryGetValue(token.Text, out var t) && IsNumericType(t),
            TokenType.Function => IsNumericFunction(token, functionReturnTypes),
            TokenType.Operator => EvaluateOperatorNumeric(node, token, variableTypes, functionReturnTypes, ref count),
            _ => false
        };
    }

    private static bool IsNumericFunction(Token token, Dictionary<string, Type> functionReturnTypes)
    {
        var name = ExtractFunctionName(token.Text);
        if (functionReturnTypes.TryGetValue(name, out var ret))
            return IsNumericType(ret);
        return false;
    }

    private static bool EvaluateOperatorNumeric(
        Node<Token> opNode,
        Token opToken,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes,
        ref int count)
    {
        var left = opNode.Left as Node<Token>;
        var right = opNode.Right as Node<Token>;

        bool leftNumeric = IsNumericSubtreeAndAccumulate(left, variableTypes, functionReturnTypes, ref count);
        bool rightNumeric = IsNumericSubtreeAndAccumulate(right, variableTypes, functionReturnTypes, ref count);

        bool arithmetic = NumericOperators.Contains(opToken.Text);
        bool thisNumeric = arithmetic && leftNumeric && rightNumeric;

        if (!(leftNumeric && rightNumeric))
            count++;

        return thisNumeric;
    }

    private static bool IsNumericLiteral(string text) =>
        int.TryParse(text, out _) ||
        long.TryParse(text, out _) ||
        double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out _) ||
        decimal.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static bool IsNumericType(Type type) =>
        type == typeof(byte) ||
        type == typeof(sbyte) ||
        type == typeof(short) ||
        type == typeof(ushort) ||
        type == typeof(int) ||
        type == typeof(uint) ||
        type == typeof(long) ||
        type == typeof(ulong) ||
        type == typeof(float) ||
        type == typeof(double) ||
        type == typeof(decimal);
}