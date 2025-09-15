using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers.CheckResults;
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
        CustomFunctions = new(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

    protected internal ParserBase(ILogger logger, ParserServices services)
      : base(logger, services.Options, services.TokenizerValidator)
    {
        _parserValidator = services.ParserValidator;
        CustomFunctions = new(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }
    public virtual Dictionary<string, object?> Constants => [];

    protected Dictionary<string, object?> MergeVariableConstants(Dictionary<string, object?>? variables)
    {
        if (variables is null) return Constants;
        foreach (var entry in Constants)
            if (!variables.ContainsKey(entry.Key)) variables.Add(entry.Key, entry.Value);
        return variables;
    }

    protected Dictionary<string, (string[] Parameters, string Body)> CustomFunctions = [];

    /// <summary>
    /// The dictionary stores the main functions with their names and the exact number of arguments.
    /// </summary>
    protected virtual Dictionary<string, int> MainFunctionsArgumentsCount => [];

    /// <summary>
    /// The dictionary stores the minimum number of arguments for each main function.
    /// </summary>
    protected virtual Dictionary<string, int> MainFunctionsMinVariableArgumentsCount => [];

    #region Custom functions

    public void RegisterFunction(string definition)
    {
        var parts = definition.Split('=', 2);
        if (parts.Length != 2)
            throw new ArgumentException("Invalid function definition format.");

        var header = parts[0].Trim();
        var body = parts[1].Trim();

        var nameAndParams = header.Split('(', 2);
        if (nameAndParams.Length != 2 || !nameAndParams[1].EndsWith(")"))
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
        Node<Token> root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[root]!;
    }



    public virtual object? Evaluate(string expression, Dictionary<string, object?>? variables = null, bool optimizeTree = false)
    {
        if (!optimizeTree)
        {
            var postfixTokens = GetPostfixTokens(expression);
            return Evaluate(postfixTokens, variables);
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
    protected virtual object? Evaluate(TokenTree tree, Dictionary<string, object?>? variables = null, bool mergeConstants = true)
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
                    nodeValueDictionary[node] = token.IsNull ? null : EvaluateLiteral(token.Text);
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
        Dictionary<string, object?>? variables = null,
        bool mergeConstants = true)
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
                    nodeValueDictionary[node] = token.IsNull ? null : EvaluateLiteralType(token.Text);
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
        return EvaluateType(postfixTokens, variables);
    }

    protected virtual Type EvaluateType(
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables = null)
    {
        _logger.LogDebug("Evaluating (type inference)...");

        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, object?> nodeValueDictionary = [];

        return EvaluateType(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary, mergeConstants: true);
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
                    nodeValueDictionary.Add(tokenNode, value = token.IsNull ? null : EvaluateLiteralType(token.Text));
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
        var root = nodeDictionary[stack.Pop()];
        return (Type)nodeValueDictionary[root]!;
    }

    protected virtual object? Evaluate(
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables = null)
    {
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, object?> nodeValueDictionary = [];
        return Evaluate(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary, mergeConstants: true);
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
                    nodeValueDictionary.Add(tokenNode, value = EvaluateLiteral(token.Text));
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
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
                }
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);
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
        else
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
    #endregion

    #region NodeDictionary calculations

    protected object? EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _options.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        return EvaluateOperator(operatorName, LeftOperand, RightOperand);
    }

    protected Type EvaluateOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _options.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        return EvaluateOperatorType(operatorName, LeftOperand, RightOperand);
    }

    protected object? EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string operatorName = _options.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return EvaluateUnaryOperator(operatorName, operand);
    }

    protected Type EvaluateUnaryOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string operatorName = _options.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return EvaluateUnaryOperatorType(operatorName, operand);
    }

    protected object? EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string functionName = _options.CaseSensitive ? functionNode.Text : functionNode.Text.ToLower();
        object?[] args = functionNode.GetFunctionArguments(nodeValueDictionary);

        if (CustomFunctions.TryGetValue(functionName, out var funcDef))
        {
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            var localVars = new Dictionary<string, object?>(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            return Evaluate(funcDef.Body, localVars);
        }

        return EvaluateFunction(functionName, args);
    }

    protected Type EvaluateFunctionType(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string functionName = _options.CaseSensitive ? functionNode.Text : functionNode.Text.ToLower();
        object?[] args = functionNode.GetFunctionArguments(nodeValueDictionary);

        if (CustomFunctions.TryGetValue(functionName, out var funcDef))
        {
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            var localVars = new Dictionary<string, object?>(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            return EvaluateType(funcDef.Body, localVars);
        }

        return EvaluateFunctionType(functionName, args);
    }

    #endregion

    #region Calculation definitions (virtual hooks)

    protected virtual object? EvaluateLiteral(string s) => new();
    protected virtual Type EvaluateLiteralType(string s)
    {
        var value = EvaluateLiteral(s);
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

    #region Utility validation methods

    public FunctionNamesCheckResult CheckFunctionNames(string expression)
    {
        var tokens = GetInfixTokens(expression);
        return _parserValidator.CheckFunctionNames(tokens, (IParserFunctionMetadata)this);
    }

    public AdjacentOperandsCheckResult CheckAdjacentOperands(string expression)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckAdjacentOperands(tokens);
    }

    public FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens)
    {
        HashSet<string> matchedNames = [];
        HashSet<string> unmatchedNames = [];
        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Function))
        {
            if (CustomFunctions.ContainsKey(t.Text) || MainFunctionsArgumentsCount.ContainsKey(t.Text))
            { matchedNames.Add(t.Text); continue; }
            unmatchedNames.Add(t.Text);
        }

        return new FunctionNamesCheckResult
        {
            MatchedNames = [.. matchedNames],
            UnmatchedNames = [.. unmatchedNames]
        };
    }

    public List<string> GetMatchedFunctionNames(string expression)
    {
        var tokens = GetInfixTokens(expression);
        return GetMatchedFunctionNames(tokens);
    }

    public List<string> GetMatchedFunctionNames(List<Token> tokens)
    {
        return [.. tokens
            .Where(t => t.TokenType == TokenType.Function &&
                        (CustomFunctions.ContainsKey(t.Text) || MainFunctionsArgumentsCount.ContainsKey(t.Text)))
            .Select(t => t.Text)
            .Distinct()];
    }

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckEmptyFunctionArguments(tree.NodeDictionary);
    }

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckFunctionArgumentsCount(tree.NodeDictionary, (IParserFunctionMetadata)this);
    }

    public InvalidBinaryOperatorsCheckResult CheckBinaryOperatorOperands(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckBinaryOperatorOperands(tree.NodeDictionary);
    }

    public InvalidUnaryOperatorsCheckResult CheckUnaryOperatorOperands(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckUnaryOperatorOperands(tree.NodeDictionary);
    }

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckOrphanArgumentSeparators(tree.NodeDictionary);
    }

    // Orchestrates two-step validation without doing any tokenization or tree building.
    // - Always pre-validates parentheses via tokenizer (string-only).
    // - If matched and inputs are provided, runs parser-level checks against infix and/or node dictionary.
    public ParserValidationReport Validate(
        string expression,
        VariableNamesOptions variableNamesOptions,
        bool earlyReturnOnErrors = false)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return new ParserValidationReport { Expression = expression };

        var parenthesesResult = _tokenizerValidator.CheckParentheses(expression);

        var report = new ParserValidationReport
        {
            Expression = expression,
            ParenthesesResult = parenthesesResult
        };
        if (!parenthesesResult.IsSuccess && earlyReturnOnErrors)
            return report;

        var infixTokens = GetInfixTokens(expression);

        var variableNamesResult = _tokenizerValidator.CheckVariableNames(infixTokens, variableNamesOptions);
        report.VariableNamesResult = variableNamesResult;
        if (!variableNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched variable names in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        var functionNamesResult = _parserValidator.CheckFunctionNames(infixTokens, (IParserFunctionMetadata)this);
        report.FunctionNamesResult = functionNamesResult;
        if (!functionNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function names in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        var adjacentOperandsResult = _tokenizerValidator.CheckAdjacentOperands(infixTokens);
        report.AdjacentOperandsResult = adjacentOperandsResult;
        if (!adjacentOperandsResult.IsSuccess)
        {
            _logger.LogWarning("Adjacent operands in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        List<Token> postfixTokens = GetPostfixTokens(infixTokens);
        TokenTree tree = GetExpressionTree(postfixTokens);
        Dictionary<Token, Node<Token>> nodeDictionary = tree.NodeDictionary;

        var emptyFunctionArgumentsResult = _parserValidator.CheckEmptyFunctionArguments(nodeDictionary);
        report.EmptyFunctionArgumentsResult = emptyFunctionArgumentsResult;
        if (!emptyFunctionArgumentsResult.IsSuccess)
        {
            _logger.LogWarning("Empty function arguments in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        var functionArgumentsCountResult = _parserValidator.CheckFunctionArgumentsCount(nodeDictionary, this);
        report.FunctionArgumentsCountResult = functionArgumentsCountResult;
        if (!functionArgumentsCountResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function arguments in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        // NEW: unary operator operand check
        var unaryOperatorOperandsResult = _parserValidator.CheckUnaryOperatorOperands(nodeDictionary);
        report.UnaryOperatorOperandsResult = unaryOperatorOperandsResult;
        if (!unaryOperatorOperandsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid unary operator operands in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        var binaryOperatorOperandsResult = _parserValidator.CheckBinaryOperatorOperands(nodeDictionary);
        report.BinaryOperatorOperandsResult = binaryOperatorOperandsResult;
        if (!binaryOperatorOperandsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid operators in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        var orphanArgumentSeparatorsResult = _parserValidator.CheckOrphanArgumentSeparators(nodeDictionary);
        report.OrphanArgumentSeparatorsResult = orphanArgumentSeparatorsResult;
        if (!orphanArgumentSeparatorsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid argument separators in formula: {expr}", expression);
            if (earlyReturnOnErrors) return report;
        }

        return report;
    }

    #endregion


}
