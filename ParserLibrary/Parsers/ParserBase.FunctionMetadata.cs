using ParserLibrary.Parsers.Interfaces;
using System.Reflection.Metadata.Ecma335;

namespace ParserLibrary.Parsers;

public partial class ParserBase : IFunctionDescriptors
{
    //useful for validation purposes 
    byte? IFunctionDescriptors.GetCustomFunctionFixedArgCount(string functionName)
    {
        if (CustomFunctions.TryGetValue(functionName, out var def)) return (byte)def.Parameters.Length;
        return null;
    }

    byte? IFunctionDescriptors.GetMainFunctionFixedArgCount(string functionName) =>
        MainFunctionsWithFixedArgumentsCount.TryGetValue(functionName, out var n) ? n : null;

    byte? IFunctionDescriptors.GetMainFunctionMinVariableArgCount(string functionName) =>
        MainFunctionsMinVariableArgumentsCount.TryGetValue(functionName, out var n) ? n : null;

    (byte, byte)? IFunctionDescriptors.GetMainFunctionMinMaxVariableArgCount(string functionName) =>
        MainFunctionsWithVariableArgumentsCount.TryGetValue(functionName, out var n) ? n : null;



    #region FunctionInformation metadata

    //alternative implementation of validation using FunctionInformation metadata

    bool IFunctionDescriptors.IsKnownFunctionWithFixedArgsCount(string functionName) =>
        GetFunctionInformation(functionName) is { FixedArgumentsCount: > 0 };

    byte? IFunctionDescriptors.GetFunctionFixedArgCount(string functionName) =>
        GetFunctionInformation(functionName)?.FixedArgumentsCount;

    bool IFunctionDescriptors.IsKnownFunctionWithVariableArgsCount(string functionName) =>
        GetFunctionInformation(functionName) is { MinArgumentsCount: > 0 };

    (byte, byte)? IFunctionDescriptors.GetFunctionMinMaxVariableArgCount(string functionName)
    {
        var f = GetFunctionInformation(functionName);
        if (f is null) return null;
        if (f.FixedArgumentsCount > 0) return null; //not a variable args function 
        if (f.MinArgumentsCount is null || f.MaxArgumentsCount is null) return null;
        return (f.MinArgumentsCount!.Value, f.MaxArgumentsCount!.Value);
    }

    IList<byte>? IFunctionDescriptors.GetFunctionSyntaxesFixedArgCount(string functionName)
    {
        var f = GetFunctionInformation(functionName);
        if (f is null || f.Syntaxes is null || f.Syntaxes.Count == 0) return null;

        var result = new List<byte>();
        foreach (var syn in f.Syntaxes)
        {
            if (syn.IsEmpty)
                result.Add(0);
            else if (syn.InputsFixed is not null)
                result.Add((byte)syn.InputsFixed.Count);
            //else if (syn.InputsDynamic is not null)
            //    result.Add((byte)syn.InputsDynamic!.Value.MinVariableArgumentsCount);
        }
        return [.. result.Distinct().OrderBy(n => n)];
    }

    byte? IFunctionDescriptors.GetFunctionSyntaxesMinVariableArgCount(string functionName)
    {
        var f = GetFunctionInformation(functionName);
        if (f is null || f.Syntaxes is null || f.Syntaxes.Count == 0) return null;

        var result = new List<byte>();
        if (f is null || f.Syntaxes is null) return null;
        foreach (var syn in f.Syntaxes)
        {
            if (syn.InputsDynamic is not null)
                result.Add((byte)syn.InputsDynamic!.Value.MinVariableArgumentsCount);
        }
        return result.Count > 0 ? result.Min() : null;
    }

    bool IFunctionDescriptors.IsKnownFunction(string functionName)
    {
        var commonFunction = GetFunctionInformation(functionName);
        return commonFunction is not null;
    }
    bool IFunctionDescriptors.IsUnknownFunction(string functionName)
    {
        if (CustomFunctions.ContainsKey(functionName)) return false;
        if (FunctionCatalog is null) return false;
        var commonFunction = GetFunctionInformation(functionName);
        return commonFunction is null;
    }


    #endregion


}