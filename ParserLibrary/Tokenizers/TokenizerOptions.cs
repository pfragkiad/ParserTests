using System.Text.Json;

namespace ParserLibrary.Tokenizers;

public class TokenizerOptions
{

    public const string TokenizerSection = "tokenizer";

    public string? Version { get; set; }

    public bool CaseSensitive { get; set; } = false;

#nullable disable
    public TokenPatterns TokenPatterns { get; set; }
#nullable restore





    //private readonly static JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
    //public static TokenizerOptions Default =>
    //    JsonSerializer.Deserialize<TokenizerOptions>("""
    //        {
    //            "version":"1.0",
    //            "caseSensitive":false,
    //            "tokenPatterns":
    //            {
    //                "identifier":"[A-Za-z_]\\w*",
    //                "literal":"\\b(?:\\d+(?:\\.\\d*)?|\\.\\d+)\\b",
    //                "openParenthesis":"(",
    //                "closeParenthesis":")",
    //                "argumentSeparator":",",
    //                "unary":
    //                    [
    //                    {"name":"-","priority":3,"prefix":true},
    //                    {"name":"+","priority":3,"prefix":true},
    //                    {"name":"!","priority":3,"prefix":true},
    //                    {"name":"%","priority":3,"prefix":false},
    //                    {"name":"*","priority":3,"prefix":false}
    //                    ],
    //                "operators":
    //                    [
    //                    {"name":"+","priority":1},
    //                    {"name":"-","priority":1},
    //                    {"name":"*","priority":2},
    //                    {"name":"/","priority":2},
    //                    {"name":"^","priority":4,"lefttoright":false},
    //                    {"name":"@","priority":4}
    //                    ]
    //            }
    //        }
    //        """
    //   , _jsonSerializerOptions)!;


    /// <summary>
    /// Returns a default TokenizerOptions instance built with code (no JSON deserialization).
    /// Mirrors the previous JSON Default payload.
    /// </summary>
    public static TokenizerOptions Default =>
        new()
        {
            Version = "1.0",
            CaseSensitive = false,
            TokenPatterns = new TokenPatterns
            {
                Identifier = "[A-Za-z_]\\w*",
                Literal = "\\b(?:\\d+(?:\\.\\d*)?|\\.\\d+)\\b",
                OpenParenthesis = '(',
                CloseParenthesis = ')',
                ArgumentSeparator = ",",
                Unary =
                [
                    new() { Name = "-", Priority = 3, Prefix = true },
                    new() { Name = "+", Priority = 3, Prefix = true },
                    new() { Name = "!", Priority = 3, Prefix = true },
                    new() { Name = "%", Priority = 3, Prefix = false },
                    new() { Name = "*", Priority = 3, Prefix = false },
                ],
                Operators =
                [
                    new() { Name = "+", Priority = 1 },
                    new() { Name = "-", Priority = 1 },
                    new() { Name = "*", Priority = 2 },
                    new() { Name = "/", Priority = 2 },
                    new() { Name = "^", Priority = 4, LeftToRight = false },
                    new() { Name = "@", Priority = 4 }
                ]
            }
        };


}