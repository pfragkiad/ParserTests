using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers.Validation;

public sealed class TokenizerValidator : ITokenizerValidator
{
    private readonly ILogger<TokenizerValidator> _logger;
    private readonly TokenPatterns _patterns;

    public TokenizerValidator(ILogger<TokenizerValidator> logger, TokenPatterns patterns)
    {
        _logger = logger;
        _patterns = patterns;
    }

    #region Parentheses

    public bool PreValidateParentheses(string expression, out ParenthesisErrorCheckResult? detail)
    {
        if (AreParenthesesMatchedFast(expression))
        {
            detail = null;
            return true;
        }

        detail = BuildParenthesisCheckDetail(expression);
        _logger.LogWarning("Unmatched parentheses detected.");
        return false;
    }

    // string-only, fast scan
    private bool AreParenthesesMatchedFast(string expression)
    {
        char open = _patterns.OpenParenthesis;
        char close = _patterns.CloseParenthesis;

        int count = 0;
        foreach (char c in expression)
        {
            if (c == open) { count++; continue; }
            if (c != close) continue;
            if (--count < 0) return false;
        }
        return count == 0;
    }

    // detailed positions (only when invalid)
    private ParenthesisErrorCheckResult BuildParenthesisCheckDetail(string expression)
    {
        char open = _patterns.OpenParenthesis;
        char close = _patterns.CloseParenthesis;

        List<int> unmatchedClosed = [];
        List<int> openPositions = [];

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            if (c == open)
            {
                openPositions.Add(i);
                continue;
            }

            if (c != close) continue;

            if (openPositions.Count == 0)
                unmatchedClosed.Add(i);
            else
                openPositions.RemoveAt(openPositions.Count - 1);
        }

        return new ParenthesisErrorCheckResult
        {
            UnmatchedClosed = unmatchedClosed,
            UnmatchedOpen = openPositions
        };
    }

    #endregion

    #region Variable names

    public VariableNamesCheckResult CheckVariableNames(
        List<Token> infixTokens,
        HashSet<string> identifierNames,
        string[] ignoreCaptureGroups)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (identifierNames.Contains(t.Text))
            {
                matched.Add(t.Text);
                continue;
            }

            if (ignoreCaptureGroups.Any(g => t.Match!.Groups[g].Success))
            {
                ignored.Add(t.Text);
                continue;
            }

            unmatched.Add(t.Text);
        }

        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched],
            IgnoredNames = [.. ignored]
        };
    }

    public VariableNamesCheckResult CheckVariableNames(
        List<Token> infixTokens,
        HashSet<string> identifierNames,
        Regex? ignoreIdentifierPattern)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (identifierNames.Contains(t.Text))
            {
                matched.Add(t.Text);
                continue;
            }

            if (ignoreIdentifierPattern is not null && ignoreIdentifierPattern.IsMatch(t.Text))
            {
                ignored.Add(t.Text);
                continue;
            }

            unmatched.Add(t.Text);
        }

        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched],
            IgnoredNames = [.. ignored]
        };
    }

    public VariableNamesCheckResult CheckVariableNames(
        List<Token> infixTokens,
        HashSet<string> identifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (identifierNames.Contains(t.Text))
            {
                matched.Add(t.Text);
                continue;
            }

            bool ignoredByPrefix = ignorePrefixes.Any(p => t.Text.StartsWith(p));
            bool ignoredByPostfix = ignorePostfixes.Any(s => t.Text.EndsWith(s));
            if (ignoredByPrefix || ignoredByPostfix)
            {
                ignored.Add(t.Text);
                continue;
            }

            unmatched.Add(t.Text);
        }

        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matched],
            UnmatchedNames = [.. unmatched],
            IgnoredNames = [.. ignored]
        };
    }

    #endregion

    // Universal, two-step validation. No tokenization here.
    // - Always checks parentheses via string.
    // - If matched AND both infixTokens and varNameOptions provided, runs variable name checks.
    public TokenizerValidationReport Validate(
        string expression,
        List<Token>? infixTokens = null,
        VariableNamesOptions? varNameOptions = null)
    {
        var matched = PreValidateParentheses(expression, out var parenDetail);

        VariableNamesCheckResult? namesResult = null;
        if (matched && infixTokens is not null && varNameOptions is not null)
        {
            if (varNameOptions.IgnoreCaptureGroups is { Length: > 0 })
            {
                namesResult = CheckVariableNames(infixTokens, varNameOptions.IdentifierNames, varNameOptions.IgnoreCaptureGroups);
            }
            else if (varNameOptions.IgnoreIdentifierPattern is not null)
            {
                namesResult = CheckVariableNames(infixTokens, varNameOptions.IdentifierNames, varNameOptions.IgnoreIdentifierPattern);
            }
            else if ((varNameOptions.IgnorePrefixes is not null && varNameOptions.IgnorePrefixes.Length > 0) ||
                     (varNameOptions.IgnorePostfixes is not null && varNameOptions.IgnorePostfixes.Length > 0))
            {
                namesResult = CheckVariableNames(
                    infixTokens,
                    varNameOptions.IdentifierNames,
                    varNameOptions.IgnorePrefixes ?? Array.Empty<string>(),
                    varNameOptions.IgnorePostfixes ?? Array.Empty<string>());
            }
            else
            {
                // No ignore rules: treat as strict (no ignores)
                namesResult = CheckVariableNames(infixTokens, varNameOptions.IdentifierNames, Array.Empty<string>());
            }
        }

        return new TokenizerValidationReport
        {
            Expression = expression,
            ParenthesesMatched = matched,
            ParenthesesDetail = matched ? null : parenDetail,
            VariableNames = namesResult
        };
    }

    // Aggregator for the post stage (infix-only).
    public VariableNamesCheckResult PostValidateVariableNames(List<Token> infixTokens, VariableNamesOptions options)
    {
        if (options.IgnoreCaptureGroups is { Length: > 0 })
            return CheckVariableNames(infixTokens, options.IdentifierNames, options.IgnoreCaptureGroups);

        if (options.IgnoreIdentifierPattern is not null)
            return CheckVariableNames(infixTokens, options.IdentifierNames, options.IgnoreIdentifierPattern);

        if ((options.IgnorePrefixes is not null && options.IgnorePrefixes.Length > 0) ||
            (options.IgnorePostfixes is not null && options.IgnorePostfixes.Length > 0))
            return CheckVariableNames(
                infixTokens,
                options.IdentifierNames,
                options.IgnorePrefixes ?? Array.Empty<string>(),
                options.IgnorePostfixes ?? Array.Empty<string>());

        // No ignore rules: strict matching (no ignores)
        return CheckVariableNames(infixTokens, options.IdentifierNames, Array.Empty<string>());
    }
}