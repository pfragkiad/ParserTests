using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Meta;
using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Helpers;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers;

public partial class ParserBase : Tokenizer, IParser
{

    protected readonly IParserValidator _parserValidator;

    public ParserBase(
        ILogger<ParserBase> logger,
        IOptions<TokenizerOptions> options,
        ITokenizerValidator tokenizerValidator,
        IParserValidator parserValidator)
        : base(logger, options, tokenizerValidator)
    {
        _parserValidator = parserValidator;
        CustomFunctions = new(_patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

    protected internal ParserBase(ILogger logger, ParserServices services)
      : base(logger, services.Options, services.TokenizerValidator)
    {
        _parserValidator = services.ParserValidator;
        CustomFunctions = new(_patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

    public FunctionCatalog? FunctionCatalog { get; set; } //optional catalog for function metadata




    public virtual Dictionary<string, object?> Constants => [];

    protected Dictionary<string, object?> MergeVariableConstants(Dictionary<string, object?>? variables)
    {
        if (variables is null) return Constants;
        foreach (var entry in Constants)
            if (!variables.ContainsKey(entry.Key)) variables.Add(entry.Key, entry.Value);
        return variables;
    }

    public List<Token> GetIdentifiers(string expression, string captureGroup, bool excludeConstantNames)
    {
        var tokens = base.GetIdentifiers(expression, captureGroup);
        if (!excludeConstantNames) return tokens;

        return [.. tokens.Where(t => !Constants.ContainsKey(t.Text))];
    }


    public Dictionary<string, (string[] Parameters, string Body)> CustomFunctions = [];

    /// <summary>
    /// The dictionary stores the main functions with their names and the exact number of arguments.
    /// </summary>
    protected virtual Dictionary<string, byte> MainFunctionsWithFixedArgumentsCount => [];

    /// <summary>
    /// The dictionary stores the minimum number of arguments for each main function.
    /// </summary>
    protected virtual Dictionary<string, byte> MainFunctionsMinVariableArgumentsCount => [];

    protected virtual Dictionary<string, (byte, byte)> MainFunctionsWithVariableArgumentsCount => [];

    #region Custom functions

    public void RegisterFunction(string definition)
    {
        var parts = definition.Split('=', 2);
        if (parts.Length != 2)
            throw new ArgumentException("Invalid function definition format.");

        var header = parts[0].Trim();
        var body = parts[1].Trim();

        var nameAndParams = header.Split('(', 2);
        if (nameAndParams.Length != 2 || !nameAndParams[1].EndsWith(')'))
            throw new ArgumentException("Invalid function header format.");

        var name = nameAndParams[0].Trim();

        var paramList = nameAndParams[1][..^1]
            .Split(_options.TokenPatterns.ArgumentSeparator,
                   StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        CustomFunctions[name] = (paramList, body);
    }

    #endregion


    #region Expression trees

    public TokenTree GetExpressionTree(string expression)
    {
        var postfixTokens = base.GetPostfixTokens(expression);
        return GetExpressionTree(postfixTokens);
    }

    public TokenTree GetExpressionTree(List<Token> postfixTokens)
    {
        _logger.LogDebug("Building expresion tree from postfix tokens...");

        if (postfixTokens.Count == 0) return TokenTree.Empty;

        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];

        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                _ = CreateFunctionNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                _logger.LogDebug("Pushing {token} from stack (function node)", token);
                continue;
            }

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                _ = CreateNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                _logger.LogDebug("Push {token} to stack", token);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary || token.TokenType == TokenType.ArgumentSeparator)
            {
                _ = CreateOperatorNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                _logger.LogDebug("Pushing {token} from stack (operator node)", token);
                continue;
            }

            _logger.LogError("Unexpected token type {type} for token {token}", token.TokenType, token);
            throw new InvalidOperationException($"Unexpected token type {token.TokenType} for token {token}");
        }

        ThrowExceptionIfStackIsInvalid(stack);
        //ThrowExceptionForOrphanArgumentSeparators(nodeDictionary); // NEW: orphan separators validation

        var root = nodeDictionary[stack.Pop()];
        return new TokenTree
        {
            Root = root,
            NodeDictionary = nodeDictionary
        };
    }

    #endregion  // close "Expression trees" region


    #region Evaluation methods

    public V? Evaluate<V>(
        string expression,
        Func<string, V>? literalParser = null,
        Dictionary<string, V>? variables = null,
        Dictionary<string, Func<V?, V?, V?>>? binaryOperators = null,
        Dictionary<string, Func<V?, V?>>? unaryOperators = null,
        Dictionary<string, Func<V?, V?>>? funcs1Arg = null,
        Dictionary<string, Func<V?, V?, V?>>? funcs2Arg = null,
        Dictionary<string, Func<V?, V?, V?, V?>>? funcs3Arg = null)
    {
        var postfixTokens = GetPostfixTokens(expression);
        return Evaluate(
            postfixTokens,
            literalParser, variables,
            binaryOperators, unaryOperators,
            funcs1Arg, funcs2Arg, funcs3Arg);
    }

    protected V? Evaluate<V>(
        List<Token> postfixTokens,
        Func<string, V?>? literalParser,
        Dictionary<string, V>? variables = null,
        Dictionary<string, Func<V?, V?, V?>>? binaryOperators = null,
        Dictionary<string, Func<V?, V?>>? unaryOperators = null,
        Dictionary<string, Func<V?, V?>>? funcs1Arg = null,
        Dictionary<string, Func<V?, V?, V?>>? funcs2Arg = null,
        Dictionary<string, Func<V?, V?, V?, V?>>? funcs3Arg = null)
    {
        _logger.LogDebug("Evaluating...");

        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, V?> nodeValueDictionary = [];

        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = CreateFunctionNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                V?[] args = [.. functionNode.GetFunctionArguments(
                    nodeValueDictionary
                        .Select(e => (e.Key, Value: (object?)e.Value))
                        .ToDictionary(e => e.Key, e => e.Value))
                        .Select(v => (V?)v)];

                V? functionResult = args.Length switch
                {
                    1 when funcs1Arg is not null => funcs1Arg[token.Text](args[0]),
                    2 when funcs2Arg is not null => funcs2Arg[token.Text](args[0], args[1]),
                    3 when funcs3Arg is not null => funcs3Arg[token.Text](args[0], args[1], args[2]),
                    _ => default
                };
                nodeValueDictionary.Add(functionNode, functionResult);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                var tokenNode = CreateNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                V? value = default;
                if (token.TokenType == TokenType.Literal && literalParser is not null)
                    nodeValueDictionary.Add(tokenNode, value = literalParser(token.Text));
                else if (token.TokenType == TokenType.Identifier && variables is not null)
                    nodeValueDictionary.Add(tokenNode, value = variables[token.Text]);

                _logger.LogDebug("Push {token} to stack (value: {value})", token, value);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary || token.TokenType == TokenType.ArgumentSeparator)
            {
                Node<Token> operatorNode = CreateOperatorNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                if (token.TokenType != TokenType.ArgumentSeparator)
                {
                    V? result = default;
                    if (token.TokenType == TokenType.Operator && binaryOperators is not null)
                    {
                        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(
                            nodeValueDictionary
                                .Select(e => (e.Key, Value: (object?)e.Value))
                                .ToDictionary(e => e.Key, e => e.Value));
                        result = binaryOperators[token.Text]((V?)LeftOperand, (V?)RightOperand);
                    }
                    else if (unaryOperators is not null)
                    {
                        V? operand = (V?)operatorNode.GetUnaryArgument(
                            _options.TokenPatterns.UnaryOperatorDictionary[token.Text].Prefix,
                            nodeValueDictionary
                                .Select(e => (e.Key, Value: (object?)e.Value))
                                .ToDictionary(e => e.Key, e => e.Value));
                        result = unaryOperators[token.Text](operand);
                    }
                    nodeValueDictionary.Add(operatorNode, result);
                    _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                {
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
                }
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);
        //ThrowExceptionForOrphanArgumentSeparators(nodeDictionary); // NEW

        Node<Token> root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[root]!;
    }



    public virtual object? Evaluate(
        string expression,
        Dictionary<string, object?>? variables = null,
        bool optimizeTree = false)
    {
        if (!optimizeTree)
        {
            var postfixTokens = GetPostfixTokens(expression);
            return Evaluate(postfixTokens, variables, mergeConstants: true);
        }

        var variableTypes = variables?
               .Where(kv => kv.Value is not null)
               .ToDictionary(kv => kv.Key, kv => kv.Value!.GetType());

        //var tree = GetExpressionTree(expression);
        //var optimizedTree =tree.OptimizeForDataTypes(
        //    _options.TokenPatterns,
        //    variableTypes,
        //    functionReturnTypes: null,
        //    ambiguousFunctionReturnTypes: null).Tree;

        var tree = GetExpressionTree(expression);
        var optimizerResult = GetOptimizedTree(tree, variables, false);
        var optimizedTree = optimizerResult.Tree;

        return Evaluate(optimizedTree, variables, mergeConstants: true);
    }


    // -------- Tree-based evaluation (object) --------
    protected virtual object? Evaluate(
        TokenTree tree,
        Dictionary<string, object?>? variables,
        bool mergeConstants)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        var nodeValueDictionary = new Dictionary<Node<Token>, object?>();

        //var postNodes = tree.Root.PostOrderNodes();

        foreach (var nb in tree.Root.PostOrderNodes())
        {
            var node = (Node<Token>)nb;
            var token = node.Value!;

            switch (token.TokenType)
            {
                case TokenType.Literal:
                    nodeValueDictionary[node] = token.IsNull ? null : EvaluateLiteral(token.Text, token.CaptureGroup);
                    break;

                case TokenType.Identifier:
                    nodeValueDictionary[node] =
                        variables is not null && variables.TryGetValue(token.Text, out var idVal)
                            ? idVal
                            : null;
                    break;

                case TokenType.Operator:
                    nodeValueDictionary[node] = EvaluateOperator(node, nodeValueDictionary);
                    break;

                case TokenType.OperatorUnary:
                    nodeValueDictionary[node] = EvaluateUnaryOperator(node, nodeValueDictionary);
                    break;

                case TokenType.Function:
                    nodeValueDictionary[node] = EvaluateFunction(node, nodeValueDictionary);
                    break;

                case TokenType.ArgumentSeparator:
                    // No value produced for separators (used for function arg routing)
                    nodeValueDictionary[node] = null;
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected token type {token.TokenType} for token {token}");
            }
        }

        return nodeValueDictionary[tree.Root];
    }

