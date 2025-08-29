using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.ExpressionTree;   // ADDED for TreeOptimizerResult & extension

namespace ParserLibrary.Parsers;

public class CoreParser : Tokenizer, IParser
{
    public CoreParser(ILogger<CoreParser> logger, IOptions<TokenizerOptions> options)
        : base(logger, options)
    { }

    protected CoreParser(ILogger logger, IOptions<TokenizerOptions> options)
        : base(logger, options)
    { }

    /// <summary>
    /// The dictionary stores the main functions with their names and the exact number of arguments.
    /// </summary>
    protected virtual Dictionary<string, int> MainFunctionsArgumentsCount => [];

    /// <summary>
    /// The dictionary stores the minimum number of arguments for each main function.
    /// </summary>
    protected virtual Dictionary<string, int> MainFunctionsMinVariableArgumentsCount => [];

    #region Custom functions

    protected Dictionary<string, (string[] Parameters, string Body)> CustomFunctions = [];

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
        return CheckFunctionNames(tokens);
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

    public Tree<Token> GetExpressionTree(string s)
    {
        var postfixTokens = GetPostfixTokens(s);
        return GetExpressionTree(postfixTokens);
    }

    public Tree<Token> GetExpressionTree(List<Token> postfixTokens)
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
        return new Tree<Token>
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

    public Tree<Token> GetOptimizedExpressionTree(
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

    public Tree<Token> GetOptimizedExpressionTree(
        List<Token> postfixTokens,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null) =>
        GetOptimizedExpressionTreeResult(
            postfixTokens,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes).Tree;

    // Legacy compatibility (interface expects methods without function maps):
    TreeOptimizerResult IParser.GetOptimizedExpressionTreeResult(string expression, Dictionary<string, Type>? variableTypes) =>
        GetOptimizedExpressionTreeResult(expression, variableTypes);

    TreeOptimizerResult IParser.GetOptimizedExpressionTreeResult(List<Token> postfixTokens, Dictionary<string, Type>? variableTypes) =>
        GetOptimizedExpressionTreeResult(postfixTokens, variableTypes);

    Tree<Token> IParser.GetOptimizedExpressionTree(string expression, Dictionary<string, Type> variableTypes) =>
        GetOptimizedExpressionTree(expression, variableTypes);

    Tree<Token> IParser.GetOptimizedExpressionTree(List<Token> postfixTokens, Dictionary<string, Type> variableTypes) =>
        GetOptimizedExpressionTree(postfixTokens, variableTypes);

    #endregion

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

    protected virtual object? EvaluateWithTreeOptimizer(Tree<Token> tree, Dictionary<string, object?>? variables = null)
    {
        var postfixTokens = tree.GetPostfixTokens();
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
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
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

    #endregion

    #region NodeDictionary calculations

    protected object? EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _options.CaseSensitive ? operatorNode.Text.ToLower() : operatorNode.Text;
        return EvaluateOperator(operatorName, LeftOperand, RightOperand);
    }

    protected Type EvaluateOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _options.CaseSensitive ? operatorNode.Text.ToLower() : operatorNode.Text;
        return EvaluateOperatorType(operatorName, LeftOperand, RightOperand);
    }

