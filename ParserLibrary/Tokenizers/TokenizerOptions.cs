using System.Text.Json;

namespace ParserLibrary.Tokenizers;

public class TokenizerOptions
{

    public static string TokenizerSection = "tokenizer";

    public string? Version { get; set; }

    public bool CaseSensitive { get; set; } = false;

#nullable disable
    public TokenPatterns TokenPatterns { get; set; }
#nullable restore

    private readonly static JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public static TokenizerOptions Default =
        System.Text.Json.JsonSerializer.Deserialize<TokenizerOptions>("""
            {"version":"1.0","caseSensitive":false,"tokenPatterns":{"identifier":"[A-Za-z_]\\w*","literal":"\\b(?:\\d+(?:\\.\\d*)?|\\.\\d+)\\b","openParenthesis":"(","closeParenthesis":")","argumentSeparator":",","unary":[{"name":"-","priority":3,"prefix":true},{"name":"+","priority":3,"prefix":true},{"name":"!","priority":3,"prefix":true},{"name":"%","priority":3,"prefix":false},{"name":"*","priority":3,"prefix":false}],"operators":[{"name":",","priority":0},{"name":"+","priority":1},{"name":"-","priority":1},{"name":"*","priority":2},{"name":"/","priority":2},{"name":"^","priority":4,"lefttoright":false},{"name":"@","priority":4}]}}
            """
       , _jsonSerializerOptions)!;





}