namespace ParserLibrary.Parsers.Interfaces;

public interface IFunctionDescriptors
{
    int? GetCustomFunctionFixedArgCount(string functionName);
    int? GetMainFunctionFixedArgCount(string functionName);
    int? GetMainFunctionMinVariableArgCount(string functionName);
}