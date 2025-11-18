using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers;

public partial class ParserSessionBase : IFunctionDescriptors
{
    ////useful for validation purposes 
    //byte? IFunctionDescriptors.GetCustomFunctionFixedArgCount(string functionName)
    //{
    //    if (CustomFunctions.TryGetValue(functionName, out var def)) return (byte)def.Parameters.Length;
    //    return null;
    //}

    //byte? IFunctionDescriptors.GetMainFunctionFixedArgCount(string functionName) =>
    //    MainFunctionsWithFixedArgumentsCount.TryGetValue(functionName, out var n) ? n : null;

    //byte? IFunctionDescriptors.GetMainFunctionMinVariableArgCount(string functionName) =>
    //    MainFunctionsMinVariableArgumentsCount.TryGetValue(functionName, out var n) ? n : null;

    //(byte, byte)? IFunctionDescriptors.GetMainFunctionMinMaxVariableArgCount(string functionName) =>
    //    MainFunctionsWithVariableArgumentsCount.TryGetValue(functionName, out var n) ? n : null;



    #region FunctionInformation metadata

    //alternative implementation of validation using FunctionInformation metadata

    //bool IFunctionDescriptors.IsKnownFunctionWithFixedArgsCount(string functionName) =>
    //    GetFunctionInformation(functionName) is { FixedArgumentsCount: > 0 };

    //byte? IFunctionDescriptors.GetFunctionFixedArgCount(string functionName) =>
    //    GetFunctionInformation(functionName)?.FixedArgumentsCount;

    //bool IFunctionDescriptors.IsKnownFunctionWithVariableArgsCount(string functionName) =>
    //    GetFunctionInformation(functionName) is { MinArgumentsCount: > 0 };

    //(byte, byte)? IFunctionDescriptors.GetFunctionMinMaxVariableArgCount(string functionName)
    //{
    //    var f = GetFunctionInformation(functionName);
    //    if (f is null) return null;
    //    if (f.Value.FixedArgumentsCount > 0) return null; //not a variable args function 
    //    if (f.Value.MinArgumentsCount is null || f.Value.MaxArgumentsCount is null) return null;
    //    return (f.Value.MinArgumentsCount!.Value, f.Value.MaxArgumentsCount!.Value);
    //}

    //bool IFunctionDescriptors.IsKnownFunction(string functionName)
    //{
    //    var commonFunction = GetFunctionInformation(functionName);
    //    return commonFunction is not null;
    //}

    //bool IFunctionDescriptors.IsCustomFunction(string functionName) =>
    //    CustomFunctions.ContainsKey(functionName);

    bool IFunctionDescriptors.IsUnknownFunction(string functionName)
    {
        if(CustomFunctions.ContainsKey(functionName)) return true;
        if (FunctionCatalog is null) return false;

        return FunctionCatalog.Get(functionName) is not null;
    }

    #endregion

}