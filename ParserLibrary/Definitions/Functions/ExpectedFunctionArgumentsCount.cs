namespace ParserLibrary.Definitions.Functions;

public class ExpectedFunctionArgumentsCount
{
    public IList<int>? FixedCounts { get; init; } = [];
    public int? MinCount { get; init; }
}