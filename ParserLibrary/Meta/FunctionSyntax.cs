namespace ParserLibrary.Meta;


public readonly struct InputsDynamic
{
    //should be used only if different from InputsDynamic
    public Type? FirstInputType { get; init; }
    //if first inputtype present, then use for >=1, else >=0
    //if last inputtype present, then use until n-2 else until n-1
    public HashSet<Type> MiddleInputTypes { get; init; }

    //should be used only if different from InputsDynamic
    public Type? LastInputType { get; init; }
    public byte MinVariableArgumentsCount { get; init; }

}

public readonly struct FunctionSyntax
{
    public int? Scenario { get; init; }

    public string? Expression { get; init; } //useful for custom functions only


    //should be initialized to EMPTY array if no inputs at all
    public List<Type>? InputsFixed { get; init; }

    public bool IsEmpty => (InputsFixed is not null || InputsFixed!.Count == 0) && InputsDynamic is null;

    public InputsDynamic? InputsDynamic { get; init; }

    public Type OutputType { get; init; }

    public string? Example { get; init; }
    public string? Description { get; init; }

    public static FunctionSyntax CreateEmpty(Type outputType, int? scenarioId, string? example = null, string? description = null)
    {
        return new FunctionSyntax
        {
            Scenario = scenarioId,
            InputsFixed = [],
            OutputType = outputType,
            Example = example,
            Description = description
        };
    }

    public static FunctionSyntax CreateFixed(List<Type> inputTypes, Type outputType, int? scenarioId, string? example = null, string? description = null)
    {
        return new FunctionSyntax
        {
            Scenario = scenarioId,
            InputsFixed = inputTypes,
            OutputType = outputType,
            Example = example,
            Description = description
        };
    }

    public static FunctionSyntax CreateVariable(
        byte minVariableArgsCount,
        Type? firstInputType,
        HashSet<Type> middleInputTypes, //at list one middle input type or else it is not variable
        Type? lastInputType,
        Type outputType,
        string? example = null,
        string? description = null,
        int? scenarioId = null)
    {
        return new FunctionSyntax
        {
            Scenario = scenarioId,
            InputsDynamic = new InputsDynamic
            {
                FirstInputType = firstInputType,
                MiddleInputTypes = middleInputTypes,
                LastInputType = lastInputType,
                MinVariableArgumentsCount = minVariableArgsCount
            },
            OutputType = outputType,
            Example = example,
            Description = description
        };
    }

}
