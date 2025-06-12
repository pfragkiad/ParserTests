namespace ParserLibrary.Parsers;

public class ParserBase  : Tokenizer, IParserBase
{
    public ParserBase(ILogger logger, IOptions<TokenizerOptions> options)
        :base(logger, options)
    { }

    protected Dictionary<string, (string[] Parameters, string Body)> _customFunctions = [];

    public void RegisterFunction(string definition)
    {
        // Example: "myf(x,y) = 10*x+sin(y)"
        var parts = definition.Split('=', 2);
        if (parts.Length != 2)
            throw new ArgumentException("Invalid function definition format.");

        var header = parts[0].Trim();
        var body = parts[1].Trim();

        var nameAndParams = header.Split('(', 2);
        if (nameAndParams.Length != 2 || !nameAndParams[1].EndsWith(")"))
            throw new ArgumentException("Invalid function header format.");

        var name = nameAndParams[0].Trim();

        var paramList = nameAndParams[1][..^1].Split(_options.TokenPatterns.ArgumentSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _customFunctions[name] = (paramList, body);
    }

    protected virtual List<string> MainFunctionNames => [];
    //needed for checking the function identifiers in the expression tree

    public FunctionNamesCheckResult CheckFunctionNames(string expression)
    {
        //returns the names of the functions that are not registered
        var tokens = GetInOrderTokens(expression);
        //var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);

        HashSet<string> matchedNames = [];
        HashSet<string> unmatchedNames = [];
        foreach (var t in tokens.Where(t => t.TokenType == TokenType.Function))
        {
            if (_customFunctions.ContainsKey(t.Text) || MainFunctionNames.Contains(t.Text))
            { matchedNames.Add(t.Text); continue; }

            unmatchedNames.Add(t.Text);
        }

        return new FunctionNamesCheckResult
        {
            MatchedNames = [.. matchedNames],
            UnmatchedNames = [.. unmatchedNames]
        };
    }

    public List<string> GetMatchedFunctionNames(string expression)
    {
        //returns the names of the functions that are registered
        var tokens = GetInOrderTokens(expression);
        //var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);
        return [.. tokens
            .Where(t => t.TokenType == TokenType.Function &&
                (_customFunctions.ContainsKey(t.Text) || MainFunctionNames.Contains(t.Text)))
            .Select(t => t.Text)
            .Distinct()];
    }


}
