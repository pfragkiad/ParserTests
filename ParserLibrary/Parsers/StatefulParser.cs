using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using ParserLibrary.Tokenizers;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers;

/// <summary>
/// This class can be use for a single evaluation, not for parallel evaluations, because the nodeValueDictionary and stack fields keep the state of the currently evaluated expression.
/// </summary>
public class StatefulParser : Parser, IStatefulParser
{

    //created for simplifying and caching dictionaries
    protected internal Dictionary<Node<Token>, object?> _nodeValueDictionary = [];
    protected Dictionary<Token, Node<Token>> _nodeDictionary = [];
    protected Stack<Token> _stack = [];

    protected List<Token> _infixTokens = [];
    protected List<Token> _postfixTokens = [];

    public StatefulParser(ILogger<StatefulParser> logger, IOptions<TokenizerOptions> options,
        string? expression = null,
        Dictionary<string,object?>? variables = null)
        : base(logger, options)
    {
        //assign expression if not null or whitespace
        if (!string.IsNullOrWhiteSpace(expression))
            Expression = expression;

        Variables = variables ?? [];
    }

    //protected TransientParser(ILogger logger, IOptions<TokenizerOptions> options, string expression)
    //: base(logger, options)
    //{ 

    //}

    #region 


    #region Expression fields


    private string? _expression;
    public string? Expression
    {
        get => _expression;
        set
        {
            Reset();

            _expression = value;
            if (string.IsNullOrWhiteSpace(_expression)) return;

            //parses all tokens
            _infixTokens = GetInfixTokens(value!);
            _postfixTokens = GetPostfixTokens(_infixTokens);
        }
    }


    protected Dictionary<string, object?> _variables = [];
    public Dictionary<string, object?> Variables
    {
        get => _variables;
        set
        {
            _variables = value ?? [];
            _variables = MergeVariableConstants(_variables);
        }
    }


    #endregion


    protected void Reset()
    {
        _infixTokens = [];
        _postfixTokens = [];

        _nodeValueDictionary = [];
        _nodeDictionary = [];
        _stack = [];
        _variables = [];
    }

    //also resets the internal expression
    public override object? Evaluate(string expression, Dictionary<string, object?>? variables = null)
    {
        Expression = expression;
        Variables = variables ?? [];
        return Evaluate();
    }

    public virtual object? Evaluate(Dictionary<string, object?>? variables = null)
    {
        Variables = variables ?? [];
        return Evaluate();
    }

    //also resets the internal expression

    public override Type EvaluateType(string expression, Dictionary<string, object?>? variables = null)
    {
        Expression = expression;
        Variables = variables ?? [];

        return EvaluateType();
    }

    public virtual Type EvaluateType(Dictionary<string, object?>? variables = null)
    {
        Variables = variables ?? [];
        return EvaluateType();
    }


    public object? Evaluate() =>
        Evaluate(_postfixTokens, _variables, _stack, _nodeDictionary, _nodeValueDictionary, mergeConstants:false);

    public Type EvaluateType() =>
        EvaluateType(_postfixTokens, _variables, _stack, _nodeDictionary, _nodeValueDictionary, mergeConstants:false);


    #endregion

    #region Validation checks

    #region Tokenizer 
    public bool AreParenthesesMatched() =>
        Expression is null || AreParenthesesMatched(Expression!);

    public ParenthesisCheckResult CheckParentheses() =>
        CheckParentheses(_infixTokens);

    public List<string> GetVariableNames() =>
        GetVariableNames(_infixTokens);

    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> identifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes) =>
        CheckVariableNames(_infixTokens, identifierNames, ignorePrefixes, ignorePostfixes);

    public VariableNamesCheckResult CheckVariableNames(
       HashSet<string> identifierNames,
       Regex? ignoreIdentifierPattern = null) =>
       CheckVariableNames(_infixTokens, identifierNames, ignoreIdentifierPattern);


    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> identifierNames,
        string[] ignoreCaptureGroups) =>
        CheckVariableNames(_infixTokens, identifierNames, ignoreCaptureGroups);
    #endregion

    #region Parser

    public FunctionNamesCheckResult CheckFunctionNames() =>
        CheckFunctionNames(_infixTokens);

    public List<string> GetMatchedFunctionNames() =>
        GetMatchedFunctionNames(_infixTokens);


    public InvalidOperatorsCheckResult CheckOperators() =>
      CheckOperators(_nodeDictionary);

    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators() =>
        CheckOrphanArgumentSeparators(_nodeDictionary);

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount() =>
          CheckFunctionArgumentsCount(_nodeDictionary);
    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments() =>
        CheckEmptyFunctionArguments(_nodeDictionary);


    #endregion

    public List<ValidationFailure> GetValidationFailures(string[] ignoreCaptureGroups)
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
            ignoreCaptureGroups: ignoreCaptureGroups);

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
