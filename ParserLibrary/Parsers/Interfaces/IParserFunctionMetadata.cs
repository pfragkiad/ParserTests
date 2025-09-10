namespace ParserLibrary.Parsers.Interfaces;

public interface IParserFunctionMetadata
{
    int? GetCustomFunctionFixedArgCount(string functionName);
    int? GetMainFunctionFixedArgCount(string functionName);
    int? GetMainFunctionMinVariableArgCount(string functionName);
}