    // -------- Tree-based evaluation (type inference) --------
    protected virtual Type EvaluateType(
        TokenTree tree,
        Dictionary<string, object?>? variables,
        bool mergeConstants)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        var nodeValueDictionary = new Dictionary<Node<Token>, object?>();

        foreach (var nb in tree.Root.PostOrderNodes())
        {
            var node = (Node<Token>)nb;
            var token = node.Value!;

            switch (token.TokenType)
            {
                case TokenType.Literal:
                    nodeValueDictionary[node] = token.IsNull ? null : EvaluateLiteralType(token.Text, token.CaptureGroup);
                    break;

                case TokenType.Identifier:
                    if (variables is not null && variables.TryGetValue(token.Text, out var v))
                    {
                        nodeValueDictionary[node] = v is Type tType ? tType : v?.GetType();
                    }
                    else
                    {
                        nodeValueDictionary[node] = null;
                    }
                    break;

                case TokenType.Operator:
                    nodeValueDictionary[node] = EvaluateOperatorType(node, nodeValueDictionary);
                    break;

                case TokenType.OperatorUnary:
                    nodeValueDictionary[node] = EvaluateUnaryOperatorType(node, nodeValueDictionary);
                    break;

                case TokenType.Function:
                    nodeValueDictionary[node] = EvaluateFunctionType(node, nodeValueDictionary);
                    break;

                case TokenType.ArgumentSeparator:
                    // No value produced for separators
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected token type {token.TokenType} for token {token}");
            }
        }

