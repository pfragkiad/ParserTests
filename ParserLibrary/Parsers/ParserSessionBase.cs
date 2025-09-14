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
    protected Dictionary<Token, Node<Token>> _nodeDictionary = [];
    protected TokenTree? _tree = null;

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
        _nodeDictionary = [];
        _tree = null;
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
            default:
            case ExpressionOptimizationMode.ParserInference:
                {
                    var initialTree = GetExpressionTree(_expression!);
                    var result = OptimizeTreeUsingInference(initialTree, Variables);
                    var optimizedTree = result.Tree;

                    _tree = optimizedTree;
                    _infixTokens = optimizedTree.GetInfixTokens();
                    _postfixTokens = optimizedTree.GetPostfixTokens();
                    _nodeDictionary = optimizedTree.NodeDictionary;
                    return;
                }

            case ExpressionOptimizationMode.None:
                _infixTokens = GetInfixTokens(_expression!);
                _postfixTokens = GetPostfixTokens(_infixTokens);
                // Build tree so downstream checks can run without rework
                _tree = GetExpressionTree(_postfixTokens);
                _nodeDictionary = _tree.NodeDictionary;
                return;

            case ExpressionOptimizationMode.StaticTypeMaps:
                {
                    variableTypes ??= BuildVariableTypesFromVariables(Variables);
                    var optimizedTree = GetOptimizedExpressionTree(
                        _expression!,
                        variableTypes,
                        functionReturnTypes,
                        ambiguousFunctionReturnTypes);

                    _tree = optimizedTree;
                    _infixTokens = optimizedTree.GetInfixTokens();
                    _postfixTokens = optimizedTree.GetPostfixTokens();
                    _nodeDictionary = optimizedTree.NodeDictionary;
                    return;
                }

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

    #region Preparation API (validate -> optimize -> ready for evaluation)

    /// <summary>
    /// Prepares the parser state for the given expression (and optional variables) in a single pass:
    /// 1) Optionally validates (parentheses, variable names, function names, arguments, operators).
    /// 2) Optionally optimizes the current tree (ParserInference in-place, or StaticTypeMaps with a new tree).
    /// 3) Leaves infix/postfix/tree/node-dictionary ready for a separate Evaluate/EvaluateType call.
    /// Returns validation failures (empty if none or validation disabled). If early return is requested and
    /// failures occur, optimization is skipped and the state reflects whatever was built up to that point.
    /// </summary>
    public List<ValidationFailure> Prepare(
        string expression,
        Dictionary<string, object?>? variables = null,
        VariableNamesOptions? variableNamesOptions = null,
        bool runValidation = true,
        bool earlyReturnOnValidationErrors = false,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.None,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        Reset();
        _expression = expression;
        Variables = variables ?? [];

        if (string.IsNullOrWhiteSpace(_expression))
            return [];

        List<ValidationFailure> failures = [];

        if (runValidation)
        {
            VariableNamesOptions effVarOpts =
                variableNamesOptions ?? new VariableNamesOptions
                {
                    KnownIdentifierNames = new HashSet<string>(
                        Variables.Keys,
                        _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
                };

            var report = Validate(effVarOpts, earlyReturnOnValidationErrors);
            failures = [.. report.GetValidationFailures()];
            if (earlyReturnOnValidationErrors && failures.Count > 0)
                return failures;
        }
        else
        {
            _infixTokens = GetInfixTokens(_expression!);
            _postfixTokens = GetPostfixTokens(_infixTokens);
            _tree = GetExpressionTree(_postfixTokens);
            _nodeDictionary = _tree.NodeDictionary;
        }

        switch (optimizationMode)
        {
            case ExpressionOptimizationMode.None:
                break;

            case ExpressionOptimizationMode.ParserInference:
            {
                var currentTree = _tree ?? GetExpressionTree(_postfixTokens);
                var result = OptimizeTreeUsingInference(currentTree, Variables);
                var optimizedTree = result.Tree;

                _tree = optimizedTree;
                _infixTokens = optimizedTree.GetInfixTokens();
                _postfixTokens = optimizedTree.GetPostfixTokens();
                _nodeDictionary = optimizedTree.NodeDictionary;
                break;
            }

            case ExpressionOptimizationMode.StaticTypeMaps:
            {
                variableTypes ??= BuildVariableTypesFromVariables(Variables);
                var optimizedTree = GetOptimizedExpressionTree(
                    _expression!,
                    variableTypes,
                    functionReturnTypes,
                    ambiguousFunctionReturnTypes);

                _tree = optimizedTree;
                _infixTokens = optimizedTree.GetInfixTokens();
                _postfixTokens = optimizedTree.GetPostfixTokens();
                _nodeDictionary = optimizedTree.NodeDictionary;
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(optimizationMode), optimizationMode, null);
        }

        return failures;
    }

    #endregion

    #region Public evaluation API

    public override object? Evaluate(string expression, Dictionary<string, object?>? variables = null)
    {
        // One-shot convenience: prepare (no validation) -> evaluate
        Prepare(
            expression,
            variables,
            variableNamesOptions: null,
            runValidation: false,
            earlyReturnOnValidationErrors: false,
            optimizationMode: ExpressionOptimizationMode.None);
        return Evaluate();
    }

    public virtual object? Evaluate(Dictionary<string, object?>? variables = null)
    {
        // Convenience: re-prepare current expression with provided variables (no validation/optimization)
        Prepare(
            _expression,
            variables,
            variableNamesOptions: null,
            runValidation: false,
            earlyReturnOnValidationErrors: false,
            optimizationMode: ExpressionOptimizationMode.None);
        return Evaluate();
    }

    public override object? EvaluateWithTreeOptimizer(string expression, Dictionary<string, object?>? variables = null)
    {
        // One-shot convenience: prepare (no validation) with StaticTypeMaps -> evaluate
        Prepare(
            expression,
            variables,
            variableNamesOptions: null,
            runValidation: false,
            earlyReturnOnValidationErrors: false,
            optimizationMode: ExpressionOptimizationMode.StaticTypeMaps);
        return Evaluate();
    }

    public virtual object? EvaluateWithTreeOptimizer(
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.StaticTypeMaps)
    {
        Prepare(
            _expression,
            variables,
            variableNamesOptions: null,
            runValidation: false,
            earlyReturnOnValidationErrors: false,
            optimizationMode: optimizationMode,
            variableTypes: variableTypes,
            functionReturnTypes: functionReturnTypes,
            ambiguousFunctionReturnTypes: ambiguousFunctionReturnTypes);
        return Evaluate();
    }

    public object? EvaluateWithParserInferenceOptimizer(
        string expression,
        Dictionary<string, object?>? variables = null)
    {
        Prepare(
            expression,
            variables,
            variableNamesOptions: null,
            runValidation: false,
            earlyReturnOnValidationErrors: false,
            optimizationMode: ExpressionOptimizationMode.ParserInference);
        return Evaluate();
    }

    public override Type EvaluateType(string expression, Dictionary<string, object?>? variables = null)
    {
        // One-shot convenience: prepare (no validation/optimization) -> type-evaluate
        Prepare(
            expression,
            variables,
            variableNamesOptions: null,
            runValidation: false,
            earlyReturnOnValidationErrors: false,
            optimizationMode: ExpressionOptimizationMode.None);
        return EvaluateType();
    }

    public virtual Type EvaluateType(Dictionary<string, object?>? variables = null)
    {
        // Convenience: re-prepare current expression with provided variables (no validation/optimization)
        Prepare(
            _expression,
            variables,
            variableNamesOptions: null,
            runValidation: false,
            earlyReturnOnValidationErrors: false,
            optimizationMode: ExpressionOptimizationMode.None);
        return EvaluateType();
    }

    public object? Evaluate() =>
        (_tree is not null)
            ? Evaluate(_tree, _variables, mergeConstants: true)
            : Evaluate(_postfixTokens, _variables);

    public Type EvaluateType() =>
        (_tree is not null)
            ? EvaluateType(_tree, _variables, mergeConstants: true)
            : EvaluateType(_postfixTokens, _variables);

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
    public virtual ParserValidationReport Validate(
        VariableNamesOptions variableNamesOptions,
        bool earlyReturnOnErrors = false)
    {
        var report = new ParserValidationReport { Expression = _expression };

        if (string.IsNullOrWhiteSpace(_expression))
            return report;

        // 1) Parentheses pre-check (string-only)
        var parenthesesResult = _tokenizerValidator.CheckParentheses(_expression);
        report.ParenthesesResult = parenthesesResult;
        if (!parenthesesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched parentheses in formula: {expr}", _expression);
            if (earlyReturnOnErrors) return report;
        }

        // 2) Acquire/reuse infix tokens
        var infixTokens = _infixTokens.Count != 0 ? _infixTokens : GetInfixTokens(_expression);
        if (_infixTokens.Count == 0) _infixTokens = infixTokens;

        // 3) Tokenizer stage: variable names
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
        report.VariableNamesResult = variableNamesResult;
        if (!variableNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched variable names in formula: {expr}", _expression);
            if (earlyReturnOnErrors) return report;
        }

        // 4) Parser stage: function names
        var functionNamesResult = _parserValidator.CheckFunctionNames(infixTokens, (IParserFunctionMetadata)this);
        report.FunctionNamesResult = functionNamesResult;
        if (!functionNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function names in formula: {expr}", _expression);
            if (earlyReturnOnErrors) return report;
        }

        // 5) Build/reuse postfix and tree for node-dictionary-based checks
        var postfixTokens = _postfixTokens.Count != 0 ? _postfixTokens : GetPostfixTokens(infixTokens);
        if (_postfixTokens.Count == 0) _postfixTokens = postfixTokens;

        _tree = GetExpressionTree(postfixTokens);
        _nodeDictionary = _tree.NodeDictionary;

        // 6) Empty function arguments
        var emptyFunctionArgumentsResult = _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary);
        report.EmptyFunctionArgumentsResult = emptyFunctionArgumentsResult;
        if (!emptyFunctionArgumentsResult.IsSuccess)
        {
            _logger.LogWarning("Empty function arguments in formula: {expr}", _expression);
            if (earlyReturnOnErrors) return report;
        }

        // 7) Function arguments count
        var functionArgumentsCountResult = _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IParserFunctionMetadata)this);
        report.FunctionArgumentsCountResult = functionArgumentsCountResult;
        if (!functionArgumentsCountResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function arguments in formula: {expr}", _expression);
            if (earlyReturnOnErrors) return report;
        }

        // 8) Invalid operators
        var operatorOperandsResult = _parserValidator.CheckOperatorOperands(_nodeDictionary);
        report.OperatorOperandsResult = operatorOperandsResult;
        if (!operatorOperandsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid operators in formula: {expr}", _expression);
            if (earlyReturnOnErrors) return report;
        }

        // 9) Orphan/invalid argument separators
        var orphanArgumentSeparatorsResult = _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);
        report.OrphanArgumentSeparatorsResult = orphanArgumentSeparatorsResult;
        if (!orphanArgumentSeparatorsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid argument separators in formula: {expr}", _expression);
            if (earlyReturnOnErrors) return report;
        }

        return report;
    }

    #endregion
}
