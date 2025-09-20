using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers;

public partial class ParserBase : IFunctionDescriptors
{
    int? IFunctionDescriptors.GetCustomFunctionFixedArgCount(string functionName)
    {
        if (CustomFunctions.TryGetValue(functionName, out var def)) return def.Parameters.Length;
        return null;
    }

    int? IFunctionDescriptors.GetMainFunctionFixedArgCount(string functionName)
    {
        return MainFunctionsArgumentsCount.TryGetValue(functionName, out var n) ? n : null;
    }

    int? IFunctionDescriptors.GetMainFunctionMinVariableArgCount(string functionName)
    {
        return MainFunctionsMinVariableArgumentsCount.TryGetValue(functionName, out var n) ? n : null;
    }
}