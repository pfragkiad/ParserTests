using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers;

public sealed class ParserServices
{
    public required IOptions<TokenizerOptions> Options { get; init; }
    public required ITokenizerValidator TokenizerValidator { get; init; }
    public required IParserValidator ParserValidator { get; init; }

    public TokenizerOptions TokenizerOptions => Options.Value;
}