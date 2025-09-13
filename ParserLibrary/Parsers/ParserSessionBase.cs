using FluentValidation.Results;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;
using ParserLibrary.Parsers.Validation;

namespace ParserLibrary.Parsers;

public class ParserSessionBase : ParserBase, IParserSession
{
    protected List<Token> _infixTokens = [];
    protected List<Token> _postfixTokens = [];
    protected internal Dictionary<Node<Token>, object?> _nodeValueDictionary = [];
    protected Dictionary<Token, Node<Token>> _nodeDictionary = [];
    protected Stack<Token> _stack = [];

    public ParserSessionBase(
        ILogger<ParserSessionBase> logger,
        IOptions<TokenizerOptions> options,
        ITokenizerValidator tokenizerValidator,
        IParserValidator parserValidator)
        : base(logger, options, tokenizerValidator, parserValidator)
    { }

    protected internal ParserSessionBase(ILogger logger, ParserServices services)
        : base(logger, services) { }


    protected void Reset()
    {
        _infixTokens = [];
        _postfixTokens = [];
        _nodeValueDictionary = [];
        _nodeDictionary = [];
        _stack = [];
    }

    private string _expression = "";
    public string Expression { get => _expression; set => _expression = value; }

    private void PrepareExpression(
        string expression,
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        _expression = expression;
        PrepareExpressionInternal(
            optimizationMode,
            variables,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes);
    }

    private void PrepareExpressionInternal(
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        Reset();
        if (string.IsNullOrWhiteSpace(_expression)) return;

        Variables = variables ?? [];

        switch (optimizationMode)
        {
            case ExpressionOptimizationMode.None:
                _infixTokens = GetInfixTokens(_expression!);
                _postfixTokens = GetPostfixTokens(_infixTokens);
                return;

            case ExpressionOptimizationMode.StaticTypeMaps:
                {
                    variableTypes ??= BuildVariableTypesFromVariables(Variables);
                    var optimizedTree = GetOptimizedExpressionTree(
                        _expression!,
                        variableTypes,
                        functionReturnTypes,
                        ambiguousFunctionReturnTypes);

                    _infixTokens = optimizedTree.GetInfixTokens();
                    _postfixTokens = optimizedTree.GetPostfixTokens();
                    return;
                }

            case ExpressionOptimizationMode.ParserInference:
                {
                    var initialTree = GetExpressionTree(_expression!);
                    var result = OptimizeTreeUsingInference(initialTree, Variables);
                    var optimizedTree = result.Tree;
                    _infixTokens = optimizedTree.GetInfixTokens();
                    _postfixTokens = optimizedTree.GetPostfixTokens();
                    return;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(optimizationMode), optimizationMode, null);
        }
    }

