using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers.Validation;

public class TokenizerValidator : ITokenizerValidator
{
    private readonly ILogger<TokenizerValidator> _logger;
    private readonly TokenPatterns _patterns;

    public TokenizerValidator(ILogger<TokenizerValidator> logger, TokenPatterns patterns)
    {
        _logger = logger;
        _patterns = patterns;
    }

    #region Parentheses

    public ParenthesisCheckResult CheckParentheses(string expression)
    {
        if (AreParenthesesMatchedFast(expression)) return ParenthesisCheckResult.Success;

        _logger.LogWarning("Unmatched parentheses detected.");
        return BuildParenthesisCheckDetail(expression);
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
    private ParenthesisCheckResult BuildParenthesisCheckDetail(string expression)
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

        return new ParenthesisCheckResult
        {
            UnmatchedClosed = unmatchedClosed,
            UnmatchedOpen = openPositions
        };
    }

    #endregion

    #region Variable names

    public VariableNamesCheckResult CheckVariableNames(
        List<Token> infixTokens,
        HashSet<string> knownIdentifierNames,
        string[] ignoreCaptureGroups)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (knownIdentifierNames.Contains(t.Text))
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
        HashSet<string> knownIdentifierNames,
        Regex? ignoreIdentifierPattern)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (knownIdentifierNames.Contains(t.Text))
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
        HashSet<string> knownIdentifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes)
    {
        HashSet<string> matched = [];
        HashSet<string> unmatched = [];
        HashSet<string> ignored = [];

        foreach (var t in infixTokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (knownIdentifierNames.Contains(t.Text))
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

    // Aggregator for the post stage (infix-only).
    public VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, VariableNamesOptions options)
    {
        if (options.IgnoreCaptureGroups is { Length: > 0 })
            return CheckVariableNames(infixTokens, options.KnownIdentifierNames, options.IgnoreCaptureGroups);

        if (options.IgnoreIdentifierPattern is not null)
            return CheckVariableNames(infixTokens, options.KnownIdentifierNames, options.IgnoreIdentifierPattern);

        if ((options.IgnorePrefixes is not null && options.IgnorePrefixes.Length > 0) ||
            (options.IgnorePostfixes is not null && options.IgnorePostfixes.Length > 0))
            return CheckVariableNames(
                infixTokens,
                options.KnownIdentifierNames,
                options.IgnorePrefixes ?? [],
                options.IgnorePostfixes ?? []);

        // No ignore rules: strict matching (no ignores)
        return CheckVariableNames(infixTokens, options.KnownIdentifierNames, []);
    }

    #endregion

    // NEW: Adjacent operands – require a binary operator between them.
    // Left: Literal or Identifier
    // Right: Literal, Identifier, Function, or OpenParenthesis
    public AdjacentOperandsCheckResult CheckAdjacentOperands(List<Token> infixTokens)
    {
        static bool IsLeftOperandOrStart(Token t) =>
            t.TokenType == TokenType.Literal ||
            t.TokenType == TokenType.Identifier ||
            t.TokenType == TokenType.ClosedParenthesis;

        static bool IsRightOperandOrStartOfGroupOrCall(Token t) =>
            t.TokenType == TokenType.Literal ||
            t.TokenType == TokenType.Identifier ||
            t.TokenType == TokenType.Function ||
            t.TokenType == TokenType.OpenParenthesis;

        var violations = new List<AdjacentOperandsViolation>();

        for (int i = 1; i < infixTokens.Count; i++)
        {
            var left = infixTokens[i - 1];
            var right = infixTokens[i];

            bool isInvalid = IsLeftOperandOrStart(left) && IsRightOperandOrStartOfGroupOrCall(right) ||
                //also check for invalid operator combinations op|op, unaryOp (pre)|op, op|unaryOp (post)
                left.TokenType  == TokenType.Operator && right.TokenType == TokenType.Operator ||
                left.TokenType == TokenType.OperatorUnary && _patterns.UnaryOperatorDictionary[left.Text].Prefix  && right.TokenType == TokenType.Operator ||
                left.TokenType == TokenType.Operator && right.TokenType == TokenType.OperatorUnary && !_patterns.UnaryOperatorDictionary[right.Text].Prefix
                ;

            if(isInvalid)
            {
                violations.Add(new AdjacentOperandsViolation
                {
                    LeftToken = left.Text,
                    LeftPosition = left.Index + 1,
                    RightToken = right.Text,
                    RightPosition = right.Index + 1
                });
            }
        }

        if (violations.Count > 0)
            _logger.LogWarning("Adjacent operands without operator: {pairs}",
                string.Join(", ", violations.Select(v => $"{v.LeftPosition}-{v.RightPosition}")));

        return new AdjacentOperandsCheckResult { Violations = violations };
    }
}