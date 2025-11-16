namespace ParserLibrary.Parsers.Interfaces;

public interface IFunctionDescriptors
{
    byte? GetCustomFunctionFixedArgCount(string functionName);

    #region Legacy main function descriptors (to be removed later)

    byte? GetMainFunctionFixedArgCount(string functionName);
    
    byte? GetMainFunctionMinVariableArgCount(string functionName);

    (byte, byte)? GetMainFunctionMinMaxVariableArgCount(string functionName);

    #endregion


    ////new implementation using FunctionInformation metadata
    //bool IsKnownFunctionWithFixedArgsCount(string functionName);

    //byte? GetFunctionFixedArgCount(string functionName);


    //bool IsKnownFunctionWithVariableArgsCount(string functionName);

    //(byte, byte)? GetFunctionMinMaxVariableArgCount(string functionName);

    IList<byte>? GetFunctionSyntaxesFixedArgCount(string functionName);

    byte? GetFunctionSyntaxesMinVariableArgCount(string functionName);

    bool IsKnownFunction(string functionName);

    bool IsUnknownFunction(string functionName);


    //--------------------------------
}