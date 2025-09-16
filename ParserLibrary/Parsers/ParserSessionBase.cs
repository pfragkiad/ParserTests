using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.Interfaces;
using ParserLibrary.Parsers.Validation;
using OneOf;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;

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
    {
        Reset();
    }

    protected internal ParserSessionBase(ILogger logger, ParserServices services)
        : base(logger, services)
    {
        Reset();
    }

    private ParserSessionState _state;
    public ParserSessionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            var old = _state;
            _state = value;
            OnStateChanged(old, _state);
        }
    }

    public Exception? LastException { get; protected set; }

    public ParserValidationReport? Report { get; protected set; }

    public bool HasValidated => Report is not null;


    public event EventHandler<ParserSessionStateChangedEventArgs>? StateChanged;

    protected virtual void OnStateChanged(ParserSessionState oldState, ParserSessionState newState) =>
        StateChanged?.Invoke(this, new ParserSessionStateChangedEventArgs(oldState, newState));

    protected void Reset()
    {
        _infixTokens = [];
        _postfixTokens = [];
        _nodeDictionary = [];
        _tree = null;
        State = ParserSessionState.Uninitialized;
        LastException = null;
    }

    private string _expression = "";
    public string Expression
    {
        get => _expression;
        set
        {
            Reset(); //always reset on expression change
            _expression = value;
            State = ParserSessionState.ExpressionSet;
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

    #region Validation + Optimization API

    /// <summary>
    /// Validates (optional) and optimizes (optional) the current expression and updates the session caches.
    /// Returns the full validation report (success if validation is disabled and no errors).
    /// </summary>
    public ParserValidationReport ValidateAndOptimize(
        string expression,

        // validation related
        Dictionary<string, object?>? variables = null,
        VariableNamesOptions? variableNamesOptions = null,
        bool runValidation = true,
        bool earlyReturnOnValidationErrors = false,

        // optimization related
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.None,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        Expression = expression; //also resets state
        Variables = variables ?? [];

        var report = new ParserValidationReport { Expression = _expression };
        if (string.IsNullOrWhiteSpace(_expression))
            return report;

        if (runValidation)
        {
            VariableNamesOptions effVarOpts =
                variableNamesOptions ?? new VariableNamesOptions
                {
                    KnownIdentifierNames = new HashSet<string>(
                        Variables.Keys,
                        _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
                };

            report = Validate(effVarOpts, earlyReturnOnValidationErrors);
            if (earlyReturnOnValidationErrors && !report.IsSuccess)
            {
                State = ParserSessionState.Invalid;
                return report;
            }
        }
        else
        {
            // Build infix/postfix/tree/nodeDictionary once without validation
            _infixTokens = GetInfixTokens(_expression!);
            State = ParserSessionState.TokenizedInfix;

            _postfixTokens = GetPostfixTokens(_infixTokens);
            State = ParserSessionState.TokenizedPostfix;

            _tree = GetExpressionTree(_postfixTokens);
            _nodeDictionary = _tree.NodeDictionary;
            State = ParserSessionState.TreeBuilt;
        }

        // Optimization (optional)
        if (optimizationMode != ExpressionOptimizationMode.None)
        {
            _ = GetOptimizedTree(optimizationMode, variableTypes, functionReturnTypes, ambiguousFunctionReturnTypes);
            State = ParserSessionState.Optimized;
        }

        return report;
    }

    // Optimization only — public API
    public TreeOptimizerResult GetOptimizedTree(
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        if (string.IsNullOrWhiteSpace(_expression))
            throw new InvalidOperationException("Expression is empty. Set Expression before optimizing.");

        // Ensure we have a baseline tree
        if (_tree is null)
        {
            if (_postfixTokens.Count == 0)
            {
                if (_infixTokens.Count == 0)
                {
                    _infixTokens = GetInfixTokens(_expression!);
                    State = ParserSessionState.TokenizedInfix;
                }
                _postfixTokens = GetPostfixTokens(_infixTokens);
                State = ParserSessionState.TokenizedPostfix;
            }
            _tree = GetExpressionTree(_postfixTokens);
            _nodeDictionary = _tree.NodeDictionary;
            State = ParserSessionState.TreeBuilt;
        }

        switch (optimizationMode)
        {
            case ExpressionOptimizationMode.None:
                return TreeOptimizerResult.Unchanged(_tree!);

            default:
            case ExpressionOptimizationMode.ParserInference:
                {
                    TreeOptimizerResult result = GetOptimizedTree(_tree!, Variables);
                    var optimizedTree = result.Tree;
                    _tree = optimizedTree;
                    _infixTokens = optimizedTree.GetInfixTokens();
                    _postfixTokens = optimizedTree.GetPostfixTokens();
                    _nodeDictionary = optimizedTree.NodeDictionary;
                    State = ParserSessionState.Optimized;
                    return result;
                }

            case ExpressionOptimizationMode.StaticTypeMaps:
                {
                    variableTypes ??= BuildVariableTypesFromVariables(Variables);
                    var result = _tree.OptimizeForDataTypes(
                        _options.TokenPatterns,
                        variableTypes,
                        functionReturnTypes,
                        ambiguousFunctionReturnTypes);

                    var optimizedTree = result.Tree;
                    _tree = optimizedTree;
                    _infixTokens = optimizedTree.GetInfixTokens();
                    _postfixTokens = optimizedTree.GetPostfixTokens();
                    _nodeDictionary = optimizedTree.NodeDictionary;
                    State = ParserSessionState.Optimized;
                    return result;
                }

        }
    }

    #endregion

    #region Public evaluation API

    public virtual OneOf<object?, ParserValidationReport> Evaluate(
        Dictionary<string, object?>? variables = null,
        bool runValidation = false,
        ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.None)
    {
        ParserValidationReport report = ValidateAndOptimize(
            _expression,
            variables,
            variableNamesOptions: null,
            runValidation: runValidation,
            earlyReturnOnValidationErrors: false,
            optimizationMode: optimizationMode);

        if (!report.IsSuccess)
            return report;

        var value = Evaluate();
        State = ParserSessionState.Calculated;
        return value;
    }

    public virtual Type EvaluateType(Dictionary<string, object?>? variables = null)
    {
        ValidateAndOptimize(
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

    public FunctionNamesCheckResult CheckFunctionNames() =>
        _parserValidator.CheckFunctionNames(_infixTokens, (IParserFunctionMetadata)this);

    public AdjacentOperandsCheckResult CheckAdjacentOperands() =>
        _tokenizerValidator.CheckAdjacentOperands(_infixTokens);

    public InvalidBinaryOperatorsCheckResult CheckBinaryOperators() =>
        _parserValidator.CheckBinaryOperatorOperands(_nodeDictionary);

    public InvalidUnaryOperatorsCheckResult CheckUnaryOperators() =>
        _parserValidator.CheckUnaryOperatorOperands(_nodeDictionary);


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
        {
            State = ParserSessionState.Validated;
            return Report = report;
        }

        // 1) Parentheses
        var parenthesesResult = _tokenizerValidator.CheckParentheses(_expression);
        report.ParenthesesResult = parenthesesResult;
        State = ParserSessionState.ParenthesesChecked;
        if (!parenthesesResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            if (earlyReturnOnErrors) return Report = report;
        }

        // 2) Infix
        try
        {
            var infixTokens = _infixTokens.Count != 0 ? _infixTokens : GetInfixTokens(_expression);
            if (_infixTokens.Count == 0) _infixTokens = infixTokens;
            State = ParserSessionState.TokenizedInfix;
        }
        catch (Exception ex)
        {
            LastException = new InvalidOperationException("Could not tokenize (get infix tokens).", innerException: ex);
            report.Exception = ex;
            State = ParserSessionState.Invalid;
            return Report = report;
        }

        // 3) Variables (names)
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
        var variableNamesResult = _tokenizerValidator.CheckVariableNames(_infixTokens, effectiveVarOpts);
        report.VariableNamesResult = variableNamesResult;
        if (!variableNamesResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            if (earlyReturnOnErrors) return Report = report;
        }

        // 4) Functions (names)
        var functionNamesResult = _parserValidator.CheckFunctionNames(_infixTokens, (IParserFunctionMetadata)this);
        report.FunctionNamesResult = functionNamesResult;
        if (!functionNamesResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            if (earlyReturnOnErrors) return Report = report;
        }

        // 4.5) NEW: Adjacent operands (avoid building invalid postfix/tree)
        var adjacentOperandsResult = _tokenizerValidator.CheckAdjacentOperands(_infixTokens);
        report.AdjacentOperandsResult = adjacentOperandsResult;

        // If we have any errors so far, ALWAYS return (cannot build postfix/tree safely)
        if (!report.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            return Report = report;
        }

        try
        {
            // 5+) Build postfix/tree and continue with parser checks
            var postfixTokens = _postfixTokens.Count != 0 ? _postfixTokens : GetPostfixTokens(_infixTokens);
            if (_postfixTokens.Count == 0) _postfixTokens = postfixTokens;
            State = ParserSessionState.TokenizedPostfix;

            _tree = GetExpressionTree(_postfixTokens);
            _nodeDictionary = _tree.NodeDictionary;
            State = ParserSessionState.TreeBuilt;
        }
        catch (Exception ex)
        {
            LastException = new InvalidOperationException("Could not convert to postfix tokens.", innerException: ex);
            report.Exception = ex;
            State = ParserSessionState.Invalid;
            return Report = report;
        }

        // 6) Empty function arguments
        var emptyFunctionArgumentsResult = _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary);
        report.EmptyFunctionArgumentsResult = emptyFunctionArgumentsResult;
        if (!emptyFunctionArgumentsResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            if (earlyReturnOnErrors) return Report = report;
        }

        // 7) Function arguments count
        var functionArgumentsCountResult = _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IParserFunctionMetadata)this);
        report.FunctionArgumentsCountResult = functionArgumentsCountResult;
        if (!functionArgumentsCountResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            if (earlyReturnOnErrors) return Report = report;
        }

        // 8) Invalid operators
        var binaryOperatorOperandsResult = _parserValidator.CheckBinaryOperatorOperands(_nodeDictionary);
        report.BinaryOperatorOperandsResult = binaryOperatorOperandsResult;
        if (!binaryOperatorOperandsResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            if (earlyReturnOnErrors) return Report = report;
        }

        var unaryOperatorOperandsResult = _parserValidator.CheckUnaryOperatorOperands(_nodeDictionary);
        report.UnaryOperatorOperandsResult = unaryOperatorOperandsResult;
        if (!unaryOperatorOperandsResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            if (earlyReturnOnErrors) return Report = report;
        }

        var orphanArgumentSeparatorsResult = _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);
        report.OrphanArgumentSeparatorsResult = orphanArgumentSeparatorsResult;
        if (!orphanArgumentSeparatorsResult.IsSuccess)
        {
            State = ParserSessionState.Invalid;
        }

        State = report.IsSuccess ? ParserSessionState.Validated : ParserSessionState.Invalid;
        return Report = report;
    }

    #endregion
}
