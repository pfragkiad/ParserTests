using FluentValidation.Results;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;
using ParserLibrary.Parsers.Validation;

namespace ParserLibrary.Parsers;

// Ensure stateful parser passes both validators to CoreParser
public class CoreStatefulParser : CoreParser, IStatefulParser
{
    // Replaced previous inner enum with public enum ExpressionOptimizationMode (see interface)
    
    protected internal Dictionary<Node<Token>, object?> _nodeValueDictionary = [];
    protected Dictionary<Token, Node<Token>> _nodeDictionary = [];
    protected Stack<Token> _stack = [];

    protected List<Token> _infixTokens = [];
    protected List<Token> _postfixTokens = [];

    public CoreStatefulParser(
        ILogger<CoreStatefulParser> logger,
        IOptions<TokenizerOptions> options,
        ITokenizerValidator tokenizerValidator,
        IParserValidator parserValidator)
        : base(logger, options, tokenizerValidator, parserValidator)
    {
    }

    protected void Reset()
    {
        _infixTokens = [];
        _postfixTokens = [];
        _nodeValueDictionary = [];
        _nodeDictionary = [];
        _stack = [];
    }

    private string? _expression;
    public string? Expression { get => _expression; set => _expression = value; }

    private void PrepareExpression(
        string? expression,
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

    #region Validation checks

    #region Tokenizer 
    public bool AreParenthesesMatched() =>
        string.IsNullOrWhiteSpace(Expression) || PreValidateParentheses(Expression!, out _);

    public ParenthesisErrorCheckResult CheckParentheses()
    {
        if (string.IsNullOrWhiteSpace(Expression))
            return ParenthesisErrorCheckResult.Success;

        _ = PreValidateParentheses(Expression!, out var detail);
        return detail ?? new ParenthesisErrorCheckResult { UnmatchedClosed = [], UnmatchedOpen = [] };
    }

    public List<string> GetVariableNames() =>
        GetVariableNames(_infixTokens);

    // MODIFIED: delegate variable-name checks directly to tokenizer validator (infix-based)
    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> identifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, identifierNames, ignorePrefixes, ignorePostfixes);

    public VariableNamesCheckResult CheckVariableNames(
       HashSet<string> identifierNames,
       Regex? ignoreIdentifierPattern = null) =>
       _tokenizerValidator.CheckVariableNames(_infixTokens, identifierNames, ignoreIdentifierPattern);


    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> identifierNames,
        string[] ignoreCaptureGroups) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, identifierNames, ignoreCaptureGroups);
    #endregion

    #region Parser

    // MODIFIED: parser-level checks now delegate to IParserValidator (infix/tree-based)
    public FunctionNamesCheckResult CheckFunctionNames() =>
        _parserValidator.CheckFunctionNames(_infixTokens, (IParserFunctionMetadata)this);

    public InvalidOperatorsCheckResult CheckOperators() =>
        _parserValidator.CheckOperators(_nodeDictionary);

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators() =>
        _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount() =>
        _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IParserFunctionMetadata)this, _options.TokenPatterns);

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments() =>
        _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary, _options.TokenPatterns);


    #endregion

    public virtual List<ValidationFailure> Validate(string[]? ignoreIdentifierCaptureGroups = null)
    {
        if (string.IsNullOrWhiteSpace(_expression)) return [];

        var failures = new List<ValidationFailure>();

        // 1) Parentheses pre-check (early exit)
        if (!PreValidateParentheses(_expression!, out var parenDetail))
        {
            _logger.LogWarning("Unmatched parentheses in formula: {formula}", _expression);
            if (parenDetail is not null)
                failures.AddRange(parenDetail.GetValidationFailures());
            return failures;
        }

        // Ensure tokens exist for validation
        var infix = _infixTokens.Count > 0 ? _infixTokens : GetInfixTokens(_expression!);

        // 2) Variable-name checks (tokenizer-level; infix-only)
        var varOpts = new VariableNamesOptions
        {
            IdentifierNames = new HashSet<string>(_variables.Keys),
            IgnoreCaptureGroups = ignoreIdentifierCaptureGroups
        };
        var nameResult = _tokenizerValidator.PostValidateVariableNames(infix, varOpts);
        if (!nameResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched/ignored identifiers in formula: {formula}", _expression);
            failures.AddRange(nameResult.GetValidationFailures());
        }

        // 3) Build a tree for parser-level checks (operators, separators, functions)
        var tree = GetExpressionTree(_expression!);

        var report = _parserValidator.Validate(
            _expression!,
            infixTokens: infix,
            tree: tree,
            metadata: (IParserFunctionMetadata)this,
            stopAtTokenizerErrors: false);

    if (report.FunctionNames is not null && !report.FunctionNames.IsSuccess)
        failures.AddRange(report.FunctionNames.GetValidationFailures());

    if (report.Operators is not null && !report.Operators.IsSuccess)
        failures.AddRange(report.Operators.GetValidationFailures());

    if (report.ArgumentSeparators is not null && !report.ArgumentSeparators.IsSuccess)
        failures.AddRange(report.ArgumentSeparators.GetValidationFailures());

    if (report.FunctionArgumentsCount is not null && !report.FunctionArgumentsCount.IsSuccess)
        failures.AddRange(report.FunctionArgumentsCount.GetValidationFailures());

    if (report.EmptyFunctionArguments is not null && !report.EmptyFunctionArguments.IsSuccess)
        failures.AddRange(report.EmptyFunctionArguments.GetValidationFailures());

    return failures;
    }


    #endregion


}
