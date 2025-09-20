using OneOf;
using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;
using ParserLibrary.Tokenizers.Interfaces;
using System.Linq.Expressions;

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
            if (!CanTransition(_state, value)) return; // enforce Invalid as terminal (except Reset)
            var old = _state;
            _state = value;
            OnStateChanged(old, _state);
        }
    }

    private static bool CanTransition(ParserSessionState from, ParserSessionState to) =>
        from != ParserSessionState.Invalid || to == ParserSessionState.Uninitialized;

    private void ForceState(ParserSessionState newState)
    {
        if (_state == newState) return;
        var old = _state;
        _state = newState;
        OnStateChanged(old, _state);
    }

    public Exception? LastException { get; protected set; }
    public ParserValidationReport? ValidationReport { get; protected set; }
    public bool HasValidated => ValidationReport is not null;

    public ParserValidationStage LastValidationState { get; private set; } = ParserValidationStage.None;

    public event EventHandler<ParserSessionStateChangedEventArgs>? StateChanged;
    protected virtual void OnStateChanged(ParserSessionState oldState, ParserSessionState newState) =>
        StateChanged?.Invoke(this, new ParserSessionStateChangedEventArgs(oldState, newState));

    protected void Reset()
    {
        _infixTokens = [];
        _postfixTokens = [];
        _nodeDictionary = [];
        _tree = null;
        LastException = null;
        ValidationReport = null;
        LastValidationState = ParserValidationStage.None;
        ForceState(ParserSessionState.Uninitialized);
    }

    private string _expression = "";
    public string Expression
    {
        get => _expression;
        set
        {
            Reset();
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


    //[Obsolete]
    //public ParserValidationReport ValidateAndOptimize(
    //    string expression,
    //    Dictionary<string, object?>? variables = null,
    //    VariableNamesOptions? variableNamesOptions = null,
    //    bool runValidation = true,
    //    bool earlyReturnOnValidationErrors = false,
    //    ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.None,
    //    Dictionary<string, Type>? variableTypes = null,
    //    Dictionary<string, Type>? functionReturnTypes = null,
    //    Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    //{
    //    Expression = expression;
    //    Variables = variables ?? [];

    //    var report = new ParserValidationReport { Expression = _expression };
    //    if (string.IsNullOrWhiteSpace(_expression))
    //    {
    //        State = ParserSessionState.Validated;
    //        return ValidationReport = report;
    //    }

    //    if (runValidation)
    //    {
    //        VariableNamesOptions effVarOpts =
    //            variableNamesOptions ?? new VariableNamesOptions
    //            {
    //                KnownIdentifierNames = new HashSet<string>(
    //                    Variables.Keys,
    //                    _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
    //            };

    //        // Note: optimization is performed inside base.Compile below (post checks)
    //        report = ValidateAndCompile(effVarOpts, earlyReturnOnValidationErrors, optimizationMode, variableTypes, functionReturnTypes, ambiguousFunctionReturnTypes);
    //        return report;
    //    }
    //    else
    //    {
    //        try
    //        {
    //            // Compile as far as needed and optimize inside compile if requested
    //            Compile(optimizationMode, variableTypes, functionReturnTypes, ambiguousFunctionReturnTypes);
    //        }
    //        catch (Exception ex)
    //        {
    //            report.Exception = ex;
    //            return report;
    //        }
    //    }

    //    return report;
    //}

    //// Optimization only — public API
    //public TreeOptimizerResult GetOptimizedTree(
    //    ExpressionOptimizationMode optimizationMode,
    //    Dictionary<string, Type>? variableTypes = null,
    //    Dictionary<string, Type>? functionReturnTypes = null,
    //    Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    //{
    //    if (string.IsNullOrWhiteSpace(_expression))
    //        throw new InvalidOperationException("Expression is empty. Set Expression before optimizing.");

    //    // Ensure we have a baseline tree; leverage compile to build and optimize if needed
    //    if (_tree is null)
    //    {
    //        // Build tree without extra validation, but do not optimize here; caller asked explicitly for optimizer
    //        Compile(ExpressionOptimizationMode.None, variableTypes, functionReturnTypes, ambiguousFunctionReturnTypes);
    //    }

    //    switch (optimizationMode)
    //    {
    //        case ExpressionOptimizationMode.None:
    //            return new TreeOptimizerResult { Tree = _tree! };

    //        default:
    //        case ExpressionOptimizationMode.ParserInference:
    //            {
    //                var result = GetOptimizedTree(_tree!, Variables);
    //                _tree = result.Tree;
    //                _infixTokens = _tree.GetInfixTokens();
    //                _postfixTokens = _tree.GetPostfixTokens();
    //                _nodeDictionary = _tree.NodeDictionary;
    //                State = ParserSessionState.Optimized;
    //                return result;
    //            }

    //        case ExpressionOptimizationMode.StaticTypeMaps:
    //            {
    //                variableTypes ??= BuildVariableTypesFromVariables(Variables);
    //                var result = _tree!.OptimizeForDataTypes(
    //                    _options.TokenPatterns,
    //                    variableTypes,
    //                    functionReturnTypes,
    //                    ambiguousFunctionReturnTypes);

    //                _tree = result.Tree;
    //                _infixTokens = _tree.GetInfixTokens();
    //                _postfixTokens = _tree.GetPostfixTokens();
    //                _nodeDictionary = _tree.NodeDictionary;
    //                State = ParserSessionState.Optimized;
    //                return result;
    //            }
    //    }
    //}
    //// Validation + compile + optional optimization in one pass

    //[Obsolete]
    //public virtual ParserValidationReport ValidateAndCompile(
    //    VariableNamesOptions variableNamesOptions,
    //    bool earlyReturnOnErrors = false,
    //    ExpressionOptimizationMode optimizationMode = ExpressionOptimizationMode.None,
    //    Dictionary<string, Type>? variableTypes = null,
    //    Dictionary<string, Type>? functionReturnTypes = null,
    //    Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    //{
    //    var report = new ParserValidationReport { Expression = _expression };
    //    LastValidationState = ParserValidationStage.None;

    //    if (string.IsNullOrWhiteSpace(_expression))
    //    {
    //        State = ParserSessionState.Validated;
    //        return ValidationReport = report;
    //    }

    //    State = ParserSessionState.Prevalidating;

    //    // 1) Parentheses
    //    LastValidationState = ParserValidationStage.Parentheses;
    //    var parenthesesResult = _tokenizerValidator.CheckParentheses(_expression);
    //    report.ParenthesesResult = parenthesesResult;
    //    if (!parenthesesResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        if (earlyReturnOnErrors) return ValidationReport = report;
    //    }

    //    // 2) Infix
    //    LastValidationState = ParserValidationStage.InfixTokenization;
    //    try
    //    {
    //        var infixTokens = _infixTokens.Count != 0 ? _infixTokens : GetInfixTokens(_expression);
    //        if (_infixTokens.Count == 0) _infixTokens = infixTokens;
    //        State = ParserSessionState.TokenizedInfix;
    //    }
    //    catch (Exception ex)
    //    {
    //        LastException = new InvalidOperationException("Could not tokenize (get infix tokens).", innerException: ex);
    //        report.Exception = ex;
    //        State = ParserSessionState.Invalid;
    //        return ValidationReport = report;
    //    }

    //    // 3) Variables (names)
    //    LastValidationState = ParserValidationStage.VariableNames;
    //    var effectiveKnown =
    //        (variableNamesOptions.KnownIdentifierNames is { Count: > 0 })
    //            ? variableNamesOptions.KnownIdentifierNames
    //            : new HashSet<string>(Variables.Keys,
    //                _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    //    var effectiveVarOpts = new VariableNamesOptions
    //    {
    //        KnownIdentifierNames = effectiveKnown,
    //        IgnoreCaptureGroups = variableNamesOptions.IgnoreCaptureGroups,
    //        IgnoreIdentifierPattern = variableNamesOptions.IgnoreIdentifierPattern,
    //        IgnorePrefixes = variableNamesOptions.IgnorePrefixes,
    //        IgnorePostfixes = variableNamesOptions.IgnorePostfixes
    //    };
    //    var variableNamesResult = _tokenizerValidator.CheckVariableNames(_infixTokens, effectiveVarOpts);
    //    report.VariableNamesResult = variableNamesResult;
    //    if (!variableNamesResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        if (earlyReturnOnErrors) return ValidationReport = report;
    //    }

    //    // 4) Functions (names)
    //    LastValidationState = ParserValidationStage.FunctionNames;
    //    var functionNamesResult = _tokenizerValidator.CheckFunctionNames(_infixTokens, (IFunctionDescriptors)this);
    //    report.FunctionNamesResult = functionNamesResult;
    //    if (!functionNamesResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        if (earlyReturnOnErrors) return ValidationReport = report;
    //    }

    //    // 4.5) Adjacent operands
    //    LastValidationState = ParserValidationStage.AdjacentOperands;
    //    var unexpectedOperatorOperandsResult = _tokenizerValidator.CheckUnexpectedOperatorOperands(_infixTokens);
    //    report.UnexpectedOperatorOperandsResult = unexpectedOperatorOperandsResult;

    //    // If we have any errors so far, ALWAYS return
    //    if (!report.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        return ValidationReport = report;
    //    }

    //    // 5 + 5.1) Build postfix + tree via centralized compile with optimization
    //    try
    //    {
    //        // IMPORTANT: During validation we ALWAYS need a tree for parser-level checks.
    //        // Compile with Full depth regardless of optimizationMode.
    //        ParserCompilationResult result = base.Compile(
    //            _infixTokens,
    //            ParserCompilationOptions.Full,
    //            optimizationMode,
    //            Variables,
    //            variableTypes,
    //            functionReturnTypes,
    //            ambiguousFunctionReturnTypes);

    //        LastValidationState = ParserValidationStage.PostfixTokenization;
    //        _postfixTokens = result.PostfixTokens ?? [];
    //        State = ParserSessionState.TokenizedPostfix;

    //        LastValidationState = ParserValidationStage.TreeBuild;
    //        _tree = result.Tree!;
    //        _nodeDictionary = _tree.NodeDictionary;
    //        State = ParserSessionState.TreeBuilt;
    //    }
    //    catch (ParserCompileException pce)
    //    {
    //        LastValidationState = pce.Stage;
    //        LastException = pce;
    //        report.Exception = pce;
    //        State = ParserSessionState.Invalid;
    //        return ValidationReport = report;
    //    }
    //    catch (Exception ex)
    //    {
    //        LastException = ex;
    //        report.Exception = ex;
    //        State = ParserSessionState.Invalid;
    //        return ValidationReport = report;
    //    }

    //    // Switch to post-validation phase (node-dictionary checks)
    //    if (_tree is not null) State = ParserSessionState.Postvalidating;

    //    // 6) Empty function arguments
    //    LastValidationState = ParserValidationStage.EmptyFunctionArguments;
    //    var emptyFunctionArgumentsResult = _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary);
    //    report.EmptyFunctionArgumentsResult = emptyFunctionArgumentsResult;
    //    if (!emptyFunctionArgumentsResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        if (earlyReturnOnErrors) return ValidationReport = report;
    //    }

    //    // 7) Function arguments count
    //    LastValidationState = ParserValidationStage.FunctionArgumentsCount;
    //    var functionArgumentsCountResult = _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IFunctionDescriptors)this);
    //    report.FunctionArgumentsCountResult = functionArgumentsCountResult;
    //    if (!functionArgumentsCountResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        if (earlyReturnOnErrors) return ValidationReport = report;
    //    }

    //    // 8) Invalid operators
    //    LastValidationState = ParserValidationStage.BinaryOperatorOperands;
    //    var binaryOperatorOperandsResult = _parserValidator.CheckBinaryOperatorOperands(_nodeDictionary);
    //    report.BinaryOperatorOperandsResult = binaryOperatorOperandsResult;
    //    if (!binaryOperatorOperandsResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        if (earlyReturnOnErrors) return ValidationReport = report;
    //    }

    //    LastValidationState = ParserValidationStage.UnaryOperatorOperands;
    //    var unaryOperatorOperandsResult = _parserValidator.CheckUnaryOperatorOperands(_nodeDictionary);
    //    report.UnaryOperatorOperandsResult = unaryOperatorOperandsResult;
    //    if (!unaryOperatorOperandsResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //        if (earlyReturnOnErrors) return ValidationReport = report;
    //    }

    //    LastValidationState = ParserValidationStage.OrphanArgumentSeparators;
    //    var orphanArgumentSeparatorsResult = _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);
    //    report.OrphanArgumentSeparatorsResult = orphanArgumentSeparatorsResult;
    //    if (!orphanArgumentSeparatorsResult.IsSuccess)
    //    {
    //        State = ParserSessionState.Invalid;
    //    }

    //    // Final state
    //    State = report.IsSuccess
    //        ? (optimizationMode != ExpressionOptimizationMode.None ? ParserSessionState.Optimized : ParserSessionState.Validated)
    //        : ParserSessionState.Invalid;

    //    return ValidationReport = report;
    //}

    //// Centralized compile step (no validation path). Builds and optionally optimizes based on mode.
    //private void Compile(
    //    ExpressionOptimizationMode optimizationMode,
    //    Dictionary<string, Type>? variableTypes = null,
    //    Dictionary<string, Type>? functionReturnTypes = null,
    //    Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    //{
    //    State = ParserSessionState.Prevalidating;

    //    try
    //    {
    //        var result = base.Compile(
    //            _expression!,
    //            ParserCompilationOptions.FromOptimizationMode(optimizationMode),
    //            optimizationMode,
    //            Variables,
    //            variableTypes,
    //            functionReturnTypes,
    //            ambiguousFunctionReturnTypes);

    //        // Map to session caches
    //        _infixTokens = result.InfixTokens;
    //        _postfixTokens = result.PostfixTokens ?? [];
    //        _tree = result.Tree;
    //        _nodeDictionary = _tree?.NodeDictionary ?? [];

    //        // Reflect how far we compiled
    //        if (_tree is not null)
    //        {
    //            LastValidationState = ParserValidationStage.TreeBuild;
    //            State = ParserSessionState.TreeBuilt;
    //            State = ParserSessionState.Postvalidating;
    //            if (optimizationMode != ExpressionOptimizationMode.None)
    //                State = ParserSessionState.Optimized;
    //        }
    //        else if (_postfixTokens.Count > 0)
    //        {
    //            LastValidationState = ParserValidationStage.PostfixTokenization;
    //            State = ParserSessionState.TokenizedPostfix;
    //        }
    //        else
    //        {
    //            LastValidationState = ParserValidationStage.InfixTokenization;
    //            State = ParserSessionState.TokenizedInfix;
    //        }
    //    }
    //    catch (ParserCompileException pce)
    //    {
    //        LastValidationState = pce.Stage;
    //        LastException = pce;
    //        State = ParserSessionState.Invalid;
    //        throw;
    //    }
    //    catch (Exception ex)
    //    {
    //        LastException = ex;
    //        State = ParserSessionState.Invalid;
    //        throw;
    //    }
    //}


    #endregion

    #region Public evaluation API

    public virtual OneOf<object?, ParserValidationReport> Evaluate(
        Dictionary<string, object?>? variables = null,
        bool runValidation = false,
        bool optimize = false)
    {
        //ParserValidationReport report = ValidateAndOptimize(
        //    _expression,
        //    variables,
        //    variableNamesOptions: null,
        //    runValidation: runValidation,
        //    earlyReturnOnValidationErrors: false,
        //    optimizationMode: optimizationMode);

        if(runValidation)
        {
            var report = Validate(
                new VariableNamesOptions
                {
                    KnownIdentifierNames = new HashSet<string>(
                        (variables ?? _variables).Keys,
                        _options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
                },
                earlyReturnOnErrors: false);
            if (!report.IsSuccess)
                return report;
        }

        Compile(optimize);
        if(optimize) State = ParserSessionState.Optimized;

        var value = Evaluate();
        State = ParserSessionState.Calculated;
        return value;
    }

    public virtual Type EvaluateType(Dictionary<string, object?>? variables = null)
    {
        //ValidateAndOptimize(
        //    _expression,
        //    variables,
        //    variableNamesOptions: null,
        //    runValidation: false,
        //    earlyReturnOnValidationErrors: false,
        //    optimizationMode: ExpressionOptimizationMode.None);
        Compile(optimize: false);
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

    public ParenthesisCheckResult ValidateParentheses() => CheckParentheses(_expression);
    public List<string> GetVariableNames() => GetVariableNames(_infixTokens);

    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignorePrefixes,
        HashSet<string> ignorePostfixes) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, knownIdentifierNames, ignorePrefixes, ignorePostfixes);

    public VariableNamesCheckResult CheckVariableNames(
       HashSet<string> knownIdentifierNames,
       Regex? ignoreIdentifierPattern = null) =>
       _tokenizerValidator.CheckVariableNames(_infixTokens, knownIdentifierNames, ignoreIdentifierPattern);

    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignoreCaptureGroups) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, knownIdentifierNames, ignoreCaptureGroups);

    public VariableNamesCheckResult CheckVariableNames(VariableNamesOptions variableNameOptions) =>
        _tokenizerValidator.CheckVariableNames(_infixTokens, variableNameOptions);

    #endregion

    #region Parser

    public FunctionNamesCheckResult CheckFunctionNames() =>
        _tokenizerValidator.CheckFunctionNames(_infixTokens, (IFunctionDescriptors)this);

    public UnexpectedOperatorOperandsCheckResult CheckAdjacentOperands() =>
        _tokenizerValidator.CheckUnexpectedOperatorOperands(_infixTokens);

    public InvalidBinaryOperatorsCheckResult CheckBinaryOperators() =>
        _parserValidator.CheckBinaryOperatorOperands(_nodeDictionary);

    public InvalidUnaryOperatorsCheckResult CheckUnaryOperators() =>
        _parserValidator.CheckUnaryOperatorOperands(_nodeDictionary);

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators() =>
        _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount() =>
        _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IFunctionDescriptors)this);

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments() =>
        _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary);

    #endregion

    #endregion

    public virtual ParserValidationReport Validate(
        VariableNamesOptions nameOptions,
        bool earlyReturnOnErrors = false)
    {
        LastValidationState = ParserValidationStage.None;
        LastException = null;
        ValidationReport = null;

        if (string.IsNullOrWhiteSpace(_expression))
        {
            State = ParserSessionState.Validated;
            return ValidationReport = new() { Expression = _expression }; ;
        }

        State = ParserSessionState.Prevalidating;

        // Run tokenizer-level validation (single pass over infix), mirror ParserBase.Validate
        TokenizerValidationReport tokenizerReport = ((Tokenizer)this).Validate(
                _expression,
                nameOptions,
                functionDescriptors: this,
                earlyReturnOnErrors: earlyReturnOnErrors);

        ParserValidationReport report = ParserValidationReport.FromTokenizerReport(tokenizerReport);
        LastValidationState = ParserValidationStage.InfixTokenization;
        _infixTokens = report.InfixTokens ?? [];

        // Always return on tokenizer errors
        if (!tokenizerReport.IsSuccess)
        {
            State = ParserSessionState.Invalid;
            this.LastException = report.Exception = tokenizerReport.Exception;
            return ValidationReport = report;
        }


        List<Token> postfixTokens;

        // Build postfix and tree (NO optimization here)
        try
        {
            LastValidationState = ParserValidationStage.PostfixTokenization;
            postfixTokens = report.PostfixTokens = GetPostfixTokens(_infixTokens);
            _postfixTokens = postfixTokens;
        }
        catch (Exception ex)
        {
            var pce = ParserCompileException.PostfixException(ex);
            LastValidationState = pce.Stage;
            LastException = pce;
            report.Exception = pce;
            State = ParserSessionState.Invalid;
            return ValidationReport = report;
        }

        try
        {
            LastValidationState = ParserValidationStage.TreeBuild;
            _tree = report.Tree = GetExpressionTree(postfixTokens);
            _nodeDictionary = _tree.NodeDictionary;
            report.NodeDictionary = _nodeDictionary;
            State = ParserSessionState.TreeBuilt;
        }
        catch (Exception ex)
        {
            var pce = ParserCompileException.TreeBuildException(ex);
            LastValidationState = pce.Stage;
            LastException = pce;
            report.Exception = pce;
            State = ParserSessionState.Invalid;
            return ValidationReport = report;
        }

        // Parser-level validations (single pass over node dictionary)
        State = ParserSessionState.Postvalidating;
        var postfixReport = _parserValidator.ValidateTreePostfixStage(
            _nodeDictionary,
            this,
            earlyReturnOnErrors);

        report.FunctionArgumentsCountResult = postfixReport.FunctionArgumentsCountResult;
        report.EmptyFunctionArgumentsResult = postfixReport.EmptyFunctionArgumentsResult;
        report.OrphanArgumentSeparatorsResult = postfixReport.OrphanArgumentSeparatorsResult;
        report.BinaryOperatorOperandsResult = postfixReport.BinaryOperatorOperandsResult;
        report.UnaryOperatorOperandsResult = postfixReport.UnaryOperatorOperandsResult;
        this.LastException = report.Exception = postfixReport.Exception;

        State = report.IsSuccess ? ParserSessionState.Validated : ParserSessionState.Invalid;
        return ValidationReport = report;
    }

    // Simplified, incremental compile. Builds infix/postfix if missing.
    // Builds tree and optimizes only when optimizationMode == ParserInference.
    public TreeOptimizerResult Compile(bool optimize)
    {
        if (string.IsNullOrWhiteSpace(_expression))
            return TreeOptimizerResult.Unchanged(TokenTree.Empty);

        // occurs after validation only (validation forces the tree build)
        if (!optimize && _tree is not null)
            return TreeOptimizerResult.Unchanged(_tree);

        LastValidationState = ParserValidationStage.None;
        LastException = null;

        // Infix
        if (_infixTokens.Count == 0)
        {
            LastValidationState = ParserValidationStage.InfixTokenization;
            try
            {
                _infixTokens = GetInfixTokens(_expression);
                State = ParserSessionState.TokenizedInfix;
            }
            catch (Exception ex)
            {
                var iex = new InvalidOperationException("Could not tokenize (get infix tokens).", ex);
                LastException = iex;
                State = ParserSessionState.Invalid;
                throw iex;
            }
        }

        // Postfix
        if (_postfixTokens.Count == 0)
        {
            LastValidationState = ParserValidationStage.PostfixTokenization;
            try
            {
                _postfixTokens = GetPostfixTokens(_infixTokens);
                State = ParserSessionState.TokenizedPostfix;
            }
            catch (Exception ex)
            {
                var pce = ParserCompileException.PostfixException(ex);
                LastException = pce;
                State = ParserSessionState.Invalid;
                throw pce;
            }
        }

        if (!optimize) // we have already the postfix tokens, no need to build the tree
            return TreeOptimizerResult.Unchanged(TokenTree.Empty);

        // Build tree if missing
        if (_tree is null)
        {
            LastValidationState = ParserValidationStage.TreeBuild;
            try
            {
                _tree = GetExpressionTree(_postfixTokens);
                _nodeDictionary = _tree.NodeDictionary;
                State = ParserSessionState.TreeBuilt;
            }
            catch (Exception ex)
            {
                var pce = ParserCompileException.TreeBuildException(ex);
                LastException = pce;
                State = ParserSessionState.Invalid;
                throw pce;
            }
        }

        // Optimize here using parser inference only in this context
        try
        {
            LastValidationState = ParserValidationStage.TreeOptimize;
            TreeOptimizerResult result = GetOptimizedTree(_tree!, Variables);
            _tree = result.Tree;
            _infixTokens = _tree.GetInfixTokens();
            _postfixTokens = _tree.GetPostfixTokens();
            _nodeDictionary = _tree.NodeDictionary;
            State = ParserSessionState.Optimized;
            return result;
        }
        catch (Exception ex)
        {
            var pce = ParserCompileException.TreeOptimizeException(ex);
            LastException = pce;
            State = ParserSessionState.Invalid;
            throw;
        }
    }


}
