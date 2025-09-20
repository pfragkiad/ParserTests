using FluentValidation.Results;
using ParserLibrary.Parsers.Validation.CheckResults;

namespace ParserLibrary.Parsers.Validation.Reports;

public sealed class ParserValidationReport : TokenizerValidationReport
{

    // Parser-level checks (optional)
    public FunctionArgumentsCountCheckResult? FunctionArgumentsCountResult { get; set; }

    public EmptyFunctionArgumentsCheckResult? EmptyFunctionArgumentsResult { get; set; }

    public InvalidArgumentSeparatorsCheckResult? OrphanArgumentSeparatorsResult { get; set; }

    public InvalidOperatorsCheckResult? BinaryOperatorOperandsResult { get; set; }

    public InvalidUnaryOperatorsCheckResult? UnaryOperatorOperandsResult { get; set; }

    public List<Token>? PostfixTokens { get; set; }

    public TokenTree? Tree { get; set; }

    public Dictionary<Token, Node<Token>>? NodeDictionary { get; set; }

    public override bool IsSuccess =>
        base.IsSuccess &&
        (FunctionArgumentsCountResult?.IsSuccess ?? true) &&
        (EmptyFunctionArgumentsResult?.IsSuccess ?? true) &&
        (OrphanArgumentSeparatorsResult?.IsSuccess ?? true) &&
        (BinaryOperatorOperandsResult?.IsSuccess ?? true) &&
        (UnaryOperatorOperandsResult?.IsSuccess ?? true)
        ;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];


        var failures = new List<ValidationFailure>(capacity: 16);
        var baseFailures = base.GetValidationFailures();
        if (baseFailures.Count > 0) failures.AddRange(baseFailures);

        if (FunctionArgumentsCountResult is { IsSuccess: false })
            failures.AddRange(FunctionArgumentsCountResult.GetValidationFailures());

        if (EmptyFunctionArgumentsResult is { IsSuccess: false })
            failures.AddRange(EmptyFunctionArgumentsResult.GetValidationFailures());

        if (OrphanArgumentSeparatorsResult is { IsSuccess: false })
            failures.AddRange(OrphanArgumentSeparatorsResult.GetValidationFailures());

        if (BinaryOperatorOperandsResult is { IsSuccess: false })
            failures.AddRange(BinaryOperatorOperandsResult.GetValidationFailures());

        if (UnaryOperatorOperandsResult is { IsSuccess: false })
            failures.AddRange(UnaryOperatorOperandsResult.GetValidationFailures());


        return failures;
    }

    public static ParserValidationReport FromTokenizerReport(TokenizerValidationReport report) =>
        new()
        {
            Expression = report.Expression,
            InfixTokens = report.InfixTokens,

            ParenthesesResult = report.ParenthesesResult,
            VariableNamesResult = report.VariableNamesResult,
            FunctionNamesResult = report.FunctionNamesResult,
            UnexpectedOperatorOperandsResult = report.UnexpectedOperatorOperandsResult,
         
            Exception = report.Exception
        };
}