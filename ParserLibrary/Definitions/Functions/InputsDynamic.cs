namespace ParserLibrary.Definitions.Functions;

public readonly struct InputsDynamic
{
    //should be used only if different from InputsDynamic
    public HashSet<Type>? FirstInputType { get; init; }
    //if first inputtype present, then use for >=1, else >=0
    //if last inputtype present, then use until n-2 else until n-1
    public HashSet<Type> MiddleInputTypes { get; init; }

    //should be used only if different from InputsDynamic
    public HashSet<Type>? LastInputType { get; init; }
    public byte MinMiddleArgumentsCount { get; init; }

}
