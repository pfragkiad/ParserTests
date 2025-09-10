using FluentValidation.Results;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;

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
            return new ParenthesisErrorCheckResult { UnmatchedClosed = [], UnmatchedOpen = [] };

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

        //stage 1 check parentheses (2 variants)
        if (!AreParenthesesMatched()) //fastest check
        {
            //second version for demo purposes (if explicit output is needed) (less fast check - without stack/binary tree)
            var parenthesisCheckResult = CheckParentheses();

            _logger.LogWarning("Unmatched parentheses in formula: {formula}", _expression);
            //get validation failures (one per unmatched parenthesis)
            failures.AddRange(parenthesisCheckResult.GetValidationFailures());
        }

        //unmatched parentheses will crash postfix checks
        bool cannotContinueOtherChecks = failures.Count > 0;

        //stage 2 check function names
        var checkFunctionNamesResult = CheckFunctionNames();
        if (!checkFunctionNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function names in formula: {formula}", _expression);
            //get validation failures (one per unmatched function name)
            failures.AddRange(checkFunctionNamesResult.GetValidationFailures());
        }

        //stage 3 check identifier names (timeseries names ONLY are expected to be within brackets so they are ignored)
        var checkNamesResult = CheckVariableNames([.. _variables.Keys],
            ignoreCaptureGroups: ignoreIdentifierCaptureGroups ?? []);

        if (!checkNamesResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched identifiers in formula: {formula}", _expression);
            //get validation failures (one per unmatched identifier name)
            failures.AddRange(checkNamesResult.GetValidationFailures());
        }

        //if there are errors here we have to exit early 
        if (cannotContinueOtherChecks)
            return failures;
        //throw new ValidationException("Errors in formula validation.", failures);

        //stage 4 check invalid operators (needs postfix tokens)
        var checkOperatorsResult = CheckOperators();
        if (!checkOperatorsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid operators in formula: {formula}", _expression);
            //get validation failures (one per invalid operator)
            failures.AddRange(checkOperatorsResult.GetValidationFailures());
        }

        var checkArgumentsResult = CheckOrphanArgumentSeparators();
        if (!checkArgumentsResult.IsSuccess)
        {
            _logger.LogWarning("Invalid argument separators in formula: {formula}", _expression);
            //get validation failures (one per invalid argument separator)
            failures.AddRange(checkArgumentsResult.GetValidationFailures());
        }


        //we have to check for null argumentts before checking arguments count

        //stage 5 check function arguments count 
        var checkFunctionArgumentsResult = CheckFunctionArgumentsCount();
        //check for function arguments count! before
        if (!checkFunctionArgumentsResult.IsSuccess)
        {
            _logger.LogWarning("Unmatched function arguments in formula: {formula}", _expression);
            //get validation failures (one per unmatched function argument)
            failures.AddRange(checkFunctionArgumentsResult.GetValidationFailures());
        }

        //stage 6 check empty function arguments (used if empty parameters are NOT allowed)
        var emptyArgumentsRsult = CheckEmptyFunctionArguments();
        if (!emptyArgumentsRsult.IsSuccess)
        {
            _logger.LogWarning("Empty function arguments in formula: {formula}", _expression);
            //get validation failures (one per empty function argument)
            failures.AddRange(emptyArgumentsRsult.GetValidationFailures());
        }


        //failures.AddRange(CheckParentheses().GetValidationFailures());
        //failures.AddRange(CheckVariableNames([.. _variables.Keys]).GetValidationFailures());
        //failures.AddRange(CheckFunctionNames().GetValidationFailures());
        //failures.AddRange(CheckOperators().GetValidationFailures());
        //failures.AddRange(CheckOrphanArgumentSeparators().GetValidationFailures());
        //failures.AddRange(CheckFunctionArgumentsCount().GetValidationFailures());
        //failures.AddRange(CheckEmptyFunctionArguments().GetValidationFailures());
        return failures;
    }


    #endregion


}
