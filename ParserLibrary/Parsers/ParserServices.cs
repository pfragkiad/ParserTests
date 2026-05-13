using ParserLibrary.Definitions;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers;

public sealed class ParserServices
{
    public required IOptions<TokenizerOptions> Options { get; init; }
    public required ITokenizerValidator TokenizerValidator { get; init; }
    public required IParserValidator ParserValidator { get; init; }
    public required LambdaExpressionFactory LambdaExpressionFactory { get; init; }

    public TokenizerOptions TokenizerOptions => Options.Value;

   //public IServiceProvider Services { get; init; }
}