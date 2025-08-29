using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

public readonly struct TreeOptimizerResult<T>
{
    public required Tree<T> Tree { get; init; }
    public required int NonAllNumericBefore { get; init; }
    public required int NonAllNumericAfter { get; init; }
    public int Improvement => NonAllNumericBefore - NonAllNumericAfter;
    public override string ToString() =>
        $"TreeOptimizerResult(NonAllNumericBefore={NonAllNumericBefore}, NonAllNumericAfter={NonAllNumericAfter}, Improvement={Improvement})";
}

public class TreeOptimizer<T>
{
    /// <summary>
    /// Optimizes commutative chains (+, *) by grouping numeric subtrees first.
    /// Uses both variableTypes (identifiers) and functionReturnTypes (functions) to decide numeric vs non-numeric.
    /// Reordering is only performed when there is a mix of numeric and non-numeric operands.
    /// </summary>
    /// <param name="originalTree">Original (unmodified) tree</param>
    /// <param name="variableTypes">Map: identifier -> CLR Type</param>
    /// <param name="functionReturnTypes">Map: functionName -> return CLR Type (e.g. "max" => typeof(Item) or typeof(double))</param>
    public TreeOptimizerResult<T> OptimizeForDataTypes(
        Tree<T> originalTree,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null)
    {
        variableTypes ??= [];
        functionReturnTypes ??= [];

        int before = CountNonAllNumericOperations(originalTree.Root, variableTypes, functionReturnTypes);

        var optimizedTree = originalTree.DeepClone();
        OptimizeNode(optimizedTree.Root, variableTypes, functionReturnTypes);

        int after = CountNonAllNumericOperations(optimizedTree.Root, variableTypes, functionReturnTypes);

        return new TreeOptimizerResult<T>
        {
            Tree = optimizedTree,
            NonAllNumericBefore = before,
            NonAllNumericAfter = after
        };
    }

    private void OptimizeNode(Node<T>? node,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        if (node is null) return;

        if (node.Left is not null) OptimizeNode((Node<T>)node.Left, variableTypes, functionReturnTypes);
        if (node.Right is not null) OptimizeNode((Node<T>)node.Right, variableTypes, functionReturnTypes);

        if (IsCommutativeOperation(node))
        {
            RearrangeCommutativeOperation(node, variableTypes, functionReturnTypes);
        }
    }

    private static bool IsCommutativeOperation(Node<T> node)
    {
        if (node.Value is not Token token) return false;
        return token.TokenType == TokenType.Operator &&
               (token.Text == "+" || token.Text == "*");
    }

    private void RearrangeCommutativeOperation(
        Node<T> node,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        var opText = GetOperatorText(node);
        var operands = CollectCommutativeOperands(node, opText);
        if (operands.Count <= 2) return;

        // Determine numeric classification first; only proceed if mixed.
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

        // Only meaningful if both numeric and non-numeric present.
        if (!(anyNumeric && anyNonNumeric)) return;

        var groupedOperands = GroupOperandsByTypePriority(operands, variableTypes, functionReturnTypes);
        if (groupedOperands.Count <= 1) return;

        var optimizedNode = BuildOptimizedTree(groupedOperands, opText);

        node.Left = optimizedNode.Left;
        node.Right = optimizedNode.Right;
    }

    private List<Node<T>> CollectCommutativeOperands(Node<T> node, string operatorText)
    {
        var operands = new List<Node<T>>();
        CollectOperandsRecursive(node, operatorText, operands);
        return operands;
    }

    private void CollectOperandsRecursive(Node<T>? node, string operatorText, List<Node<T>> operands)
    {
        if (node is null) return;

        var currentOp = GetOperatorText(node);
        if (currentOp == operatorText)
        {
            CollectOperandsRecursive(node.Left as Node<T>, operatorText, operands);
            CollectOperandsRecursive(node.Right as Node<T>, operatorText, operands);
        }
        else
        {
            operands.Add(node);
        }
    }

