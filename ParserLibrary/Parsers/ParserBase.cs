using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Definitions;
using ParserLibrary.Definitions.BinaryOperators;
using ParserLibrary.Definitions.Functions;
using ParserLibrary.Definitions.UnaryOperators;
using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Helpers;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.Interfaces;
using Serilog.Core;

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
        CustomFunctions = new(_patterns.Comparer);
    }

    protected internal ParserBase(ILogger logger, ParserServices services)
      : base(logger, services.Options, services.TokenizerValidator)
    {
        _parserValidator = services.ParserValidator;
        CustomFunctions = new(_patterns.Comparer);
    }

    //optional catalog for function metadata
    public CatalogBase<FunctionDefinition>? FunctionCatalog { get; set; }

    public CatalogBase<BinaryOperatorDefinition>? BinaryOperatorCatalog { get; set; }

    public CatalogBase<UnaryOperatorDefinition>? UnaryOperatorCatalog { get; set; }

    //optional for additional information passed to formulas in addition to arguments (e.g. number of decimals)
    public ParserContext? Context { get; set; }


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

    #region Legacy way of defining functions arguments count (for simple cases only)

    protected virtual Dictionary<string, byte> MainFunctionsWithFixedArgumentsCount => [];

    protected virtual Dictionary<string, byte> MainFunctionsMinVariableArgumentsCount => [];

    protected virtual Dictionary<string, (byte, byte)> MainFunctionsWithVariableArgumentsCount => [];

    #endregion

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

        var functionName = nameAndParams[0].Trim();

        var paramList = nameAndParams[1][..^1]
            .Split(_options.TokenPatterns.ArgumentSeparator,
                   StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        RegisterFunction(functionName, paramList, body);
    }

    public void RegisterFunction(string functionName, string[] paramList, string body)
    {
        CustomFunctions[functionName] = (paramList, body);
    }

    public string RegisterTempFunction(string[] paramList, string body)
    {
        string functionName = GetTempVariableName("TF_");
        CustomFunctions[functionName] = (paramList, body);
        return functionName; //return the name in order to remove it later after temporary use
    }

    public bool UnregisterFunction(string functionName) =>
        CustomFunctions.Remove(functionName);

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
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Pushing {token} from stack (function node)", token);
                continue;
            }

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                _ = CreateNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Push {token} to stack", token);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary || token.TokenType == TokenType.ArgumentSeparator)
            {
                _ = CreateOperatorNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Pushing {token} from stack (operator node)", token);
                continue;
            }
            if (_logger.IsEnabled(LogLevel.Error))
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
                if (_logger.IsEnabled(LogLevel.Debug))
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
                if (_logger.IsEnabled(LogLevel.Debug))
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
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
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

        return Evaluate(optimizedTree.Root, variables, mergeConstants: true);
    }

    public virtual async Task<object?> EvaluateAsync(
        string expression,
        Dictionary<string, object?>? variables = null,
        bool optimizeTree = false,
        CancellationToken ct = default)
    {
        if (!optimizeTree)
        {
            var postfixTokens = GetPostfixTokens(expression);
            return await EvaluateAsync(postfixTokens, variables, mergeConstants: true, ct);
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

        return await EvaluateAsync(optimizedTree.Root, variables, mergeConstants: true, ct);
    }



    // -------- Tree-based evaluation (object) --------
    public virtual object? Evaluate(
        //TokenTree tree,
        Node<Token> root,
        Dictionary<string, object?>? variables,
        bool mergeConstants)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        var nodeValueDictionary = new Dictionary<Node<Token>, object?>();

        //var postNodes = tree.Root.PostOrderNodes();

        foreach (var nb in root.PostOrderNodes())
        {
            var node = (Node<Token>)nb;
            var token = node.Value!;

            nodeValueDictionary[node] = token.TokenType switch
            {
                TokenType.Literal => token.IsNull ? null : EvaluateLiteral(token.Text, token.CaptureGroup),
                TokenType.Identifier => variables is not null && variables.TryGetValue(token.Text, out var idVal)
                                            ? idVal
                                            : null,
                TokenType.Operator => EvaluateOperator(node, nodeValueDictionary),
                TokenType.OperatorUnary => EvaluateUnaryOperator(node, nodeValueDictionary),
                TokenType.Function => EvaluateFunction(node, nodeValueDictionary),
                TokenType.ArgumentSeparator => null,// No value produced for separators (used for function arg routing)
                _ => throw new InvalidOperationException($"Unexpected token type {token.TokenType} for token {token}"),
            };
        }

        return nodeValueDictionary[root];
    }

    public virtual async Task<object?> EvaluateAsync(
        //TokenTree tree,
        Node<Token> root,
        Dictionary<string, object?>? variables,
        bool mergeConstants,
        CancellationToken ct = default)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        var nodeValueDictionary = new Dictionary<Node<Token>, object?>();

        //var postNodes = tree.Root.PostOrderNodes();

        foreach (var nb in root.PostOrderNodes())
        {
            var node = (Node<Token>)nb;
            var token = node.Value!;

            ct.ThrowIfCancellationRequested();

            nodeValueDictionary[node] = token.TokenType switch
            {
                TokenType.Literal => token.IsNull ? null : EvaluateLiteral(token.Text, token.CaptureGroup),
                TokenType.Identifier => variables is not null && variables.TryGetValue(token.Text, out var idVal)
                                            ? idVal
                                            : null,
                TokenType.Operator => await EvaluateOperatorAsync(node, nodeValueDictionary, ct),
                TokenType.OperatorUnary => await EvaluateUnaryOperatorAsync(node, nodeValueDictionary, ct),
                TokenType.Function => await EvaluateFunctionAsync(node, nodeValueDictionary, ct),
                TokenType.ArgumentSeparator => null,// No value produced for separators (used for function arg routing)
                _ => throw new InvalidOperationException($"Unexpected token type {token.TokenType} for token {token}"),
            };
        }

        return nodeValueDictionary[root];
    }

    // -------- Tree-based evaluation (type inference) --------
    protected virtual Type EvaluateType(
        TokenTree tree,
        Dictionary<string, object?>? variables,
        bool mergeConstants)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        var nodeValueDictionary = new Dictionary<Node<Token>, TypeInferenceValue>();

        foreach (var nb in tree.Root.PostOrderNodes())
        {
            var node = (Node<Token>)nb;
            var token = node.Value!;

            switch (token.TokenType)
            {
                case TokenType.Literal:
                    nodeValueDictionary[node] = token.IsNull
                        ? TypeInferenceValue.FromRuntimeValue(null)
                        : TypeInferenceValue.FromRuntimeValue(EvaluateLiteral(token.Text, token.CaptureGroup));
                    break;

                case TokenType.Identifier:
                    if (variables is not null && variables.TryGetValue(token.Text, out var v))
                    {
                        nodeValueDictionary[node] = TypeInferenceValue.FromVariable(v);
                    }
                    else
                    {
                        nodeValueDictionary[node] = TypeInferenceValue.Unknown;
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

        return nodeValueDictionary[tree.Root].ResolvedType;
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
        Dictionary<Node<Token>, TypeInferenceValue> nodeValueDictionary = [];

        return EvaluateType(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary, mergeConstants);
    }

    protected Type EvaluateType(
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables,
        Stack<Token> stack,
        Dictionary<Token, Node<Token>> nodeDictionary,
        Dictionary<Node<Token>, TypeInferenceValue> nodeValueDictionary,
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
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                var tokenNode = CreateNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                var value = TypeInferenceValue.Unknown;

                if (token.TokenType == TokenType.Literal)
                {
                    value = token.IsNull
                        ? TypeInferenceValue.FromRuntimeValue(null)
                        : TypeInferenceValue.FromRuntimeValue(EvaluateLiteral(token.Text, token.CaptureGroup));
                }
                else if (token.TokenType == TokenType.Identifier && variables is not null && variables.TryGetValue(token.Text, out var variableValue))
                {
                    value = TypeInferenceValue.FromVariable(variableValue);
                }

                nodeValueDictionary.Add(tokenNode, value);

                if (_logger.IsEnabled(LogLevel.Debug))
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
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
                }
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);
        //ThrowExceptionForOrphanArgumentSeparators(nodeDictionary); // NEW

        var root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[root].ResolvedType;
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

    protected virtual async Task<object?> EvaluateAsync(
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables,
        bool mergeConstants,
        CancellationToken ct = default )
    {
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, object?> nodeValueDictionary = [];
        return await EvaluateAsync(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary, mergeConstants, ct);
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
                if (_logger.IsEnabled(LogLevel.Debug))
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
                if (_logger.IsEnabled(LogLevel.Debug))
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
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                {
                    nodeValueDictionary.Add(operatorNode, null); //argument separator produces no result
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
                }
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);
        //ThrowExceptionForOrphanArgumentSeparators(nodeDictionary); // NEW

        var root = nodeDictionary[stack.Pop()];

        return nodeValueDictionary[root];
    }

    protected async Task<object?> EvaluateAsync( //MAIN EVALUATE FUNCTION
        List<Token> postfixTokens,
        Dictionary<string, object?>? variables,
        Stack<Token> stack,
        Dictionary<Token, Node<Token>> nodeDictionary,
        Dictionary<Node<Token>, object?> nodeValueDictionary,
        bool mergeConstants,
        CancellationToken ct)
    {
        if (mergeConstants)
            variables = MergeVariableConstants(variables);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Evaluating...");
        foreach (var token in postfixTokens)
        {
            ct.ThrowIfCancellationRequested();


            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = CreateFunctionNodeAndPushToExpressionStack(stack, nodeDictionary, token);
                object? functionResult = await EvaluateFunctionAsync(functionNode, nodeValueDictionary, ct);
                nodeValueDictionary.Add(functionNode, functionResult);
                if (_logger.IsEnabled(LogLevel.Debug))
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
                if (_logger.IsEnabled(LogLevel.Debug))
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
                            ? await EvaluateOperatorAsync(operatorNode, nodeValueDictionary, ct)
                            : await EvaluateUnaryOperatorAsync(operatorNode, nodeValueDictionary, ct);
                    nodeValueDictionary.Add(operatorNode, result);
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                {
                    nodeValueDictionary.Add(operatorNode, null); //argument separator produces no result
                    if (_logger.IsEnabled(LogLevel.Debug))
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
        if (_logger.IsEnabled(LogLevel.Debug))
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
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Pop {rightToken} from stack (right child)", rightToken);
                _logger.LogDebug("Pop {leftToken} from stack (left child)", leftToken);
            }
        }
        else //UNARY
        {
            Token childToken = stack.Pop();
            UnaryOperator op = _options.TokenPatterns.UnaryOperatorDictionary[token.Text];
            if (op.Prefix)
            {
                operatorNode.Right = nodeDictionary[childToken];
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Pop {rightToken} from stack (right child)", childToken);
            }
            else
            {
                operatorNode.Left = nodeDictionary[childToken];
                if (_logger.IsEnabled(LogLevel.Debug))
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
        if (_logger.IsEnabled(LogLevel.Error))
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
        var (leftOperand, rightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        return EvaluateOperator(operatorName, leftOperand, rightOperand);
    }

    protected async Task<object?> EvaluateOperatorAsync(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary, CancellationToken ct)
    {
        var (leftOperand, rightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        return await EvaluateOperatorAsync(operatorName, leftOperand, rightOperand, ct);
    }

    protected TypeInferenceValue EvaluateOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, TypeInferenceValue> nodeValueDictionary)
    {
        var (leftNode, rightNode) = operatorNode.GetBinaryArgumentNodes();
        var leftOperand = nodeValueDictionary[leftNode];
        var rightOperand = nodeValueDictionary[rightNode];
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var resolvedType = EvaluateOperatorType(operatorName, leftOperand.ToResolverArgument(), rightOperand.ToResolverArgument());
        return TypeInferenceValue.FromDeclaredType(resolvedType);
    }

    protected object? EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return EvaluateUnaryOperator(operatorName, operand);
    }

    protected async Task<object?> EvaluateUnaryOperatorAsync(Node<Token> operatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary, CancellationToken ct)
    {
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var operand = operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix,
            nodeValueDictionary);
        return await EvaluateUnaryOperatorAsync(operatorName, operand, ct);
    }

    protected TypeInferenceValue EvaluateUnaryOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, TypeInferenceValue> nodeValueDictionary)
    {
        string operatorName = _patterns.CaseSensitive ? operatorNode.Text : operatorNode.Text.ToLower();
        var operandNode = operatorNode.GetUnaryArgumentNode(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorName].Prefix);
        var operand = nodeValueDictionary[operandNode];
        var resolvedType = EvaluateUnaryOperatorType(operatorName, operand.ToResolverArgument());
        return TypeInferenceValue.FromDeclaredType(resolvedType);
    }

    protected object? EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        string functionName = _patterns.CaseSensitive ? functionNode.Text : functionNode.Text.ToLower();
        object?[] args = functionNode.GetFunctionArguments(nodeValueDictionary);

        if (CustomFunctions.TryGetValue(functionName, out var funcDef))
        {
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            var localVars = new Dictionary<string, object?>(_patterns.Comparer);
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            return Evaluate(funcDef.Body, localVars);
        }

        return EvaluateFunction(functionName, args);
    }

    protected async Task<object?> EvaluateFunctionAsync(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary, CancellationToken ct)
    {
        string functionName = _patterns.CaseSensitive ? functionNode.Text : functionNode.Text.ToLower();
        object?[] args = functionNode.GetFunctionArguments(nodeValueDictionary);

        if (CustomFunctions.TryGetValue(functionName, out var funcDef))
        {
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");
            var localVars = new Dictionary<string, object?>(_patterns.Comparer);
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            return await EvaluateAsync(funcDef.Body, localVars, optimizeTree: false, ct); //  <--------
        }

        return await EvaluateFunctionAsync(functionName, args, ct); //FunctionDefinition implementation  <--------
    }


    protected TypeInferenceValue EvaluateFunctionType(Node<Token> functionNode, Dictionary<Node<Token>, TypeInferenceValue> nodeValueDictionary)
    {
        string functionName = _patterns.CaseSensitive ? functionNode.Text : functionNode.Text.ToLower();
        var args = functionNode
            .GetFunctionArgumentNodes()
            .Select(node => nodeValueDictionary[node])
            .ToArray();
        var resolverArgs = ToResolverArguments(args);

        if (CustomFunctions.TryGetValue(functionName, out var funcDef))
        {
            if (resolverArgs.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            var localVars = new Dictionary<string, object?>(_patterns.Comparer);
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = resolverArgs[i];

            var resolvedType = EvaluateType(funcDef.Body, localVars);
            return TypeInferenceValue.FromDeclaredType(resolvedType);
        }

        var functionType = EvaluateFunctionType(functionName, resolverArgs);
        return TypeInferenceValue.FromDeclaredType(functionType);
    }

    #endregion

    #region Calculation definitions (virtual hooks)

    protected virtual object? EvaluateLiteral(string s, string? group) => new();
    protected virtual Type EvaluateLiteralType(string s, string? group)
    {
        var value = EvaluateLiteral(s, group);
        return value is null ? TypeHelpers.NullArgumentType : value.GetType();
    }

    //At least one of EvaluateOperator/EvaluateOperatorAsync must be overridden in derived class to provide operator evaluation logic.
    protected virtual object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand) =>
        ParserLibrarySettings.WithCalcFallback ? EvaluateOperatorAsync(operatorName, leftOperand, rightOperand, CancellationToken.None).GetAwaiter().GetResult() :
        throw new InvalidOperationException($"Unknown operator ({operatorName})");

    protected virtual Task<object?> EvaluateOperatorAsync(string operatorName, object? leftOperand, object? rightOperand, CancellationToken ct) =>
        ParserLibrarySettings.WithCalcFallback ? Task.FromResult(EvaluateOperator(operatorName, leftOperand, rightOperand)) :
        throw new InvalidOperationException($"Unknown operator ({operatorName})");

    protected virtual Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand) =>
        throw new InvalidOperationException($"Unknown operator ({operatorName})");

    //At least one of EvaluateUnaryOperator/EvaluateUnaryOperatorAsync must be overridden in derived class to provide operator evaluation logic.
    protected virtual object? EvaluateUnaryOperator(string operatorName, object? operand) =>
        ParserLibrarySettings.WithCalcFallback ? EvaluateUnaryOperatorAsync(operatorName, operand, CancellationToken.None).GetAwaiter().GetResult() :
        throw new InvalidOperationException($"Unknown unary operator ({operatorName})");

    protected virtual Task<object?> EvaluateUnaryOperatorAsync(string operatorName, object? operand, CancellationToken ct) =>
        ParserLibrarySettings.WithCalcFallback ? Task.FromResult(EvaluateUnaryOperator(operatorName, operand)) :
        throw new InvalidOperationException($"Unknown unary operator ({operatorName})");

    protected virtual Type EvaluateUnaryOperatorType(string operatorName, object? operand) =>
        throw new InvalidOperationException($"Unknown unary operator ({operatorName})");

    //At least one of EvaluateFunction/EvaluateFunctionAsync must be overridden in derived class to provide function evaluation logic.
    protected virtual object? EvaluateFunction(string functionName, object?[] args) =>
        ParserLibrarySettings.WithCalcFallback ? EvaluateFunctionAsync(functionName, args, CancellationToken.None).GetAwaiter().GetResult() :
        throw new InvalidOperationException($"Unknown function ({functionName})");

    protected virtual async Task<object?> EvaluateFunctionAsync(string functionName, object?[] args, CancellationToken ct) =>
        ParserLibrarySettings.WithCalcFallback ? Task.FromResult(EvaluateFunction(functionName, args)) :
        throw new InvalidOperationException($"Unknown function ({functionName})");


    protected virtual Type EvaluateFunctionType(string functionName, object?[] args) =>
        throw new InvalidOperationException($"Unknown function ({functionName})");

    #endregion


    #region Functions mgmt via Catalog

    public virtual bool AllowParentTypesInValidation => true;

    public virtual FunctionDefinition? GetFunctionInformation(string functionName)
    {
        return FunctionCatalog?.Get(functionName);
    }

    public virtual ValidationResult ValidateFunction(string functionName, object?[] args)
    {
        FunctionDefinition? f = GetFunctionInformation(functionName);
        if (f is null) return ValidationHelpers.UnknownFunctionResult(functionName);
        return f.Validate(args, Context, allowParentTypes: AllowParentTypesInValidation);
    }

    public virtual Result<Type, ValidationResult> ResolveFunctionType(string functionName, object?[] args)
    {
        FunctionDefinition? f = GetFunctionInformation(functionName);
        if (f is null) return ValidationHelpers.UnknownFunctionResult(functionName);
        return f.ResolveOutputType(args, Context, allowParentTypes: AllowParentTypesInValidation);
    }

    public virtual async Task<Result<Type, ValidationResult>> ResolveFunctionTypeAsync(string functionName, object?[] args, CancellationToken ct)
    {
        FunctionDefinition? f = GetFunctionInformation(functionName);
        if (f is null) return ValidationHelpers.UnknownFunctionResult(functionName);
        return await f.ResolveOutputTypeAsync(args, Context, allowParentTypes: AllowParentTypesInValidation, ct: ct);
    }

    //public virtual Result<Type[], ValidationResult> GetFunctionArgumentTypes(string functionName, object?[] args)
    //{
    //    FunctionDefinition? f = GetFunctionInformation(functionName);
    //    if (f is null) return ValidationHelpers.UnknownFunctionResult(functionName);
    //    return f.ValidateArgumentTypesLegacy(args, allowParentTypes: true);
    //}

    public virtual Result<FunctionSyntaxMatch, ValidationResult> GetFunctionSyntax(string functionName, object?[] args)
    {
        FunctionDefinition? f = GetFunctionInformation(functionName);
        if (f is null) return ValidationHelpers.UnknownFunctionResult(functionName);
        return f.ValidateArgumentTypes(args, Context, allowParentTypes: AllowParentTypesInValidation);
    }

    public virtual Result<object?, ValidationResult> ValidateAndEvaluateFunction(string functionName, object?[] args)
    {
        FunctionDefinition? f = GetFunctionInformation(functionName);
        if (f is null) return ValidationHelpers.UnknownFunctionResult(functionName);
        return f.ValidateAndCalc(args, Context, allowParentTypes: AllowParentTypesInValidation); //does not cover CalcAsync
    }

    public virtual async Task<Result<object?, ValidationResult>> ValidateAndEvaluateFunctionAsync(string functionName, object?[] args, CancellationToken ct)
    {
        FunctionDefinition? f = GetFunctionInformation(functionName);
        if (f is null) return ValidationHelpers.UnknownFunctionResult(functionName);
        return await f.ValidateAndCalcAsync(args, Context, allowParentTypes: AllowParentTypesInValidation, ct: ct); //also covers Calc
    }

    #endregion


    #region Operators mgmt via Cataloag

    // Binary operator info via catalog
    public virtual BinaryOperatorDefinition? GetBinaryOperatorInformation(string operatorName)
    {
        return BinaryOperatorCatalog?.Get(operatorName);
    }

    // Unary operator info by kind (explicit)
    public virtual UnaryOperatorDefinition? GetUnaryOperatorInformation(string operatorName)
    {
        return UnaryOperatorCatalog?.Get(operatorName);
    }

    // Helper: attempt to resolve unary operator information by using token patterns first,
    // then trying both prefix/postfix lookup in the catalog as fallback.
    private UnaryOperatorDefinition? ResolveUnaryOperatorInfoForName(string operatorName)
    {
        if (!_patterns.UnaryOperatorDictionary.ContainsKey(operatorName)) return null;
        return UnaryOperatorCatalog?.Get(operatorName);
    }

    // Catalog-backed binary operator validation
    public virtual ValidationResult ValidateBinaryOperator(string operatorName, object? leftArg, object? rightArg)
    {
        var info = GetBinaryOperatorInformation(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.Validate(leftArg, rightArg, Context, allowParentTypes: true );
    }

    // Catalog-backed unary operator validation
    public virtual ValidationResult ValidateUnaryOperator(string operatorName, object? arg)
    {
        var info = ResolveUnaryOperatorInfoForName(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.Validate(arg, Context, allowParentTypes: true);
    }

    // Return operand types for binary operator (catalog-backed)
    public virtual Result<(Type, Type), ValidationResult> GetBinaryOperatorOperandTypes(string operatorName, object? leftArg, object? rightArg)
    {
        var info = GetBinaryOperatorInformation(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);

        var r = info.GetValidSyntax(leftArg, rightArg, Context, allowParentTypes: true);
        if (r.IsFailure) return r.Error!;
        return (r.Value!.LeftType, r.Value!.RightType);
    }

    // Return operand type for unary operator (catalog-backed)
    public virtual Result<Type, ValidationResult> GetUnaryOperatorOperandType(string operatorName, object? arg)
    {
        var info = ResolveUnaryOperatorInfoForName(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);

        var r = info.GetValidSyntax(arg, Context, allowParentTypes: true);
        if (r.IsFailure) return r.Error!;
        return r.Value!.OperandType;
    }

    // Catalog-backed validate + evaluate (binary)
    public virtual Result<object?, ValidationResult> ValidateAndEvaluateBinaryOperator(string operatorName, object? leftArg, object? rightArg)
    {
        var info = GetBinaryOperatorInformation(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.ValidateAndCalc(leftArg, rightArg, Context, allowParentTypes: true);
    }

    public virtual async Task<Result<object?, ValidationResult>> ValidateAndEvaluateBinaryOperatorAsync(string operatorName, object? leftArg, object? rightArg, CancellationToken ct)
    {
        var info = GetBinaryOperatorInformation(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return await info.ValidateAndCalcAsync(leftArg, rightArg, Context, allowParentTypes: true,  ct: ct);
    }

    // Catalog-backed validate + evaluate (unary)
    public virtual Result<object?, ValidationResult> ValidateAndEvaluateUnaryOperator(string operatorName, object? arg)
    {
        var info = ResolveUnaryOperatorInfoForName(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.ValidateAndCalc(arg, Context, allowParentTypes: true);
    }

    public virtual async Task<Result<object?, ValidationResult>> ValidateAndEvaluateUnaryOperatorAsync(string operatorName, object? arg, CancellationToken ct)
    {
        var info = ResolveUnaryOperatorInfoForName(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return await info.ValidateAndCalcAsync(arg, Context, allowParentTypes: true, ct: ct);
    }

    // Catalog-backed resolve output type (binary)
    public virtual Result<Type, ValidationResult> ResolveBinaryOperatorType(string operatorName, object? leftArg, object? rightArg)
    {
        var info = GetBinaryOperatorInformation(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.ResolveOutputType(leftArg, rightArg, Context, allowParentTypes: true);
    }

    // Catalog-backed resolve output type (unary)
    public virtual Result<Type, ValidationResult> ResolveUnaryOperatorType(string operatorName, object? arg)
    {
        var info = ResolveUnaryOperatorInfoForName(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.ResolveOutputType(arg,Context, allowParentTypes: true);
    }

    // Added two catalog-backed helpers for retrieving operator syntax matches (binary + unary).
    // These mirror GetFunctionSyntax and forward to BinaryOperatorInformation/UnaryOperatorInformation.
    public virtual Result<BinaryOperatorSyntaxMatch, ValidationResult> GetBinaryOperatorSyntax(string operatorName, object? leftArg, object? rightArg)
    {
        var info = GetBinaryOperatorInformation(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.GetValidSyntax(leftArg, rightArg, Context, allowParentTypes: true);
    }

    public virtual Result<UnaryOperatorSyntaxMatch, ValidationResult> GetUnaryOperatorSyntax(string operatorName, object? arg)
    {
        var info = ResolveUnaryOperatorInfoForName(operatorName);
        if (info is null) return ValidationHelpers.UnknownOperatorResult(operatorName);
        return info.GetValidSyntax(arg, Context, allowParentTypes: true);
    }

    #endregion

}
