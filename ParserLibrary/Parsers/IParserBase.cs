namespace ParserLibrary.Parsers;

public interface IParserBase
{
    //ITokenizer Tokenizer { get; }

    FunctionNamesCheckResult CheckFunctionNames(string expression);
    List<string> GetMatchedFunctionNames(string expression);
    void RegisterFunction(string definition);
}