    private List<List<Node<T>>> GroupOperandsByTypePriority(
        List<Node<T>> operands,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        var typeGroups = new Dictionary<int, List<Node<T>>>();

        foreach (var operand in operands)
        {
            int priority = GetTypePriority(operand, variableTypes, functionReturnTypes);
            if (!typeGroups.TryGetValue(priority, out var list))
            {
                list = [];
                typeGroups[priority] = list;
            }
            list.Add(operand);
        }

        return [.. typeGroups
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value)];
    }

    private int GetTypePriority(
        Node<T> operand,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        if (operand.Value is not Token token) return int.MaxValue;

        return token.TokenType switch
        {
            TokenType.Literal => TreeOptimizer<T>.GetLiteralTypePriority(token.Text),
            TokenType.Identifier when variableTypes.TryGetValue(token.Text, out var type)
                => TreeOptimizer<T>.GetTypePriorityValue(type),
            TokenType.Function => GetFunctionPriority(token, functionReturnTypes),
            _ => int.MaxValue
        };
    }

    private int GetFunctionPriority(Token token, Dictionary<string, Type> functionReturnTypes)
    {
        var name = TreeOptimizer<T>.ExtractFunctionName(token.Text);
        if (functionReturnTypes.TryGetValue(name, out var retType))
            return TreeOptimizer<T>.GetTypePriorityValue(retType);
        // Unknown function return type => treat as non-numeric, push to the end.
        return 1000;
    }

    private static string ExtractFunctionName(string text)
    {
        // If tokens include parentheses (e.g., "max(") or full calls, strip at first '(' or whitespace.
        int idx = text.IndexOf('(');
        if (idx > 0) return text[..idx];
        idx = text.IndexOfAny([' ', '\t']);
        if (idx > 0) return text[..idx];
        return text;
    }

    private static int GetLiteralTypePriority(string literalText)
    {
        if (int.TryParse(literalText, out _)) return 2; // Int
        if (double.TryParse(literalText, System.Globalization.CultureInfo.InvariantCulture, out _)) return 1; // Float-like
        return 0; // Other literals (kept leftmost so numeric chains aggregate early)
    }

    private static int GetTypePriorityValue(Type type)
    {
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return 1;
        if (type == typeof(int) || type == typeof(long)) return 2;
        return 50; // Generic non-numeric-ish types (Items etc.) get larger value to be placed after numerics.
    }

    private Node<T> BuildOptimizedTree(List<List<Node<T>>> groupedOperands, string operatorText)
    {
        var allOperands = groupedOperands.SelectMany(g => g).ToList();
        return BuildLeftAssociativeTree(allOperands, operatorText);
    }

    private Node<T> BuildLeftAssociativeTree(List<Node<T>> operands, string operatorText)
    {
        if (operands.Count == 1) return operands[0];

        var result = operands[0];
        for (int i = 1; i < operands.Count; i++)
        {
            var opNode = CreateOperatorNode(operatorText);
            opNode.Left = result;
            opNode.Right = operands[i];
            result = opNode;
        }
        return result;
    }

    private Node<T> CreateOperatorNode(string operatorText)
    {
        var operatorToken = new Token(TokenType.Operator, operatorText, -1);
        return new Node<T>((T)(object)operatorToken);
    }

    private string GetOperatorText(Node<T> node) =>
        node.Value is Token token ? token.Text : "";

    // ----------------------------------------------------------------
    // Numeric subtree detection & counting
    // ----------------------------------------------------------------

    private static readonly HashSet<string> _numericOperators = new(StringComparer.Ordinal)
    {
        "+","-","*","/","^"
    };

    private int CountNonAllNumericOperations(
        Node<T>? root,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        int count = 0;
        IsNumericSubtreeAndAccumulate(root, variableTypes, functionReturnTypes, ref count);
        return count;
    }

    private bool IsNumericSubtree(
        Node<T>? node,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes)
    {
        int dummy = 0;
        return IsNumericSubtreeAndAccumulate(node, variableTypes, functionReturnTypes, ref dummy);
    }

    private bool IsNumericSubtreeAndAccumulate(
        Node<T>? node,
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

    private bool IsNumericFunction(Token token, Dictionary<string, Type> functionReturnTypes)
    {
        string name = TreeOptimizer<T>.ExtractFunctionName(token.Text);
        if (functionReturnTypes.TryGetValue(name, out var retType))
            return IsNumericType(retType);
        return false;
    }

    private bool EvaluateOperatorNumeric(
        Node<T> opNode,
        Token opToken,
        Dictionary<string, Type> variableTypes,
        Dictionary<string, Type> functionReturnTypes,
        ref int count)
    {
        var left = opNode.Left as Node<T>;
        var right = opNode.Right as Node<T>;

        bool leftNumeric = IsNumericSubtreeAndAccumulate(left, variableTypes, functionReturnTypes, ref count);
        bool rightNumeric = IsNumericSubtreeAndAccumulate(right, variableTypes, functionReturnTypes, ref count);

        bool isArithmetic = _numericOperators.Contains(opToken.Text);
        bool thisNumeric = isArithmetic && leftNumeric && rightNumeric;

        if (!(leftNumeric && rightNumeric))
            count++;

        return thisNumeric;
    }

    private bool IsNumericLiteral(string text) =>
        int.TryParse(text, out _) ||
        long.TryParse(text, out _) ||
        double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out _) ||
        decimal.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out _);

    private bool IsNumericType(Type type) =>
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