    private static Dictionary<string, Type> BuildVariableTypesFromVariables(Dictionary<string, object?> variables) =>
        variables
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.GetType());

    protected Dictionary<string, object?> _variables = [];
    public Dictionary<string, object?> Variables
    {
        get => _variables;
        set => _variables = MergeVariableConstants(value);
    }

    #region Public evaluation API

    public override object? Evaluate(string expression, Dictionary<string, object?>? variables = null)
    {
        PrepareExpression(expression, ExpressionOptimizationMode.None, variables);
        return Evaluate();
    }

    public virtual object? Evaluate(Dictionary<string, object?>? variables = null)
    {
        PrepareExpressionInternal(ExpressionOptimizationMode.None, variables);
        return Evaluate();
    }

    public override object? EvaluateWithTreeOptimizer(string expression, Dictionary<string, object?>? variables = null)
    {
        PrepareExpression(
            expression,
            ExpressionOptimizationMode.StaticTypeMaps,
            variables);
        return Evaluate();
    }

    public virtual object? EvaluateWithTreeOptimizer(
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.StaticTypeMaps)
    {
        PrepareExpressionInternal(
            optimizationMode,
            variables,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes);
        return Evaluate();
    }

    public object? EvaluateWithParserInferenceOptimizer(
        string expression,
        Dictionary<string, object?>? variables = null)
    {
        PrepareExpression(
            expression,
            ExpressionOptimizationMode.ParserInference,
            variables);
        return Evaluate();
    }

    public override Type EvaluateType(string expression, Dictionary<string, object?>? variables = null)
    {
        PrepareExpression(
            expression,
            ExpressionOptimizationMode.None,
            variables);
        return EvaluateType();
    }

    public virtual Type EvaluateType(Dictionary<string, object?>? variables = null)
    {
        PrepareExpressionInternal(ExpressionOptimizationMode.None, variables);
        return EvaluateType();
    }

    public object? Evaluate() =>
        Evaluate(_postfixTokens, _variables, _stack, _nodeDictionary, _nodeValueDictionary, mergeConstants: false);

    public Type EvaluateType() =>
        EvaluateType(_postfixTokens, _variables, _stack, _nodeDictionary, _nodeValueDictionary, mergeConstants: false);

    #endregion

    #region Utility validation methods

    #region Tokenizer 

    public ParenthesisCheckResult ValidateParentheses() => ValidateParentheses(_expression);

    public List<string> GetVariableNames() => GetVariableNames(_infixTokens);

    // MODIFIED: delegate variable-name checks directly to tokenizer validator (infix-based)
    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, knownIdentifierNames, ignorePrefixes, ignorePostfixes);

    public VariableNamesCheckResult CheckVariableNames(
       HashSet<string> knownIdentifierNames,
       Regex? ignoreIdentifierPattern = null) =>
       _tokenizerValidator.CheckVariableNames(_infixTokens, knownIdentifierNames, ignoreIdentifierPattern);


    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        string[] ignoreCaptureGroups) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, knownIdentifierNames, ignoreCaptureGroups);

    public VariableNamesCheckResult CheckVariableNames(VariableNamesOptions variableNameOptions) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, variableNameOptions);

    #endregion

    #region Parser

    // MODIFIED: parser-level checks now delegate to IParserValidator (infix/tree-based)
    public FunctionNamesCheckResult CheckFunctionNames() =>
        _parserValidator.CheckFunctionNames(_infixTokens, (IParserFunctionMetadata)this);

    public InvalidOperatorsCheckResult CheckOperators() =>
        _parserValidator.CheckOperatorOperands(_nodeDictionary);

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators() =>
        _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount() =>
        _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IParserFunctionMetadata)this);

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments() =>
        _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary);

    #endregion

    // Updated: align with CoreParser.Validate and honor provided VariableNamesOptions vs. current Variables.
    public virtual List<ValidationFailure> Validate(
        VariableNamesOptions variableNamesOptions,
        bool earlyReturnOnErrors = false)
    {
        if (string.IsNullOrWhiteSpace(_expression)) return [];

        var failures = new List<ValidationFailure>();

        // 1) Parentheses pre-check (string-only)
        var parenthesesResult = _tokenizerValidator.CheckParentheses(_expression);
        if (!parenthesesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched parentheses in formula: {expr}", _expression);
            failures.AddRange(parenthesesResult.GetValidationFailures());
            if (earlyReturnOnErrors) return failures;
        }

        // 2) Acquire/reuse infix tokens
        var infixTokens = _infixTokens.Count != 0 ? _infixTokens : GetInfixTokens(_expression);
        if (_infixTokens.Count == 0) _infixTokens = infixTokens; // keep state in sync   <--------------

        // 3) Tokenizer stage: variable names
        // Prefer the provided KnownIdentifierNames; if empty/null, fall back to Variables.Keys (already merged with Constants).
        var effectiveKnown =
            (variableNamesOptions.KnownIdentifierNames is { Count: > 0 })
                ? variableNamesOptions.KnownIdentifierNames
                : new HashSet<string>(Variables.Keys,
                    _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        var effectiveVarOpts = new VariableNamesOptions
        {
            KnownIdentifierNames = effectiveKnown,
            IgnoreCaptureGroups = variableNamesOptions.IgnoreCaptureGroups,
            IgnoreIdentifierPattern = variableNamesOptions.IgnoreIdentifierPattern,
            IgnorePrefixes = variableNamesOptions.IgnorePrefixes,
            IgnorePostfixes = variableNamesOptions.IgnorePostfixes
        };

        var variableNamesResult = _tokenizerValidator.CheckVariableNames(infixTokens, effectiveVarOpts);
        if (!variableNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched variable names in formula: {expr}", _expression);
            failures.AddRange(variableNamesResult.GetValidationFailures());
            if (earlyReturnOnErrors) return failures;
        }

        // 4) Parser stage: function names (requires infix + metadata)
        var functionNamesResult = _parserValidator.CheckFunctionNames(infixTokens, (IParserFunctionMetadata)this);
        if (!functionNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function names in formula: {expr}", _expression);
            failures.AddRange(functionNamesResult.GetValidationFailures());
            if (earlyReturnOnErrors) return failures;
        }

        // 5) Build/reuse postfix and tree for node-dictionary-based checks
        var postfixTokens = _postfixTokens.Count != 0 ? _postfixTokens : GetPostfixTokens(infixTokens);
        if (_postfixTokens.Count == 0) _postfixTokens = postfixTokens; // keep state in sync   <--------------

        var tree = GetExpressionTree(postfixTokens);
        _nodeDictionary = tree.NodeDictionary; // keep state in sync   <--------------

        // 6) Parser stage: empty function arguments
        var emptyFunctionArgumentsResult = _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary);
        if (!emptyFunctionArgumentsResult.IsSuccess)
        {
            _logger.LogWarning("Empty function arguments in formula: {expr}", _expression);
            failures.AddRange(emptyFunctionArgumentsResult.GetValidationFailures());
            if (earlyReturnOnErrors) return failures;
        }

        // 7) Parser stage: function arguments count (needs metadata)
        var functionArgumentsCountResult = _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IParserFunctionMetadata)this);
        if (!functionArgumentsCountResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function arguments in formula: {expr}", _expression);
            failures.AddRange(functionArgumentsCountResult.GetValidationFailures());
            if (earlyReturnOnErrors) return failures;
        }

        // 8) Parser stage: invalid operators
        var operatorOperandsResult = _parserValidator.CheckOperatorOperands(_nodeDictionary);
        if (!operatorOperandsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid operators in formula: {expr}", _expression);
            failures.AddRange(operatorOperandsResult.GetValidationFailures());
            if (earlyReturnOnErrors) return failures;
        }

        // 9) Parser stage: orphan/invalid argument separators
        var orphanArgumentSeparatorsResult = _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);
        if (!orphanArgumentSeparatorsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid argument separators in formula: {expr}", _expression);
            failures.AddRange(orphanArgumentSeparatorsResult.GetValidationFailures());
            if (earlyReturnOnErrors) return failures;
        }

        return failures;
    }
    #endregion


}
