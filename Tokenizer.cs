using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParserTests;

public class Tokenizer : ITokenizer
{
    private readonly ILogger<Tokenizer> _logger;
    private readonly TokenizerOptions _options;

    public Tokenizer(ILogger<Tokenizer> logger, IOptions<TokenizerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public List<Token> Tokenize(string expression)
    {
        //inspiration: https://gwerren.com/Blog/Posts/simpleCSharpTokenizerUsingRegex

        _logger.LogDebug("Retrieving infix tokens...");

        List<Token> tokens = new();

        //identifiers
        var matches =
            _options.CaseSensitive ?
            Regex.Matches(expression, _options.TokenPatterns.Identifier) :
            Regex.Matches(expression, _options.TokenPatterns.Identifier, RegexOptions.IgnoreCase);
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(Token.IdentifierTokenType, m)));

        //literals
        matches = Regex.Matches(expression, _options.TokenPatterns.Literal);
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(Token.LiteralTokenType, m)));

        //open parenthesis
        matches = Regex.Matches(expression, $@"\{_options.TokenPatterns.OpenParenthesis}");
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(Token.OpenParenthesisTokenType, m)));

        //close parenthesis
        matches = Regex.Matches(expression, $@"\{_options.TokenPatterns.CloseParenthesis}");
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(Token.CloseParenthesisTokenType, m)));

        //operators
        foreach (var op in _options.TokenPatterns.Operators)
        {
            matches = Regex.Matches(expression, $@"\{op.Name}");
            if (matches.Any())
                tokens.AddRange(matches.Select(m => new Token(Token.OperatorTokenType, m)));
        }

        //sort by Match.Index (get "infix ordering")
        tokens.Sort();

        //now we need to convert to postfix
        //https://youtu.be/PAceaOSnxQs

        //https://www.techiedelight.com/expression-tree/

        if (_logger is not null)
        {
            foreach (var token in tokens)
                _logger.LogDebug("{token} ({type})", token.Match.Value, token.TokenType);
        }

        return tokens;
    }

}
