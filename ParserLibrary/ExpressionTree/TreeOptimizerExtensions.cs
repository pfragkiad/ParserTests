using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;



public static class TreeOptimizerExtensions
{
    private static readonly HashSet<string> _numericOperators =
        new(StringComparer.Ordinal) { "+", "-", "*", "/", "^" };

    /// <summary>
    /// Optimizes commutative chains (+, *) by grouping numeric subtrees first when a mix of numeric and non-numeric operands exists.
    /// Requires the TokenPatterns used by the Tokenizer so the same argument separator is honored.
    /// </summary>
    public static TreeOptimizerResult OptimizeForDataTypes(
        this Tree<Token> originalTree,
        TokenPatterns tokenPatterns,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        variableTypes ??= [];
        functionReturnTypes ??= [];
        ambiguousFunctionReturnTypes ??= [];

        var ctx = new TypeResolutionContext(
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes,
            tokenPatterns.ArgumentSeparator);

        int before = CountNonAllNumericOperations(originalTree.Root, ctx);

        var optimizedTree = originalTree.DeepClone();
        OptimizeNode(optimizedTree.Root, ctx);

        int after = CountNonAllNumericOperations(optimizedTree.Root, ctx);

        return new TreeOptimizerResult
        {
            Tree = optimizedTree,
            NonAllNumericBefore = before,
            NonAllNumericAfter = after
        };
    }

    // Removed: legacy overload that internally created a new TokenPatterns instance (per request).

    #region Internal context
    private sealed class TypeResolutionContext
    {
        public readonly Dictionary<string, Type> VariableTypes;
        public readonly Dictionary<string, Type> FunctionReturnTypes;
        public readonly Dictionary<string, Func<Type?[], Type?>> AmbiguousFunctionReturnTypes;
        public readonly Dictionary<Node<Token>, Type?> TypeCache = new();
        public readonly string ArgumentSeparator;

        public TypeResolutionContext(
            Dictionary<string, Type> variableTypes,
            Dictionary<string, Type> functionReturnTypes,
            Dictionary<string, Func<Type?[], Type?>> ambiguous,
            string argumentSeparator)
        {
            VariableTypes = variableTypes;
            FunctionReturnTypes = functionReturnTypes;
            AmbiguousFunctionReturnTypes = ambiguous;
            ArgumentSeparator = argumentSeparator;
        }
    }
    #endregion

    #region Optimization
    private static void OptimizeNode(Node<Token>? node, TypeResolutionContext ctx)
    {
        if (node is null) return;
        if (node.Left is Node<Token> l) OptimizeNode(l, ctx);
        if (node.Right is Node<Token> r) OptimizeNode(r, ctx);
        if (IsCommutativeOperation(node))
            RearrangeCommutativeOperation(node, ctx);
    }

    private static bool IsCommutativeOperation(Node<Token> node) =>
        node.Value is Token t &&
        t.TokenType == TokenType.Operator &&
        (t.Text == "+" || t.Text == "*");

    private static void RearrangeCommutativeOperation(Node<Token> node, TypeResolutionContext ctx)
    {
        var opText = GetOperatorText(node);
        var operands = CollectCommutativeOperands(node, opText);
        if (operands.Count <= 2) return;

        bool anyNumeric = false;
        bool anyNonNumeric = false;
        foreach (var o in operands)
        {
            if (IsNumericSubtree(o, ctx)) anyNumeric = true; else anyNonNumeric = true;
            if (anyNumeric && anyNonNumeric) break;
        }
        if (!(anyNumeric && anyNonNumeric)) return;

        var grouped = GroupOperandsByTypePriority(operands, ctx);
        if (grouped.Count <= 1) return;

        var optimized = BuildOptimizedTree(grouped, opText);
        node.Left = optimized.Left;
        node.Right = optimized.Right;
    }
    #endregion

    #region Operand collection
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
    #endregion

    #region Grouping / priority
    private static List<List<Node<Token>>> GroupOperandsByTypePriority(
        List<Node<Token>> operands,
        TypeResolutionContext ctx)
    {
        var map = new Dictionary<int, List<Node<Token>>>();
        foreach (var o in operands)
        {
            int p = GetTypePriority(o, ctx);
            if (!map.TryGetValue(p, out var bucket))
            {
                bucket = [];
                map[p] = bucket;
            }
            bucket.Add(o);
        }
        return map.OrderBy(k => k.Key).Select(k => k.Value).ToList();
    }

    private static int GetTypePriority(Node<Token> operand, TypeResolutionContext ctx)
    {
        if (operand.Value is not Token t) return int.MaxValue;
        return t.TokenType switch
        {
            TokenType.Literal => GetLiteralTypePriority(t.Text),
            TokenType.Identifier => ctx.VariableTypes.TryGetValue(t.Text, out var vt)
                ? GetTypePriorityValue(vt)
                : 900,
            TokenType.Function => GetFunctionPriority(t, ctx),
            TokenType.Operator => GetOperatorPriority(operand, ctx),
            _ => int.MaxValue
        };
    }

    private static int GetOperatorPriority(Node<Token> node, TypeResolutionContext ctx)
    {
        var t = ResolveNodeType(node, ctx);
        return t is null ? 60 : GetTypePriorityValue(t);
    }

