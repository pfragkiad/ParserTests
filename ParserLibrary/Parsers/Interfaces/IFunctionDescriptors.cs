namespace ParserLibrary.Parsers.Interfaces;

public interface IFunctionDescriptors
{
    byte? GetCustomFunctionFixedArgCount(string functionName);
    byte? GetMainFunctionFixedArgCount(string functionName);
    byte? GetMainFunctionMinVariableArgCount(string functionName);

    (byte, byte)? GetMainFunctionMinMaxVariableArgCount(string functionName);


    //new implementation using FunctionInformation metadata
    bool IsKnownFunctionWithFixedArgsCount(string functionName);

    byte? GetFunctionFixedArgCount(string functionName);


    bool IsKnownFunctionWithVariableArgsCount(string functionName);

    (byte, byte)? GetFunctionMinMaxVariableArgCount(string functionName);

    bool IsKnownFunction(string functionName);

    bool IsUnknownFunction(string functionName);


    //--------------------------------
}