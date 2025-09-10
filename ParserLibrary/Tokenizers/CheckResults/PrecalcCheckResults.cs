namespace ParserLibrary.Tokenizers.CheckResults;

public readonly struct PrecalcCheckResults
{
    public ParenthesisErrorCheckResult ParenthesisCheckResult { get; init; }

    public FunctionNamesCheckResult? FunctionNamesCheckResult { get; init; }

    public VariableNamesCheckResult? VariableNamesCheckResult { get; init; }


    //---
    public InvalidOperatorsCheckResult? InvalidOperatorsCheckResult { get; init; }

    public InvalidArgumentSeparatorsCheckResult? InvalidArgumentSeparatorsCheckResult { get; init; }

    public FunctionArgumentsCountCheckResult? FunctionArgumentsCountCheckResult { get; init; }

    public EmptyFunctionArgumentsCheckResult? EmptyFunctionArgumentsCheckResult { get;init; }

}
