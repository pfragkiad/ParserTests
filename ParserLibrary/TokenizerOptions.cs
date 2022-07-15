namespace ParserLibrary;

public class TokenizerOptions
{

    public static string TokenizerSection = "tokenizer";

    public string? Version { get; set; }

    public bool CaseSensitive { get; set; } = false;

#nullable disable
    public TokenPatterns TokenPatterns { get; set; }
#nullable restore

    public static TokenizerOptions Default = 
        System.Text.Json.JsonSerializer.Deserialize<TokenizerOptions>(
        "{\n    \"version\": \"1.0\",\n    \"caseSensitive\": false,\n    \"tokenPatterns\": {\n      \"identifier\": \"[A-Za-z_]\\\\w*\",\n      \"literal\": \"\\\\b(?:\\\\d+(?:\\\\.\\\\d*)?|\\\\.\\\\d+)\\\\b\",\n      \"openParenthesis\": \"(\",\n      \"closeParenthesis\": \")\",\n      \"argumentSeparator\": \",\",\n\n      \"unary\": [\n        {\n          \"name\": \"-\",\n          \"priority\": 3,\n          \"prefix\": true\n        },\n        {\n          \"name\": \"+\",\n          \"priority\": 3,\n          \"prefix\": true\n        },\n        {\n          \"name\": \"!\",\n          \"priority\": 3,\n          \"prefix\": true\n        },\n        {\n          \"name\": \"%\",\n          \"priority\": 3,\n          \"prefix\": false\n        },\n        {\n          \"name\": \"*\",\n          \"priority\": 3,\n          \"prefix\": false\n        }\n      ],\n      \"operators\": [\n        {\n          \"name\": \",\",\n          \"priority\": 0\n        },\n        {\n          \"name\": \"+\",\n          \"priority\": 1\n        },\n        {\n          \"name\": \"-\",\n          \"priority\": 1\n        },\n        {\n          \"name\": \"*\",\n          \"priority\": 2\n        },\n        {\n          \"name\": \"/\",\n          \"priority\": 2\n        },\n        {\n          \"name\": \"^\",\n          \"priority\": 4,\n          \"lefttoright\": false\n        }\n      ]\n    }\n  }"
       ,new System.Text.Json.JsonSerializerOptions() { PropertyNameCaseInsensitive = true}
            );


    
 
}