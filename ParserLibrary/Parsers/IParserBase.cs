using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers;

public interface IParserBase : ITokenizer
{
    FunctionNamesCheckResult CheckFunctionNames(string expression);
    List<string> GetMatchedFunctionNames(string expression);
    void RegisterFunction(string definition);
}