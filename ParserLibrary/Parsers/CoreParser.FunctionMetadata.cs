using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers;

public partial class CoreParser : IParserFunctionMetadata
{
    int? IParserFunctionMetadata.GetCustomFunctionFixedArgCount(string functionName)
    {
        if (CustomFunctions.TryGetValue(functionName, out var def)) return def.Parameters.Length;
        return null;
    }

    int? IParserFunctionMetadata.GetMainFunctionFixedArgCount(string functionName)
    {
        return MainFunctionsArgumentsCount.TryGetValue(functionName, out var n) ? n : null;
    }

    int? IParserFunctionMetadata.GetMainFunctionMinVariableArgCount(string functionName)
    {
        return MainFunctionsMinVariableArgumentsCount.TryGetValue(functionName, out var n) ? n : null;
    }
}