        return (Type)nodeValueDictionary[tree.Root]!;
    }

    public virtual Type EvaluateType(
        string expression,
        Dictionary<string, object?>? variables = null)
    {
        var postfixTokens = GetPostfixTokens(expression);
        return EvaluateType(postfixTokens, variables, mergeConstants: true);
    }

    protected virtual Type EvaluateType(
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables,
        bool mergeConstants)
    {
        _logger.LogDebug("Evaluating (type inference)...");

        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, object?> nodeValueDictionary = [];

        return EvaluateType(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary, mergeConstants);
    }

    protected Type EvaluateType(
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables,
        Stack<Token> stack,
        Dictionary<Token, Node<Token>> nodeDictionary,
        Dictionary<Node<Token>, object?> nodeValueDictionary,
        bool mergeConstants)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = CreateFunctionNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                var functionResult = EvaluateFunctionType(functionNode, nodeValueDictionary);
                nodeValueDictionary.Add(functionNode, functionResult);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                var tokenNode = CreateNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                object? value = null;

                if (token.TokenType == TokenType.Literal)
                {
                    // BUGFIX: store type (or null for Token.Null), not the parsed value
                    nodeValueDictionary.Add(tokenNode, value = token.IsNull ? null : EvaluateLiteralType(token.Text, token.CaptureGroup));
                }
                else if (token.TokenType == TokenType.Identifier && variables is not null)
                {
                    if (variables[token.Text] is Type tType)
                        nodeValueDictionary.Add(tokenNode, value = tType);
                    else
                        nodeValueDictionary.Add(tokenNode, value = variables[token.Text]?.GetType());
                }

                _logger.LogDebug("Push {token} to stack (value: {value})", token, value);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary || token.TokenType == TokenType.ArgumentSeparator)
            {
                Node<Token> operatorNode = CreateOperatorNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                if (token.TokenType != TokenType.ArgumentSeparator)
                {
                    var result =
                        token.TokenType == TokenType.Operator
                            ? EvaluateOperatorType(operatorNode, nodeValueDictionary)
                            : EvaluateUnaryOperatorType(operatorNode, nodeValueDictionary);
                    nodeValueDictionary.Add(operatorNode, result);
                    _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                {
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
                }
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);
        //ThrowExceptionForOrphanArgumentSeparators(nodeDictionary); // NEW

        var root = nodeDictionary[stack.Pop()];
        return (Type)nodeValueDictionary[root]!;
    }

    protected virtual object? Evaluate(
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables,
        bool mergeConstants)
    {
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, object?> nodeValueDictionary = [];
        return Evaluate(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary, mergeConstants);
    }

    protected object? Evaluate( //MAIN EVALUATE FUNCTION
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables,
        Stack<Token> stack,
        Dictionary<Token, Node<Token>> nodeDictionary,
        Dictionary<Node<Token>, object?> nodeValueDictionary,
        bool mergeConstants)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        _logger.LogDebug("Evaluating...");
        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = CreateFunctionNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                object? functionResult = EvaluateFunction(functionNode, nodeValueDictionary);
                nodeValueDictionary.Add(functionNode, functionResult);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                var tokenNode = CreateNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                object? value = null;
                if (token.TokenType == TokenType.Literal)
                    nodeValueDictionary.Add(tokenNode, value = tokenNode.Value!.IsNull ? null : EvaluateLiteral(token.Text, token.CaptureGroup));
                else if (token.TokenType == TokenType.Identifier && variables is not null)
                    nodeValueDictionary.Add(tokenNode, value = variables[token.Text]);
                _logger.LogDebug("Push {token} to stack (value: {value})", token, value);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary || token.TokenType == TokenType.ArgumentSeparator)
            {
                Node<Token> operatorNode = CreateOperatorNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                if (token.TokenType != TokenType.ArgumentSeparator)
                {
                    var result =
                        token.TokenType == TokenType.Operator
                            ? EvaluateOperator(operatorNode, nodeValueDictionary)
                            : EvaluateUnaryOperator(operatorNode, nodeValueDictionary);
                    nodeValueDictionary.Add(operatorNode, result);
                    _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                {
                    nodeValueDictionary.Add(operatorNode, null); //argument separator produces no result
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
                }
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);
        //ThrowExceptionForOrphanArgumentSeparators(nodeDictionary); // NEW

        var root = nodeDictionary[stack.Pop()];

        return nodeValueDictionary[root];
    }

    #endregion

    #region Node creation helpers

    private Node<Token> CreateFunctionNodeAndPushToExpressionStack(Stack<Token> stack, Dictionary<Token, Node<Token>> nodeDictionary, Token token)
    {
        Node<Token> functionNode = new(token);
        Token tokenInFunction = stack.Pop();
        functionNode.Right = nodeDictionary[tokenInFunction];
        _logger.LogDebug("Pop {token} from stack (function right child)", tokenInFunction);
        nodeDictionary.Add(token, functionNode);
        stack.Push(token);
        return functionNode;
    }

    private static Node<Token> CreateNodeAndPushToExpressionStack(Stack<Token> stack, Dictionary<Token, Node<Token>> nodeDictionary, Token token)
    {
        Node<Token> tokenNode = new(token);
        nodeDictionary.Add(token, tokenNode);
        stack.Push(token);
        return tokenNode;
    }

    private Node<Token> CreateOperatorNodeAndPushToExpressionStack(Stack<Token> stack, Dictionary<Token, Node<Token>> nodeDictionary, Token token)
    {
        Node<Token> operatorNode = new(token);

        if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.ArgumentSeparator)
        {
            Token rightToken = stack.Pop(), leftToken = stack.Pop();
            operatorNode.Right = nodeDictionary[rightToken];
            operatorNode.Left = nodeDictionary[leftToken];
            _logger.LogDebug("Pop {rightToken} from stack (right child)", rightToken);
            _logger.LogDebug("Pop {leftToken} from stack (left child)", leftToken);
        }
        else //UNARY
        {
            Token childToken = stack.Pop();
            UnaryOperator op = _options.TokenPatterns.UnaryOperatorDictionary[token.Text];
            if (op.Prefix)
            {
                operatorNode.Right = nodeDictionary[childToken];
                _logger.LogDebug("Pop {rightToken} from stack (right child)", childToken);
            }
            else
            {
                operatorNode.Left = nodeDictionary[childToken];
                _logger.LogDebug("Pop {leftToken} from stack (left child)", childToken);
            }
        }

        nodeDictionary.Add(token, operatorNode);
        stack.Push(token);
        return operatorNode;
    }

    private void ThrowExceptionIfStackIsInvalid(Stack<Token> stack)
    {
        if (stack.Count <= 1) return;

        string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Text));
        _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
        throw new InvalidOperationException(
            $"The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {stackItemsString}");
    }

    // NEW: Validate that all ArgumentSeparator nodes have a valid parent (Function or ArgumentSeparator) and are not the root.
    protected static void ThrowExceptionForOrphanArgumentSeparators(Dictionary<Token, Node<Token>> nodeDictionary)
    {
        // Build a quick lookup to find parents by child reference
        foreach (var kv in nodeDictionary)
        {
            var token = kv.Key;
            if (token.TokenType != TokenType.ArgumentSeparator) continue;

            var sepNode = kv.Value;
            Node<Token>? parent = null;

            foreach (var candidate in nodeDictionary.Values)
            {
                if (ReferenceEquals(candidate.Left, sepNode) || ReferenceEquals(candidate.Right, sepNode))
                {
                    parent = candidate;
                    break;
                }
            }

            // If no parent found => it's the root or detached => invalid
            if (parent is null)
                throw new OrphanArgumentSeparatorException(token.Index + 1);

            var parentTok = (Token)parent.Value!;
            if (parentTok.TokenType != TokenType.Function && parentTok.TokenType != TokenType.ArgumentSeparator)
                throw new OrphanArgumentSeparatorException(token.Index + 1);
        }
    }




    #endregion

    #region NodeDictionary calculations

    protected object? EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        return EvaluateOperator(operatorName, LeftOperand, RightOperand);
    }

    protected Type EvaluateOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        return EvaluateOperatorType(operatorName, LeftOperand, RightOperand);
    }

    protected object? EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return EvaluateUnaryOperator(operatorName, operand);
    }

    protected Type EvaluateUnaryOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return EvaluateUnaryOperatorType(operatorName, operand);
    }

    protected object? EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string functionName = _patterns.CaseSensitive ? functionNode.Text : functionNode.Text.ToLower();
        object?[] args = functionNode.GetFunctionArguments(nodeValueDictionary);

        if (CustomFunctions.TryGetValue(functionName, out var funcDef))
        {
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            var localVars = new Dictionary<string, object?>(_patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            return Evaluate(funcDef.Body, localVars);
        }

        return EvaluateFunction(functionName, args);
    }

    protected Type EvaluateFunctionType(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string functionName = _patterns.CaseSensitive ? functionNode.Text : functionNode.Text.ToLower();
        object?[] args = functionNode.GetFunctionArguments(nodeValueDictionary);

        if (CustomFunctions.TryGetValue(functionName, out var funcDef))
        {
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            var localVars = new Dictionary<string, object?>(_patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            return EvaluateType(funcDef.Body, localVars);
        }

        return EvaluateFunctionType(functionName, args);
    }

    #endregion

    #region Calculation definitions (virtual hooks)

    protected virtual object? EvaluateLiteral(string s, string? group) => new();
    protected virtual Type EvaluateLiteralType(string s, string? group)
    {
        var value = EvaluateLiteral(s, group);
        return value is null ? typeof(object) : value.GetType();
    }

    protected virtual object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand) =>
        throw new InvalidOperationException($"Unknown operator ({operatorName})");

    protected virtual Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand) =>
        throw new InvalidOperationException($"Unknown operator ({operatorName})");

    protected virtual object? EvaluateUnaryOperator(string operatorName, object? operand) =>
        throw new InvalidOperationException($"Unknown unary operator ({operatorName})");

    protected virtual Type EvaluateUnaryOperatorType(string operatorName, object? operand) =>
        throw new InvalidOperationException($"Unknown unary operator ({operatorName})");

    protected virtual object? EvaluateFunction(string functionName, object?[] args) =>
        throw new InvalidOperationException($"Unknown function ({functionName})");

    protected virtual Type EvaluateFunctionType(string functionName, object?[] args) =>
        throw new InvalidOperationException($"Unknown function ({functionName})");

    #endregion

    #region Functions



    protected Result<Type[], ValidationResult> ValidateArgumentTypes
    (FunctionInformation funcInfo, object?[] args, bool allowParentTypes = true)
    {
        // Prefer the new syntax-based validation when syntaxes are declared
        if (funcInfo.Syntaxes is { Count: > 0 })
            return funcInfo.ValidateArgumentTypesLegacy(args, allowParentTypes);

        // Fallback to legacy per-position/global constraints when no syntaxes exist
        return GetFunctionArgumentTypesOld(funcInfo, args, allowParentTypes);
    }

    //[Obsolete]
    protected Result<Type[], ValidationResult> GetFunctionArgumentTypesOld //temporary - to be removed
        (FunctionInformation funcInfo, object?[] args, bool allowParentTypes = true)
    {
        // Prefer new syntax-based validation
        if (funcInfo.Syntaxes is { Count: > 0 })
            return funcInfo.ValidateArgumentTypesLegacy(args, allowParentTypes);

        // Fixed arity path
        if (funcInfo.FixedArgumentsCount.HasValue)
        {
            var fixedCount = funcInfo.FixedArgumentsCount.Value;

            if (args.Length != fixedCount)
                return new ValidationResult(failures: [
                    new("arguments", $"{funcInfo.Name} requires exactly {fixedCount} argument{(fixedCount == 1 ? "" : "s")}.")
                ]);

            if (args.Any(a => a is null))
                return new ValidationResult(failures: [
                    new("arguments", $"{funcInfo.Name} does not accept null arguments.")
                ]);

            var resolved = new Type[fixedCount];
            for (int i = 0; i < fixedCount; i++)
            {
                var t = args[i] is Type provided ? provided : args[i]!.GetType();

                // Allowed types come only from FunctionInformation
                HashSet<Type>? allowed = null;
                if (funcInfo.AllowedTypesForLast is { Count: > 0 } && i == fixedCount - 1)
                    allowed = funcInfo.AllowedTypesForLast;
                else if (funcInfo.AllowedTypesPerPosition is not null && i < funcInfo.AllowedTypesPerPosition.Count)
                    allowed = funcInfo.AllowedTypesPerPosition[i];
                else
                    allowed = funcInfo.AllowedTypesForAll;

                if (allowed is not null && allowed.Count > 0 && !allowed.Any(exp => TypeHelpers.TypeMatches(t, exp, allowParentTypes)))
                {
                    string allowedStr = string.Join(", ", allowed.Select(x => x.Name));
                    string posText = i switch { 0 => "first", 1 => "second", 2 => "third", _ => $"{i + 1}th" };
                    return new ValidationResult(failures: [
                        new("arguments", $"{funcInfo.Name} function allowed types for the {posText} argument are [{allowedStr}], got {t.Name}.")
                    ]);
                }

                resolved[i] = t;
            }

            // Centralized string constraints (values + formats + positional/last/all)
            var strCheck = funcInfo.ValidateStringConstraints(args);
            if (!strCheck.IsValid) return strCheck;

            return resolved;
        }

        // Min/Max arity path
        if (funcInfo.MinArgumentsCount.HasValue && funcInfo.MaxArgumentsCount.HasValue)
        {
            var minCount = funcInfo.MinArgumentsCount.Value;
            var maxCount = funcInfo.MaxArgumentsCount.Value;

            ArgumentOutOfRangeException.ThrowIfNegative(minCount);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, minCount);

            if (args.Length < minCount || args.Length > maxCount)
            {
                if (minCount == maxCount)
                    return new ValidationResult(failures: [
                        new("arguments", $"{funcInfo.Name} requires exactly {minCount} argument{(minCount == 1 ? "" : "s")}.")
                    ]);
                return new ValidationResult(failures: [
                    new("arguments", $"{funcInfo.Name} requires between {minCount} and {maxCount} arguments.")
                ]);
            }

            if (args.Any(a => a is null))
                return new ValidationResult(failures: [
                    new("arguments", $"{funcInfo.Name} does not accept null arguments.")
                ]);

            var resolvedTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var t = args[i] is Type provided ? provided : args[i]!.GetType();

                HashSet<Type>? allowed = null;
                if (funcInfo.AllowedTypesForLast is { Count: > 0 } && i == args.Length - 1)
                    allowed = funcInfo.AllowedTypesForLast;
                else if (funcInfo.AllowedTypesPerPosition is not null && i < funcInfo.AllowedTypesPerPosition.Count)
                    allowed = funcInfo.AllowedTypesPerPosition[i];
                else
                    allowed = funcInfo.AllowedTypesForAll;

                if (allowed is not null && allowed.Count > 0 && !allowed.Any(exp => TypeHelpers.TypeMatches(t, exp, allowParentTypes)))
                {
                    string allowedStr = string.Join(", ", allowed.Select(x => x.Name));
                    string posText = ValidationHelpers.ToOrdinal(i + 1);
                    return new ValidationResult(failures: [
                        new("arguments", $"{funcInfo.Name} function allowed types for the {posText} argument are [{allowedStr}], got {t.Name}.")
                    ]);
                }

                resolvedTypes[i] = t;
            }

            var strCheck = funcInfo.ValidateStringConstraints( args);
            if (!strCheck.IsValid) return strCheck;

            return resolvedTypes;
        }

        // Not properly configured
        return new ValidationResult(failures: [
            new("function", $"Function '{funcInfo.Name}' is not properly configured.")
        ]);
    }


    #region Virtual function hooks

    public virtual FunctionInformation? GetFunctionInformation(string functionName)
    {
        return null;
    }

    public virtual ValidationResult ValidateFunction(string functionName, object?[] args)
    {
        return ValidationHelpers.UnknownFunctionResult(functionName);
    }

    public virtual Result<Type[], ValidationResult> GetFunctionArgumentTypes(string functionName, object?[] args)
    {
        return ValidationHelpers.UnknownFunctionResult(functionName);
    }

    public virtual Result<object?, ValidationResult> ValidateAndEvaluateFunction(string functionName, object?[] args)
    {
        return ValidationHelpers.UnknownFunctionResult(functionName);
    }

    public virtual Result<Type, ValidationResult> ResolveFunctionType(string functionName, object?[] args)
    {
        return ValidationHelpers.UnknownFunctionResult(functionName);
    }

    #endregion

    #endregion

    #region Operators

    // Back-compat overload: default to allowing parent types (same policy as functions/unary)
    protected Result<(Type, Type), ValidationResult> GetBinaryOperatorArgumentTypes(
        string operatorName,
        object? leftArg,
        object? rightArg,
        HashSet<(Type, Type)> allowedOperandTypes,
        bool allowParentTypes = true)
    {
        // Nulls not allowed
        if (leftArg is null || rightArg is null)
            return new ValidationResult(failures: [
                new ValidationFailure("operands", $"{operatorName} operator does not accept null operands.")
            ]);

        // Resolve operand types (support passing Type directly)
        Type leftType = leftArg is Type lt ? lt : leftArg.GetType();
        Type rightType = rightArg is Type rt ? rt : rightArg.GetType();

        // If no constraints provided, accept any types
        if (allowedOperandTypes is null || allowedOperandTypes.Count == 0)
            return (leftType, rightType);

        // Validate allowed combinations (exact or superclass/interface when allowed)
        bool anyMatch = allowedOperandTypes.Any(p =>
            TypeHelpers.TypeMatches(leftType, p.Item1, allowParentTypes) &&
            TypeHelpers.TypeMatches(rightType, p.Item2, allowParentTypes));

        if (anyMatch)
            return (leftType, rightType);

        // Build helpful error message
        string allowedStr = string.Join(", ", allowedOperandTypes.Select(p => $"({p.Item1.Name}, {p.Item2.Name})"));
        return new ValidationResult(failures: [
            new ValidationFailure(
                "operands",
                $"{operatorName} operator allowed operand type pairs are [{allowedStr}], got ({leftType.Name}, {rightType.Name}).")
        ]);
    }

    protected Result<Type, ValidationResult> GetUnaryOperatorArgumentType(
        string operatorName,
        object? arg,
        HashSet<Type> allowedOperandType,
        bool allowParentTypes = true)
    {
        // Nulls not allowed
        if (arg is null)
            return new ValidationResult(failures: [
                new ValidationFailure("operands", $"{operatorName} operator does not accept null operands.")
            ]);

        // Resolve operand type (support passing Type directly)
        Type actual = arg is Type t ? t : arg.GetType();

        // If no constraints provided, accept any type
        if (allowedOperandType is null || allowedOperandType.Count == 0)
            return actual;

        // Check exact or superclass/interface (when allowed)
        bool match = allowedOperandType.Any(expected => TypeHelpers.TypeMatches(actual, expected, allowParentTypes));
        if (match)
            return actual;

        string allowedStr = string.Join(", ", allowedOperandType.Select(x => x.Name));
        return new ValidationResult(failures: [
            new ValidationFailure(
            "operands",
            $"{operatorName} operator allowed operand types are [{allowedStr}], got {actual.Name}.")
        ]);
    }

    public virtual ValidationResult ValidateBinaryOperator(string operatorName, object? leftArg, object? rightArg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }

    public virtual ValidationResult ValidateUnaryOperator(string operatorName, object? arg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }

    public virtual Result<(Type, Type), ValidationResult> GetBinaryOperatorOperandTypes(string operatorName, object? leftArg, object? rightArg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }

    public virtual Result<Type, ValidationResult> GetUnaryOperatorOperandType(string operatorName, object? arg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }


    public virtual Result<object?, ValidationResult> ValidateAndEvaluateBinaryOperator(string operatorName, object? leftArg, object? rightArg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }

    public virtual Result<object?, ValidationResult> ValidateAndEvaluateUnaryOperator(string operatorName, object? arg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }

    public virtual Result<Type, ValidationResult> ResolveBinaryOperatorType(string operatorName, object? leftArg, object? rightArg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }
    public virtual Result<Type, ValidationResult> ResolveUnaryOperatorType(string operatorName, object? arg)
    {
        return ValidationHelpers.UnknownOperatorResult(operatorName);
    }

    #endregion



}