    private static int GetFunctionPriority(Token token, TypeResolutionContext ctx)
    {
        var type = ResolveFunctionReturnType(token, functionNode: null, ctx);
        if (type is null) return 1000;
        return GetTypePriorityValue(type);
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
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
            type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
            type == typeof(byte) || type == typeof(sbyte)) return 2;
        return 50;
    }
    #endregion

    #region Tree rebuild
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
    #endregion

    #region Counting mixed operations
    private static int CountNonAllNumericOperations(Node<Token>? root, TypeResolutionContext ctx)
    {
        int count = 0;
        IsNumericSubtreeAndAccumulate(root, ctx, ref count);
        return count;
    }
    #endregion

    #region Type & numeric resolution
    private static bool IsNumericSubtree(Node<Token>? node, TypeResolutionContext ctx)
    {
        int dummy = 0;
        return IsNumericSubtreeAndAccumulate(node, ctx, ref dummy);
    }

    private static bool IsNumericSubtreeAndAccumulate(Node<Token>? node, TypeResolutionContext ctx, ref int count)
    {
        if (node is null || node.Value is not Token token) return false;

        return token.TokenType switch
        {
            TokenType.Literal or TokenType.Identifier or TokenType.Function =>
                ResolveNodeType(node, ctx) is Type t && IsNumericType(t),
            TokenType.Operator => EvaluateOperatorNumeric(node, token, ctx, ref count),
            _ => false
        };
    }

    private static bool EvaluateOperatorNumeric(
        Node<Token> opNode,
        Token opToken,
        TypeResolutionContext ctx,
        ref int count)
    {
        var left = opNode.Left as Node<Token>;
        var right = opNode.Right as Node<Token>;

        bool leftNumeric = IsNumericSubtreeAndAccumulate(left, ctx, ref count);
        bool rightNumeric = IsNumericSubtreeAndAccumulate(right, ctx, ref count);

        bool arithmetic = _numericOperators.Contains(opToken.Text);
        bool thisNumeric = arithmetic && leftNumeric && rightNumeric;

        if (!(leftNumeric && rightNumeric))
            count++;

        if (thisNumeric)
        {
            ctx.TypeCache[opNode] = PromoteNumericTypes(
                left is null ? null : ctx.TypeCache.GetValueOrDefault(left),
                right is null ? null : ctx.TypeCache.GetValueOrDefault(right));
        }
        else
        {
            ctx.TypeCache[opNode] = null;
        }

        return thisNumeric;
    }

    private static Type? ResolveNodeType(Node<Token>? node, TypeResolutionContext ctx)
    {
        if (node is null) return null;
        if (ctx.TypeCache.TryGetValue(node, out var cached))
            return cached;

        if (node.Value is not Token token)
        {
            ctx.TypeCache[node] = null;
            return null;
        }

        Type? resolved = token.TokenType switch
        {
            TokenType.Literal => ResolveLiteralType(token.Text),
            TokenType.Identifier => ctx.VariableTypes.TryGetValue(token.Text, out var vt) ? vt : null,
            TokenType.Function => ResolveFunctionReturnType(token, node, ctx),
            TokenType.Operator => ResolveOperatorType(node, token, ctx),
            _ => null
        };

        ctx.TypeCache[node] = resolved;
        return resolved;
    }

    private static Type? ResolveLiteralType(string text)
    {
        if (int.TryParse(text, out _)) return typeof(int);
        if (long.TryParse(text, out _)) return typeof(long);
        if (double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out _)) return typeof(double);
        if (decimal.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out _)) return typeof(decimal);
        return null;
    }

    private static Type? ResolveOperatorType(Node<Token> node, Token token, TypeResolutionContext ctx)
    {
        if (!_numericOperators.Contains(token.Text)) return null;
        var lt = ResolveNodeType(node.Left as Node<Token>, ctx);
        var rt = ResolveNodeType(node.Right as Node<Token>, ctx);
        if (lt is null || rt is null) return null;
        if (!(IsNumericType(lt) && IsNumericType(rt))) return null;
        return PromoteNumericTypes(lt, rt);
    }

    private static Type? ResolveFunctionReturnType(Token token, Node<Token>? functionNode, TypeResolutionContext ctx)
    {
        string name = ExtractFunctionName(token.Text);

        if (ctx.FunctionReturnTypes.TryGetValue(name, out var fixedType))
            return fixedType;

        if (functionNode is not null &&
            ctx.AmbiguousFunctionReturnTypes.TryGetValue(name, out var resolver))
        {
            var argNodes = functionNode.GetFunctionArgumentNodes(ctx.ArgumentSeparator);
            var argTypes = new Type?[argNodes.Length];
            for (int i = 0; i < argNodes.Length; i++)
                argTypes[i] = ResolveNodeType(argNodes[i], ctx);
            try { return resolver(argTypes); } catch { return null; }
        }
        return null;
    }
    #endregion

    #region Helpers
    private static string GetOperatorText(Node<Token> node) =>
        node.Value?.Text ?? string.Empty;

    private static string ExtractFunctionName(string text)
    {
        int idx = text.IndexOf('(');
        if (idx > 0) return text[..idx];
        idx = text.IndexOfAny([' ', '\t']);
        if (idx > 0) return text[..idx];
        return text;
    }

    private static Type? PromoteNumericTypes(Type? a, Type? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        var order = new[]
        {
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        };
        int Rank(Type t) => Array.IndexOf(order, t) switch { < 0 => int.MaxValue, var i => i };
        return Rank(a) >= Rank(b) ? a : b;
    }

    private static bool IsNumericType(Type type) =>
        type == typeof(byte) || type == typeof(sbyte) ||
        type == typeof(short) || type == typeof(ushort) ||
        type == typeof(int) || type == typeof(uint) ||
        type == typeof(long) || type == typeof(ulong) ||
        type == typeof(float) || type == typeof(double) ||
        type == typeof(decimal);
    #endregion
}