    protected object? EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string operatorName = _options.CaseSensitive ? operatorNode.Text.ToLower() : operatorNode.Text;
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return EvaluateUnaryOperator(operatorName, operand);
    }

    protected Type EvaluateUnaryOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string operatorName = _options.CaseSensitive ? operatorNode.Text.ToLower() : operatorNode.Text;
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return EvaluateUnaryOperatorType(operatorName, operand);
    }

    protected object? EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string functionName = _options.CaseSensitive ? functionNode.Text.ToLower() : functionNode.Text;
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
        string functionName = _options.CaseSensitive ? functionNode.Text.ToLower() : functionNode.Text;
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
        return CheckEmptyFunctionArguments(tree.NodeDictionary);
    }

    protected EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(Dictionary<Token, Node<Token>> nodeDictionary)
    {
        List<FunctionArgumentCheckResult> ValidFunctions = [];
        List<FunctionArgumentCheckResult> InvalidFunctions = [];
        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            var node = entry.Value;
            if (token.TokenType != TokenType.Function) continue;

            var arguments = GetFunctionArgumentNodes(node);
            var newCheckResult = new FunctionArgumentCheckResult
            {
                FunctionName = token.Text,
                Position = token.Index + 1
            };

            if (arguments.Any(n => n.Value!.IsNull))
                InvalidFunctions.Add(newCheckResult);
            else
                ValidFunctions.Add(newCheckResult);
        }

        return new EmptyFunctionArgumentsCheckResult
        {
            ValidFunctions = [.. ValidFunctions],
            InvalidFunctions = [.. InvalidFunctions]
        };
    }

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(string expression)
    {
        var tree = GetExpressionTree(expression);
        return CheckFunctionArgumentsCount(tree.NodeDictionary);
    }

    protected FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(Dictionary<Token, Node<Token>> nodeDictionary)
    {
        HashSet<FunctionArgumentCheckResult> validFunctions = [];
        HashSet<FunctionArgumentCheckResult> invalidFunctions = [];

        foreach (var entry in nodeDictionary)
        {
            var node = entry.Value;
            if (node.Value!.TokenType != TokenType.Function) continue;

            string functionName = node.Value.Text;
            int actualArgumentsCount = node.GetFunctionArgumentsCount(_options.TokenPatterns.ArgumentSeparator.ToString());

            if (CustomFunctions.TryGetValue(functionName, out var funcDef))
            {
                int expectedArgumentsCount = funcDef.Parameters.Length;
                var checkResult = new FunctionArgumentCheckResult
                {
                    ActualArgumentsCount = actualArgumentsCount,
                    ExpectedArgumentsCount = expectedArgumentsCount,
                    FunctionName = functionName,
                    Position = node.Value.Index + 1
                };
                if (actualArgumentsCount != expectedArgumentsCount)
                    invalidFunctions.Add(checkResult);
                else validFunctions.Add(checkResult);
                continue;
            }

            if (MainFunctionsArgumentsCount.TryGetValue(functionName, out int expectedCount))
            {
                var checkResult = new FunctionArgumentCheckResult
                {
                    ActualArgumentsCount = actualArgumentsCount,
                    ExpectedArgumentsCount = expectedCount,
                    FunctionName = functionName,
                    Position = node.Value.Index + 1
                };
                if (actualArgumentsCount != expectedCount)
                    invalidFunctions.Add(checkResult);
                else validFunctions.Add(checkResult);
                continue;
            }

            if (MainFunctionsMinVariableArgumentsCount.TryGetValue(functionName, out int minExpectedCount))
            {
                var checkResult = new FunctionArgumentCheckResult
                {
                    ActualArgumentsCount = actualArgumentsCount,
                    MinExpectedArgumentsCount = minExpectedCount,
                    FunctionName = functionName,
                    Position = node.Value.Index + 1
                };
                if (actualArgumentsCount < minExpectedCount)
                    invalidFunctions.Add(checkResult);
                else validFunctions.Add(checkResult);
                continue;
            }

            invalidFunctions.Add(new FunctionArgumentCheckResult
            {
                ActualArgumentsCount = actualArgumentsCount,
                ExpectedArgumentsCount = 0,
                FunctionName = functionName
            });
        }

        return new FunctionArgumentsCountCheckResult
        {
            ValidFunctions = [.. validFunctions],
            InvalidFunctions = [.. invalidFunctions]
        };
    }

    public InvalidOperatorsCheckResult CheckOperators(string expression)
    {
        var tree = GetExpressionTree(expression);
        return CheckOperators(tree.NodeDictionary);
    }

    protected static InvalidOperatorsCheckResult CheckOperators(Dictionary<Token, Node<Token>> nodeDictionary)
    {
        List<OperatorArgumentCheckResult> ValidOperators = [];
        List<OperatorArgumentCheckResult> InvalidOperators = [];
        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            var node = entry.Value;
            if (token.TokenType != TokenType.Operator) continue;
            if (token.TokenType == TokenType.ArgumentSeparator) continue;

            var (LeftOperand, RightOperand) = node.GetBinaryArgumentNodes();
            var newCheckResult = new OperatorArgumentCheckResult
            {
                Operator = token.Text,
                Position = token.Index + 1
            };

            if (LeftOperand.Value!.IsNull || RightOperand.Value!.IsNull)
                InvalidOperators.Add(newCheckResult);
            else
                ValidOperators.Add(newCheckResult);
        }

        return new InvalidOperatorsCheckResult
        {
            ValidOperators = [.. ValidOperators],
            InvalidOperators = [.. InvalidOperators]
        };
    }

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(string expression)
    {
        var tree = GetExpressionTree(expression);
        return CheckOrphanArgumentSeparators(tree.NodeDictionary);
    }

    protected static InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(Dictionary<Token, Node<Token>> nodeDictionary)
    {
        List<int> validPositions = [];
        List<int> invalidPositions = [];

        // Traverse the tree and check for argument separators that have a parent other than argument separator or a function
        foreach (var entry in nodeDictionary)
        {
            var token = entry.Key;
            var node = entry.Value;

            // Only check tokens that are argument separators
            if (token.TokenType != TokenType.Operator ||
                //token.Text[0] != _options.TokenPatterns.ArgumentSeparator
                token.TokenType != TokenType.ArgumentSeparator
                )
                continue;

            // Find the parent node of this argument separator
            var parentFound = false;
            foreach (var parentEntry in nodeDictionary)
            {
                var parentNode = parentEntry.Value;

                // Check if parentNode has this argument separator as one of its children
                if ((parentNode.Left == node || parentNode.Right == node) &&
                    (parentEntry.Key.TokenType == TokenType.Function ||
                     (parentEntry.Key.TokenType == TokenType.Operator &&
                      //parentEntry.Key.Text[0] == _options.TokenPatterns.ArgumentSeparator
                      parentEntry.Key.TokenType == TokenType.ArgumentSeparator
                      )))
                {
                    // Valid: Parent is either a function or an argument separator
                    validPositions.Add(token.Index + 1); // 1-based index
                    parentFound = true;
                    break;
                }
            }

            // If no valid parent was found, the argument separator is invalid
            if (!parentFound)
            {
                invalidPositions.Add(token.Index + 1); // 1-based index
            }
        }

        return new InvalidArgumentSeparatorsCheckResult
        {
            ValidPositions = [.. validPositions],
            InvalidPositions = [.. invalidPositions]
        };
    }

    #endregion
}
