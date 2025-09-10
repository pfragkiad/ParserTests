using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers;

public partial class CoreParser : Tokenizer, IParser
{
    protected Dictionary<string, (string[] Parameters, string Body)> CustomFunctions = [];

    // NEW: keep a parser-level validator reference for derived usage
    protected readonly IParserValidator _parserValidator;

    public CoreParser(
        ILogger<CoreParser> logger,
        IOptions<TokenizerOptions> options,
        ITokenizerValidator tokenizerValidator,
        IParserValidator parserValidator)
        : base(logger, options, tokenizerValidator)
    {
        _parserValidator = parserValidator ?? throw new ArgumentNullException(nameof(parserValidator));
        CustomFunctions = new(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

    protected CoreParser(
        ILogger logger,
        IOptions<TokenizerOptions> options,
        ITokenizerValidator tokenizerValidator,
        IParserValidator parserValidator)
        : base(logger, options, tokenizerValidator)
    {
        _parserValidator = parserValidator ?? throw new ArgumentNullException(nameof(parserValidator));
        CustomFunctions = new(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

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

    public FunctionNamesCheckResult CheckFunctionNames(string expression)
    {
        var tokens = GetInfixTokens(expression);
        return _parserValidator.CheckFunctionNames(tokens, (IParserFunctionMetadata)this);
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

    #region Expression trees

    public TokenTree GetExpressionTree(string expression)
    {
        var postfixTokens = GetPostfixTokens(expression);
        return GetExpressionTree(postfixTokens);
    }

    public TokenTree GetExpressionTree(List<Token> postfixTokens)
    {
        _logger.LogDebug("Building expresion tree from postfix tokens...");

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



    // ---------------- Tree Optimizer integration (UPDATED) ----------------

    /// <summary>
    /// Core optimized tree result – full signature including function & ambiguous return type maps.
    /// </summary>
    public TreeOptimizerResult GetOptimizedExpressionTreeResult(
        string expression,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        var tree = GetExpressionTree(expression);
        return tree.OptimizeForDataTypes(
            _options.TokenPatterns,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes);
    }

    public TokenTree GetOptimizedExpressionTree(
        string expression,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null) =>
        GetOptimizedExpressionTreeResult(
            expression,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes).Tree;

    public TreeOptimizerResult GetOptimizedExpressionTreeResult(
        List<Token> postfixTokens,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        var tree = GetExpressionTree(postfixTokens);
        return tree.OptimizeForDataTypes(
            _options.TokenPatterns,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes);
    }

    public TokenTree GetOptimizedExpressionTree(
        List<Token> postfixTokens,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null) =>
        GetOptimizedExpressionTreeResult(
            postfixTokens,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes).Tree;

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
                V?[] args = [.. functionNode.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator,
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

    public virtual Dictionary<string, object?> Constants => [];

    protected Dictionary<string, object?> MergeVariableConstants(Dictionary<string, object?>? variables)
    {
        if (variables is null) return Constants;
        foreach (var entry in Constants)
            if (!variables.ContainsKey(entry.Key)) variables.Add(entry.Key, entry.Value);
        return variables;
    }

    public virtual object? Evaluate(string expression, Dictionary<string, object?>? variables = null)
    {
        var postfixTokens = GetPostfixTokens(expression);
        return Evaluate(postfixTokens, variables);
    }

    public virtual object? EvaluateWithTreeOptimizer(string expression, Dictionary<string, object?>? variables = null)
    {
        // Only variable types for optimization (function return maps could be added externally if desired)
        var variableTypes = variables?
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.GetType());

        var optimizedTree = GetOptimizedExpressionTree(
            expression,
            variableTypes,
            functionReturnTypes: null,
            ambiguousFunctionReturnTypes: null);

        return EvaluateWithTreeOptimizer(optimizedTree, variables);
    }

    protected virtual object? EvaluateWithTreeOptimizer(TokenTree optimizedTree, Dictionary<string, object?>? variables = null)
    {
        var postfixTokens = optimizedTree.GetPostfixTokens();
        return Evaluate(postfixTokens, variables);
    }

    public virtual Type EvaluateType(string expression, Dictionary<string, object?>? variables = null)
    {
        var postfixTokens = GetPostfixTokens(expression);
        return EvaluateType(postfixTokens, variables);
    }

    protected virtual Type EvaluateType(List<Token> postfixTokens, Dictionary<string, object?>? variables = null)
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
                    nodeValueDictionary.Add(tokenNode, value = EvaluateLiteralType(token.Text));
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

    protected virtual object? Evaluate(List<Token> postfixTokens, Dictionary<string, object?>? variables = null)
    {
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, object?> nodeValueDictionary = [];
        return Evaluate(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary, mergeConstants: true);
    }

    protected object? Evaluate(
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
        if (stack.Count > 1)
        {
            string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Text));
            _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
            throw new InvalidOperationException(
                $"The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {stackItemsString}");
        }
    }

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
        object?[] args = GetFunctionArguments(functionNode, nodeValueDictionary);

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
        object?[] args = GetFunctionArguments(functionNode, nodeValueDictionary);

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

    #region Get arguments helpers

    private object?[] GetFunctionArguments(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary) =>
        functionNode.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator, nodeValueDictionary);

    private Node<Token>[] GetFunctionArgumentNodes(Node<Token> functionNode) =>
        functionNode.GetFunctionArgumentNodes(_options.TokenPatterns.ArgumentSeparator);

    #endregion

    #region Checks before evaluation (optional)

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckEmptyFunctionArguments(tree.NodeDictionary, _options.TokenPatterns);
    }

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckFunctionArgumentsCount(tree.NodeDictionary, (IParserFunctionMetadata)this, _options.TokenPatterns);
    }

    public InvalidOperatorsCheckResult CheckOperators(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckOperators(tree.NodeDictionary);
    }

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(string expression)
    {
        var tree = GetExpressionTree(expression);
        return _parserValidator.CheckOrphanArgumentSeparators(tree.NodeDictionary);
    }

    #endregion

    /// <summary>
    /// Infers (and caches) the Type of every node in an existing expression tree using the parser's
    /// standard Evaluate* virtual methods. Returned dictionary maps each Node to its resolved Type (null if unknown).
    /// </summary>
    /// <param name="tree">Already built expression tree</param>
    /// <param name="variables">Optional variable instances (or Types) for identifiers</param>
    protected internal Dictionary<Node<Token>, Type?> InferNodeTypes(
        TokenTree tree,
        Dictionary<string, object?>? variables = null)
    {
        // Merge constants (consistent with EvaluateType)
        variables = MergeVariableConstants(variables);

        // We reuse the existing tree nodes; we only need a postfix walk to ensure children first.
        var postfixTokens = tree.GetPostfixTokens();
        // Token -> Node lookup is in tree.NodeDictionary already.

        Dictionary<Node<Token>, Type?> nodeTypeMap = [];

        // Local helper: get node for token
        Node<Token> GetNode(Token t) => tree.NodeDictionary[t];

        // For building "argument value dictionary" similar to EvaluateType (values here are Types)
        Dictionary<Node<Token>, object?> boxedMap = new();

        foreach (var token in postfixTokens)
        {
            var node = GetNode(token);

            switch (token.TokenType)
            {
                case TokenType.Literal:
                    {
                        var t = EvaluateLiteralType(token.Text);
                        nodeTypeMap[node] = t;
                        boxedMap[node] = t;
                        break;
                    }
                case TokenType.Identifier:
                    {
                        Type? t = null;
                        if (variables is not null && variables.TryGetValue(token.Text, out var v))
                        {
                            if (v is Type vt) t = vt;
                            else t = v?.GetType();
                        }
                        nodeTypeMap[node] = t;
                        boxedMap[node] = t;
                        break;
                    }
                case TokenType.Function:
                    {
                        // Arguments have already been processed (postfix)
                        var args = node.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator, boxedMap);
                        var ft = EvaluateFunctionType(token.Text, args);
                        nodeTypeMap[node] = ft;
                        boxedMap[node] = ft;
                        break;
                    }
                case TokenType.Operator:
                    {
                        var (lNode, rNode) = node.GetBinaryArgumentNodes();
                        var lt = nodeTypeMap.GetValueOrDefault(lNode);
                        var rt = nodeTypeMap.GetValueOrDefault(rNode);
                        Type? result = null;
                        try
                        {
                            result = EvaluateOperatorType(token.Text, lt, rt);
                        }
                        catch
                        {
                            // Unknown operator type -> leave null
                        }
                        nodeTypeMap[node] = result;
                        boxedMap[node] = result;
                        break;
                    }
                case TokenType.OperatorUnary:
                    {
                        bool isPrefix = _options.TokenPatterns.UnaryOperatorDictionary[token.Text].Prefix;
                        var child = node.GetUnaryArgumentNode(isPrefix);
                        var ct = nodeTypeMap.GetValueOrDefault(child);
                        Type? result = null;
                        try
                        {
                            result = EvaluateUnaryOperatorType(token.Text, ct);
                        }
                        catch
                        {
                            // Leave null
                        }
                        nodeTypeMap[node] = result;
                        boxedMap[node] = result;
                        break;
                    }
                case TokenType.ArgumentSeparator:
                    {
                        // Treat argument separator nodes as producing no type
                        nodeTypeMap[node] = null;
                        boxedMap[node] = null;
                        break;
                    }
                default:
                    nodeTypeMap[node] = null;
                    boxedMap[node] = null;
                    break;
            }
        }

        return nodeTypeMap;
    }

}
