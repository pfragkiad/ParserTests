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
    public string Expression
    {
        get => _expression;
        set
        {
            Reset(); //always reset on expression change
            _expression = value;
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
                return report;
        }
        else
        {
            // Build infix/postfix/tree/nodeDictionary once without validation
            _infixTokens = GetInfixTokens(_expression!);
            _postfixTokens = GetPostfixTokens(_infixTokens);
            _tree = GetExpressionTree(_postfixTokens);
            _nodeDictionary = _tree.NodeDictionary;
        }

        // Optimization (optional)
        if (optimizationMode != ExpressionOptimizationMode.None)
            _ = Optimize(optimizationMode, variableTypes, functionReturnTypes, ambiguousFunctionReturnTypes);

        return report;
    }

    // Optimization only — public API
    public TreeOptimizerResult Optimize(
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
                    _infixTokens = GetInfixTokens(_expression!);
                _postfixTokens = GetPostfixTokens(_infixTokens);
            }
            _tree = GetExpressionTree(_postfixTokens);
            _nodeDictionary = _tree.NodeDictionary;
        }

        switch (optimizationMode)
        {
            case ExpressionOptimizationMode.None:
                return TreeOptimizerResult.Unchanged(_tree!);

              default:
          case ExpressionOptimizationMode.ParserInference:
            {
                TreeOptimizerResult result = OptimizeTreeUsingInference(_tree!, Variables);
                var optimizedTree = result.Tree;
                _tree = optimizedTree;
                _infixTokens = optimizedTree.GetInfixTokens();
                _postfixTokens = optimizedTree.GetPostfixTokens();
                _nodeDictionary = optimizedTree.NodeDictionary;
                return result;
            }

            case ExpressionOptimizationMode.StaticTypeMaps:
            {
                variableTypes ??= BuildVariableTypesFromVariables(Variables);
                var result = GetOptimizedExpressionTreeResult(
                    _expression!,
                    variableTypes,
                    functionReturnTypes,
                    ambiguousFunctionReturnTypes);

                var optimizedTree = result.Tree;
                _tree = optimizedTree;
                _infixTokens = optimizedTree.GetInfixTokens();
                _postfixTokens = optimizedTree.GetPostfixTokens();
                _nodeDictionary = optimizedTree.NodeDictionary;
                return result;
            }

        }
    }

    #endregion

    #region Public evaluation API

    public override object? Evaluate(string expression, Dictionary<string, object?>? variables = null)
    {
        ValidateAndOptimize(
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
        ValidateAndOptimize(
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
        ValidateAndOptimize(
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
        ValidateAndOptimize(
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
        ValidateAndOptimize(
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
        ValidateAndOptimize(
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
            return report;

        // 1) Parentheses
        var parenthesesResult = _tokenizerValidator.CheckParentheses(_expression);
        report.ParenthesesResult = parenthesesResult;
        if (!parenthesesResult.IsSuccess && earlyReturnOnErrors) return report;

        // 2) Infix
        var infixTokens = _infixTokens.Count != 0 ? _infixTokens : GetInfixTokens(_expression);
        if (_infixTokens.Count == 0) _infixTokens = infixTokens;

        // 3) Variables
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
        if (!variableNamesResult.IsSuccess && earlyReturnOnErrors) return report;

        // 4) Functions (names)
        var functionNamesResult = _parserValidator.CheckFunctionNames(infixTokens, (IParserFunctionMetadata)this);
        report.FunctionNamesResult = functionNamesResult;
        if (!functionNamesResult.IsSuccess && earlyReturnOnErrors) return report;

        // 4.5) NEW: Adjacent operands (avoid building invalid postfix/tree)
        var adjacentOperandsResult = _tokenizerValidator.CheckAdjacentOperands(infixTokens);
        report.AdjacentOperandsResult = adjacentOperandsResult;
        if (!adjacentOperandsResult.IsSuccess) return report;

        // 5+) Build postfix/tree and continue with parser checks
        var postfixTokens = _postfixTokens.Count != 0 ? _postfixTokens : GetPostfixTokens(infixTokens);
        if (_postfixTokens.Count == 0) _postfixTokens = postfixTokens;

        _tree = GetExpressionTree(postfixTokens);
        _nodeDictionary = _tree.NodeDictionary;

        // 6) Empty function arguments
        var emptyFunctionArgumentsResult = _parserValidator.CheckEmptyFunctionArguments(_nodeDictionary);
        report.EmptyFunctionArgumentsResult = emptyFunctionArgumentsResult;
        if (!emptyFunctionArgumentsResult.IsSuccess && earlyReturnOnErrors) return report;

        // 7) Function arguments count
        var functionArgumentsCountResult = _parserValidator.CheckFunctionArgumentsCount(_nodeDictionary, (IParserFunctionMetadata)this);
        report.FunctionArgumentsCountResult = functionArgumentsCountResult;
        if (!functionArgumentsCountResult.IsSuccess && earlyReturnOnErrors) return report;

        // 8) Invalid operators
        var binaryOperatorOperandsResult = _parserValidator.CheckBinaryOperatorOperands(_nodeDictionary);
        report.BinaryOperatorOperandsResult = binaryOperatorOperandsResult;
        if (!binaryOperatorOperandsResult.IsSuccess && earlyReturnOnErrors) return report;

        var unaryOperatorOperandsResult = _parserValidator.CheckUnaryOperatorOperands(_nodeDictionary);
        report.UnaryOperatorOperandsResult = unaryOperatorOperandsResult;
        if (!unaryOperatorOperandsResult.IsSuccess && earlyReturnOnErrors) return report;

        var orphanArgumentSeparatorsResult = _parserValidator.CheckOrphanArgumentSeparators(_nodeDictionary);
        report.OrphanArgumentSeparatorsResult = orphanArgumentSeparatorsResult;

        return report;
    }

    #endregion
}
