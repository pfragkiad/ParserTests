using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Tokenizers;

public class Tokenizer : ITokenizer
{
    protected readonly ILogger _logger;

    protected Operator? ArgumentOperator;

    protected readonly ITokenizerValidator _tokenizerValidator;

    public Tokenizer(ILogger<Tokenizer> logger, IOptions<TokenizerOptions> options, ITokenizerValidator tokenizerValidator) :
        this(logger as ILogger, options, tokenizerValidator)
    { }

    protected Tokenizer(ILogger logger, IOptions<TokenizerOptions> options, ITokenizerValidator tokenizerValidator)
    {
        _logger = logger;
        _options = options.Value;
        _patterns = _options.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;

        if (_options.TokenPatterns is null) _options = TokenizerOptions.Default;

        _tokenizerValidator = tokenizerValidator ?? throw new ArgumentNullException(nameof(tokenizerValidator));

        // ---- Cached regex initialization (singleton-friendly) ----
        if (_patterns.Identifier is null && !_patterns.HasNamedIdentifiers)
            throw new InvalidOperationException("Identifier pattern cannot be null.");

        if (_patterns.Literal is null && !_patterns.HasNamedLiterals)
            throw new InvalidOperationException("Literal pattern cannot be null.");

        var identifierOptions = _patterns.CaseSensitive ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.IgnoreCase;
        var literalOptions = identifierOptions; // case sensitivity rule is shared

        _identifierRegex = new Regex(_patterns.GetIdentifierPattern(), identifierOptions);
        _identifierGroupNames = _patterns.IdentifierNames;
        _identifierHasNamedGroups = _patterns.HasNamedIdentifiers;
        if (!_identifierHasNamedGroups) //attempt to get from regex if not explicitly set
        {
            _identifierGroupNames = [.. _identifierRegex
            .GetGroupNames()
            .Where(n => !int.TryParse(n, out _))];
            _identifierHasNamedGroups = _identifierGroupNames.Length > 0;
        }

        _literalRegex = new Regex(_patterns.GetLiteralPattern(), literalOptions);
        _literalGroupNames = _patterns.LiteralNames;
        _literalHasNamedGroups = _literalGroupNames.Length > 0;
        if (!_literalHasNamedGroups) //attempt to get from regex if not explicitly set
        {
            _literalGroupNames = [.. _literalRegex
            .GetGroupNames()
            .Where(n => !int.TryParse(n, out _))];

            _literalHasNamedGroups = _literalGroupNames.Length > 0;
        }

        // Precompile per-operator regexes (binary & same-name unary/binary). Process longer names first.
        _operatorRegexes = [.. (_patterns.Operators ?? [])
            .OrderByDescending(o => o.Name.Length)
            .Select(o => (op: o, regex: new Regex(Regex.Escape(o.Name), RegexOptions.Compiled)))];

        // Precompile unique unary operator regexes (those not sharing name with binary operators) — longer first too
        _uniqueUnaryOperatorRegexes = _patterns.UniqueUnaryOperators is { Count: > 0 }
                ? [.. _patterns.UniqueUnaryOperators
            .OrderByDescending(u => u.Name.Length)
            .Select(u => (op: u, regex: new Regex(Regex.Escape(u.Name), RegexOptions.Compiled)))]
                : [];
        // ---- Lightweight single-pass precomputations (non-regex operator matching) ----
        _operatorNamesByLenDesc = [.. (_patterns.Operators ?? [])
            .Select(o => o.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderByDescending(n => n.Length)];

        _uniqueUnaryNamesByLenDesc = _patterns.UniqueUnaryOperators is { Count: > 0 }
            ? [.. _patterns.UniqueUnaryOperators
                .Select(u => u.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderByDescending(n => n.Length)] : [];

        _operatorStartChars = [.. _operatorNamesByLenDesc
            .Concat(_uniqueUnaryNamesByLenDesc)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n[0])];

        // Only enable the single-pass path when operators are symbolic (no textual ops like 'and', 'not')
        // to avoid ambiguous overlaps with identifiers.
        _enableSinglePass = _operatorStartChars.All(ch => !char.IsLetterOrDigit(ch) && ch != '_');
    }

    protected readonly TokenPatterns _patterns;

    protected readonly TokenizerOptions _options;
    public TokenizerOptions TokenizerOptions => _options;

    // Cached regex fields
    private readonly Regex _identifierRegex;
    private readonly string[] _identifierGroupNames;
    private readonly bool _identifierHasNamedGroups;

    private readonly Regex _literalRegex;
    private readonly string[] _literalGroupNames;
    private readonly bool _literalHasNamedGroups;

    private readonly (Operator op, Regex regex)[] _operatorRegexes;
    private readonly (UnaryOperator op, Regex regex)[] _uniqueUnaryOperatorRegexes;

    // Lightweight single-pass fields
    private readonly string[] _operatorNamesByLenDesc;
    private readonly string[] _uniqueUnaryNamesByLenDesc;
    private readonly HashSet<char> _operatorStartChars;
    private readonly bool _enableSinglePass;

    public List<Token> GetIdentifiers(string expression, string captureGroup)
    {
        MatchCollection litMatches = _literalRegex.Matches(expression);
        if (litMatches.Count == 0) return [];

        List<Token> tokens = [];
        foreach (Match m in litMatches)
        {
            string? group = FirstSuccessfulNamedGroup(m, _literalGroupNames);
            if (group is null) continue;


            if (group.Equals(captureGroup, StringComparison.OrdinalIgnoreCase))
                tokens.Add(Token.FromMatch(m, TokenType.Literal, group));
        }
        return tokens;
    }

    protected static string? FirstSuccessfulNamedGroup(Match m, string[] groupNames)
    {
        foreach (var g in groupNames)
            if (m.Groups[g].Success) return g;
        return null;
    }

    //The second property contains the number of arguments needed by each corresponding function.
    public List<Token> GetInfixTokens(string expression)
    {
        // Fast-path single-pass scanner (minimizes regex usage)
        if (_enableSinglePass)
            return GetInfixTokens_SinglePass(expression);

        _logger.LogDebug("Retrieving infix tokens...");

        List<Token> tokens = [];
        HashSet<int> functionParenthesisPositions = [];

        // 1) LITERALS FIRST - collect literal tokens and spans
        MatchCollection litMatches = _literalRegex.Matches(expression);
        if (litMatches.Count > 0)
        {
            foreach (Match m in litMatches)
            {
                string? group = _literalHasNamedGroups ? FirstSuccessfulNamedGroup(m, _literalGroupNames) : null;
                tokens.Add(Token.FromMatch(m, TokenType.Literal, group));
            }
        }

        // Build literal spans to ignore regex hits inside quotes/literals
        List<(int Start, int End)> literalSpans = [];
        if (litMatches.Count > 0)
        {
            foreach (Match m in litMatches)
                literalSpans.Add((m.Index, m.Index + m.Length));
        }

        static bool IsInsideAnySpan(int start, int end, List<(int Start, int End)> spans)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                var s = spans[i];
                if (start >= s.Start && end <= s.End) return true;
            }
            return false;
        }

        // 2) IDENTIFIERS (+ possible functions) - skip matches inside literal spans
        MatchCollection idMatches = _identifierRegex.Matches(expression);
        List<(int Start, int End)> identifierSpans = [];
        if (idMatches.Count > 0)
        {
            foreach (Match m in idMatches)
            {
                // Do not add identifiers/functions that are already part of a literal
                if (IsInsideAnySpan(m.Index, m.Index + m.Length, literalSpans))
                    continue;

                string? group = _identifierHasNamedGroups ? FirstSuccessfulNamedGroup(m, _identifierGroupNames) : null;

                int i = m.Index + m.Length;
                // Skip optional whitespace between identifier and '('
                while (i < expression.Length && char.IsWhiteSpace(expression[i])) i++;

                if (i < expression.Length && expression[i] == _patterns.OpenParenthesis)
                {
                    // Function: add function token and remember '(' position to avoid re-adding it
                    tokens.Add(Token.FromMatch(m, TokenType.Function, group));
                    functionParenthesisPositions.Add(i);
                    identifierSpans.Add((m.Index, m.Index + m.Length));
                    continue;
                }

                // Plain identifier
                tokens.Add(Token.FromMatch(m, TokenType.Identifier, group));
                identifierSpans.Add((m.Index, m.Index + m.Length));
            }
        }

        // 3) Parentheses & argument separators (single pass over chars)
        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];
            if (c == _patterns.OpenParenthesis && !functionParenthesisPositions.Contains(i))
                tokens.Add(new Token(TokenType.OpenParenthesis, c, i));
            else if (c == _patterns.CloseParenthesis)
                tokens.Add(new Token(TokenType.ClosedParenthesis, c, i));
            else if (c == _patterns.ArgumentSeparator)
                tokens.Add(new Token(TokenType.ArgumentSeparator, c, i));
        }

        // Track already-claimed operator spans so shorter ops inside a longer one are skipped.
        List<(int Start, int End)> operatorSpans = [];

        // 4) Unique unary operators (precompiled) - skip matches inside literals/identifiers/operators
        //    longest operator names first; ensure earliest-longest wins at the same start.
        if (_uniqueUnaryOperatorRegexes.Length > 0)
        {
            var unaryStarts = new HashSet<int>();
            foreach (var (op, regex) in _uniqueUnaryOperatorRegexes)
            {
                var matches = regex.Matches(expression);
                if (matches.Count == 0) continue;
                foreach (Match m in matches)
                {
                    int start = m.Index;
                    int end = m.Index + m.Length;
                    if (IsInsideAnySpan(start, end, literalSpans)) continue;
                    if (IsInsideAnySpan(start, end, identifierSpans)) continue;
                    if (IsInsideAnySpan(start, end, operatorSpans)) continue;

                    // longest-first guarantees correct choice; skip if a longer match already claimed this start
                    if (!unaryStarts.Add(start)) continue;

                    tokens.Add(Token.FromMatch(m, TokenType.OperatorUnary));
                    operatorSpans.Add((start, end));
                }
            }
        }

        // 5) Operators (binary or ambiguous) - skip matches inside literals/identifiers/operators
        //    longest operator names first; ensure earliest-longest wins at the same start.
        if (_operatorRegexes.Length > 0)
        {
            var opStarts = new HashSet<int>();
            foreach (var (op, regex) in _operatorRegexes)
            {
                var matches = regex.Matches(expression);
                if (matches.Count == 0) continue;
                foreach (Match m in matches)
                {
                    int start = m.Index;
                    int end = m.Index + m.Length;
                    if (IsInsideAnySpan(start, end, literalSpans)) continue;
                    if (IsInsideAnySpan(start, end, identifierSpans)) continue;
                    if (IsInsideAnySpan(start, end, operatorSpans)) continue;

                    // longest-first guarantees correct choice; skip if a longer match already claimed this start
                    if (!opStarts.Add(start)) continue;

                    tokens.Add(Token.FromMatch(m, TokenType.Operator));
                    operatorSpans.Add((start, end));
                }
            }
        }

        // sort by index (infix ordering)
        tokens.Sort();

        FixUnaryOperators(tokens);

        return tokens;
    }

    // Lightweight single-pass tokenizer (minimizes regex usage; operators matched without regex)
    private List<Token> GetInfixTokens_SinglePass(string expression)
    {
        _logger.LogDebug("Retrieving infix tokens (single-pass/lightweight)...");
        var tokens = new List<Token>(Math.Max(8, expression.Length / 2));
        var functionParenthesisPositions = new HashSet<int>(); // to avoid re-adding '(' after Function
        var span = expression.AsSpan();

        int i = 0;
        while (i < span.Length)
        {
            char c = span[i];

            // Skip whitespace
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Parentheses & argument separators
            if (c == _patterns.OpenParenthesis)
            {
                if (!functionParenthesisPositions.Contains(i))
                    tokens.Add(new Token(TokenType.OpenParenthesis, c, i));
                i++;
                continue;
            }
            if (c == _patterns.CloseParenthesis)
            {
                tokens.Add(new Token(TokenType.ClosedParenthesis, c, i));
                i++;
                continue;
            }
            if (c == _patterns.ArgumentSeparator)
            {
                tokens.Add(new Token(TokenType.ArgumentSeparator, c, i));
                i++;
                continue;
            }

            // Try operators first (symbolic-only set). Longest match wins.
            if (_operatorStartChars.Contains(c))
            {
                if (TryMatchOperator(span, i, _uniqueUnaryNamesByLenDesc, out int ulen))
                {
                    tokens.Add(new Token(TokenType.OperatorUnary, expression.Substring(i, ulen), i));
                    i += ulen;
                    continue;
                }
                if (TryMatchOperator(span, i, _operatorNamesByLenDesc, out int blen))
                {
                    tokens.Add(new Token(TokenType.Operator, expression.Substring(i, blen), i));
                    i += blen;
                    continue;
                }
            }

            // Literal via regex at current index only (DO THIS BEFORE IDENTIFIER)
            if (LooksLikeLiteralStart(span, i))
            {
                var m = _literalRegex.Match(expression, i);
                if (m.Success && m.Index == i)
                {
                    string? group = _literalHasNamedGroups ? FirstSuccessfulNamedGroup(m, _literalGroupNames) : null;
                    tokens.Add(new Token(TokenType.Literal, m.Value, i, group));
                    i += m.Length;
                    continue;
                }
            }

            // Identifier (+function) via regex at current index only.
            if (LooksLikeIdentifierStart(c))
            {
                var m = _identifierRegex.Match(expression, i);
                if (m.Success && m.Index == i)
                {
                    string? group = _identifierHasNamedGroups ? FirstSuccessfulNamedGroup(m, _identifierGroupNames) : null;
                    var text = m.Value;

                    // detect function call: optional spaces + '('
                    int j = i + text.Length;
                    while (j < span.Length && char.IsWhiteSpace(span[j])) j++;
                    bool isFunc = j < span.Length && span[j] == _patterns.OpenParenthesis;
                    if (isFunc) functionParenthesisPositions.Add(j);

                    tokens.Add(new Token(isFunc ? TokenType.Function : TokenType.Identifier, text, i, group));
                    i += text.Length;
                    continue;
                }
            }

            // Fallback: if nothing matched, try operator matching again.
            if (TryMatchOperator(span, i, _uniqueUnaryNamesByLenDesc, out int ulen2))
            {
                tokens.Add(new Token(TokenType.OperatorUnary, expression.Substring(i, ulen2), i));
                i += ulen2;
                continue;
            }
            if (TryMatchOperator(span, i, _operatorNamesByLenDesc, out int blen2))
            {
                tokens.Add(new Token(TokenType.Operator, expression.Substring(i, blen2), i));
                i += blen2;
                continue;
            }

            // Unknown character: skip
            _logger.LogDebug("Skipping unknown character '{c}' at {i}", c, i);
            i++;
        }

        // Maintain original behavior: decide unary/binary for same-name operators afterward
        FixUnaryOperators(tokens);
        return tokens;
    }

    private static bool LooksLikeIdentifierStart(char c)
        => char.IsLetter(c) || c == '_' || c == '$'
        || c == '[' || c == '{'; //IDENTIFIER can be enclosed in [] or {}

    private static bool LooksLikeLiteralStart(ReadOnlySpan<char> s, int i)
    {
        char c = s[i];
        if (char.IsDigit(c) || c == '"' || c == '\'') return true;
        if (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])) return true;

        //SOS: check for preceding dot for properties like .Property (which should be considered a 'literal' )
        int prev = i - 1;
        while (prev >= 0 && char.IsWhiteSpace(s[prev])) prev--;
        if (prev >= 0 && s[prev] == '.') return true;

        return false;
    }

    private static bool TryMatchOperator(ReadOnlySpan<char> span, int index, string[] namesByLenDesc, out int matchedLength)
    {
        foreach (var name in namesByLenDesc)
        {
            if (name.Length == 0) continue;
            if (index + name.Length > span.Length) continue;
            if (span.Slice(index, name.Length).Equals(name, StringComparison.Ordinal))
            {
                matchedLength = name.Length;
                return true;
            }
        }
        matchedLength = 0;
        return false;
    }

    private void FixUnaryOperators(List<Token> tokens)
    {
        var unaryDictionary = _patterns.UnaryOperatorDictionary;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.TokenType != TokenType.Operator) continue;

            // Only process operators that could be unary
            bool foundSameUnary = _patterns.SameNameUnaryAndBinaryOperators.Contains(token.Text);
            if (!foundSameUnary) continue;

            var matchedUnaryOp = unaryDictionary[token.Text];

            UnaryOperator unary = matchedUnaryOp!;
            if (i == 0 && unary.Prefix || i == tokens.Count - 1 && !unary.Prefix)
            {
                token.TokenType = TokenType.OperatorUnary; continue;
            }

            if (i == 0 && !unary.Prefix) continue; //stay as binary

            Token previousToken = tokens[i - 1];
            TokenType previousTokenType = previousToken!.TokenType;

            if (unary.Prefix)
            {
                if (previousTokenType == TokenType.Literal || previousTokenType == TokenType.Identifier)
                    continue; // previous is value => this is binary

                if (previousTokenType == TokenType.Operator ||                 // + -2
                    previousTokenType == TokenType.ArgumentSeparator ||        // , -2
                    previousTokenType == TokenType.OperatorUnary &&            // ---2
                        unaryDictionary[previousToken.Text].Prefix ||
                    previousTokenType == TokenType.OpenParenthesis ||          //  (-2
                    previousTokenType == TokenType.Function)                   // func(-2
                {
                    token.TokenType = TokenType.OperatorUnary;
                }
                continue;
            }

            //unary.postfix case from now on (assuming in comments that '*' is also a unary postfix operator)
            bool canBePostfix =
                previousTokenType == TokenType.Literal ||    //5*
                previousTokenType == TokenType.Identifier || //a*
                (previousTokenType == TokenType.OperatorUnary && !unaryDictionary[previousToken.Text].Prefix); //previous is postfix: %*, *+            if (!canBePostfix) continue; //stay as binary
            if (!canBePostfix) continue; //stay as binary

            Token nextToken = tokens[i + 1];
            TokenType nextTokenType = nextToken.TokenType;

            //the next is a variable/literal or function or open parenthesis so this is binary
            if (nextTokenType == TokenType.Literal ||       // *2
                nextTokenType == TokenType.Identifier ||    //*a
                nextTokenType == TokenType.Function ||      //*func(
                nextTokenType == TokenType.OpenParenthesis) //*(
                continue; //stay as binary

            if (nextTokenType == TokenType.ClosedParenthesis || //a*)
                nextTokenType == TokenType.ArgumentSeparator || //a*,
                nextTokenType == TokenType.Operator)
            {
                token.TokenType = TokenType.OperatorUnary; continue;
            } //assume unary

        }
    }


    //Infix to postfix (example)
    //https://www.youtube.com/watch?v=PAceaOSnxQs
    public List<Token> GetPostfixTokens(List<Token> infixTokens)
    {
        if (infixTokens.Count == 0) return [];

        List<Token> postfixTokens = [];
        Stack<Token> operatorStack = new();

        var operators = _patterns.OperatorDictionary;
        var unary = _patterns.UnaryOperatorDictionary;
        var argumentOperator = _patterns.ArgumentSeparatorOperator;

        _logger.LogDebug("Retrieving postfix tokens...");

        for (int iToken = 0; iToken < infixTokens.Count; iToken++)
        {
            Token token = infixTokens[iToken];

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                postfixTokens.Add(token);
                _logger.LogDebug("Push to postfix expression -> {token}", token);
                continue;
            }

            if (token.TokenType == TokenType.OpenParenthesis
                || token.TokenType == TokenType.Function)
            {
                operatorStack.Push(token);
                _logger.LogDebug("Push to stack (open parenthesis) -> {token}", token);
                continue;
            }

            if (token.TokenType == TokenType.ClosedParenthesis)
            {
                _logger.LogDebug("Pop stack until open parenthesis is found (close parenthesis) -> {token}", token);

                if (iToken > 0)
                {
                    var previousToken = infixTokens[iToken - 1];
                    var previousTokenType = previousToken.TokenType;

                    if (previousTokenType == TokenType.Operator ||   //  +)
                        previousTokenType == TokenType.ArgumentSeparator || // ,)
                        previousTokenType == TokenType.Function ||   // func())
                        (previousTokenType == TokenType.OperatorUnary
                         && unary[previousToken.Text].Prefix)) // (-)
                    {
                        postfixTokens.Add(Token.Null);
                    }
                }

                do
                {
                    if (operatorStack.Count == 0)
                    {
                        _logger.LogError("Unmatched closed parenthesis. An open parenthesis should precede.");
                        throw new InvalidOperationException($"Unmatched closed parenthesis (closed parenthesis at {token.Index})");
                    }
                    var stackToken = operatorStack.Pop();
                    if (stackToken.TokenType == TokenType.OpenParenthesis) break;
                    postfixTokens.Add(stackToken);
                    _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
                    if (stackToken.TokenType == TokenType.Function) break;
                } while (true);
                continue;
            }

            // Operator / separator / unary
            Operator? currentOperator = null;
            UnaryOperator? currentUnaryOperator = null;

            if (token.TokenType == TokenType.Operator)
                currentOperator = operators[token.Text]!;
            else if (token.TokenType == TokenType.ArgumentSeparator)
                currentOperator = argumentOperator;
            else if (token.TokenType == TokenType.OperatorUnary)
                currentUnaryOperator = unary[token.Text!];

            // ADD NULL FOR MISSING LEFT OPERAND 
            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.ArgumentSeparator)
            {
                TokenType? previousTokenType = iToken > 0 ? infixTokens[iToken - 1].TokenType : null;

                if (previousTokenType is null
                    || previousTokenType == TokenType.Operator
                    || previousTokenType == TokenType.ArgumentSeparator
                    || previousTokenType == TokenType.OpenParenthesis
                    || previousTokenType == TokenType.Function)
                {
                    postfixTokens.Add(Token.Null);
                }
            }

            string message = "";
            while (operatorStack.Count != 0)
            {
                Token currentHead = operatorStack.Peek();
                if (currentHead.TokenType == TokenType.OpenParenthesis ||
                    currentHead.TokenType == TokenType.Function)
                {
                    message = "Push to stack (open parenthesis or function) -> {token}";
                    break;
                }

                Operator? currentHeadOperator = null;
                UnaryOperator? currentHeadUnaryOperator = null;
                if (currentHead.TokenType == TokenType.Operator)
                    currentHeadOperator = operators[currentHead.Text]!;
                else if (currentHead.TokenType == TokenType.ArgumentSeparator)
                    currentHeadOperator = argumentOperator;
                else if (currentHead.TokenType == TokenType.OperatorUnary)
                    currentHeadUnaryOperator = unary[currentHead.Text!];

                int? currentHeadPriority = currentHeadOperator?.Priority ?? currentHeadUnaryOperator?.Priority;

                if (currentOperator is not null
                   && (currentOperator.Priority > currentHeadPriority || currentOperator.Priority == currentHeadPriority && !currentOperator.LeftToRight)
                   ||
                   currentUnaryOperator is not null
                   && (currentUnaryOperator.Priority > currentHeadPriority || currentUnaryOperator.Priority == currentHeadPriority && currentUnaryOperator.Prefix))
                {
                    message = "Push to stack (op with high priority) -> {token}";
                    break;
                }

                var stackToken = operatorStack.Pop();
                postfixTokens.Add(stackToken);
                _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
            }

            operatorStack.Push(token);
            if (operatorStack.Count == 1)
                message = "Push to stack (empty stack) -> {token}";
            _logger.LogDebug(message, token);
        }

        //add dummy node if the expression ends with an operator or separator
        var lastToken = infixTokens[^1];
        //var unary = _patterns.UnaryOperatorDictionary;
        if (lastToken.TokenType == TokenType.Operator            // ... + 
            || lastToken.TokenType == TokenType.ArgumentSeparator // ... ,
            || (lastToken.TokenType == TokenType.OperatorUnary    // ... -
                && unary[lastToken.Text].Prefix)) // prefix-unary at end
        {
            postfixTokens.Add(Token.Null);
        }

        //check the operator stack at the end
        while (operatorStack.Count != 0)
        {
            var stackToken = operatorStack.Pop();
            if (stackToken.TokenType == TokenType.OpenParenthesis)
            {
                _logger.LogError("Unmatched open parenthesis. A closed parenthesis should follow.");
                throw new InvalidOperationException($"Unmatched open parenthesis (open parenthesis at {stackToken.Index})");
            }

            if (stackToken.TokenType == TokenType.Function)
            {
                _logger.LogError("Unmatched function open parenthesis. A closed parenthesis should follow.");
                throw new InvalidOperationException($"Unmatched function open parenthesis (open parenthesis at {stackToken.Index})");
            }

            postfixTokens.Add(stackToken);
            _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
        }
        return postfixTokens;
    }

    public List<Token> GetPostfixTokens(string expression)
    {
        var infixTokens = GetInfixTokens(expression);
        return GetPostfixTokens(infixTokens);
    }

    public List<string> GetVariableNames(string expression)
    {
        var infixTokens = GetInfixTokens(expression);
        return GetVariableNames(infixTokens);
    }

    public List<string> GetVariableNames(List<Token> infixTokens)
    {
        return [.. infixTokens
                .Where(t => t.TokenType == TokenType.Identifier)
                .Select(t => t.Text)
                .Distinct()];
    }


    #region Utility validation methods

    public ParenthesisCheckResult CheckParentheses(string expression)
    {
        return _tokenizerValidator.CheckParentheses(expression);
    }

    // Public string-based overloads: added optional checkParentheses guard (default false)
    public VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignorePrefixes,
        HashSet<string> ignorePostfixes)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckVariableNames(tokens, knownIdentifierNames, ignorePrefixes, ignorePostfixes);
    }

    public VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> knownIdentifierNames,
        Regex? ignoreIdentifierPattern = null)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckVariableNames(tokens, knownIdentifierNames, ignoreIdentifierPattern);
    }

    public VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> knownIdentifierNames,
        HashSet<string> ignoreCaptureGroups)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckVariableNames(tokens, knownIdentifierNames, ignoreCaptureGroups);
    }

    public VariableNamesCheckResult CheckVariableNames(string expression, VariableNamesOptions variableNameOptions)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckVariableNames(tokens, variableNameOptions);
    }

    public UnexpectedOperatorOperandsCheckResult CheckUnexpectedOperatorOperands(string expression)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckUnexpectedOperatorOperands(tokens);
    }

    public FunctionNamesCheckResult CheckFunctionNames(
        string expression,
        IFunctionDescriptors functionDescriptors)
    {
        var tokens = GetInfixTokens(expression);
        return _tokenizerValidator.CheckFunctionNames(tokens, functionDescriptors);
    }

    // Full validation report (convenience method if no Parser validation is needed)
    public TokenizerValidationReport Validate(
        string expression,
        VariableNamesOptions nameOptions,
        IFunctionDescriptors? functionDescriptors = null,
        bool earlyReturnOnErrors = false)
    {

        TokenizerValidationReport report = new() { Expression = expression };
        if (string.IsNullOrWhiteSpace(expression)) return report;

        try
        {
            var parenthesesResult = _tokenizerValidator.CheckParentheses(expression);
            if (!parenthesesResult.IsSuccess)
            {
                report.ParenthesesResult = parenthesesResult;
                return report;
            }

            //calculate the infix tokens only if we need to check variable names
            List<Token> infixTokens;
            try
            {
                infixTokens = GetInfixTokens(expression);
            }
            catch (Exception ex)
            {
                report.Exception = ParserCompileException.InfixException(ex);
                return report;
            }

            var infixReport = _tokenizerValidator.ValidateInfixStage(
                infixTokens,
                nameOptions,
                functionDescriptors,
                earlyReturnOnErrors);
            report.VariableNamesResult = infixReport.VariableNamesResult;
            report.FunctionNamesResult = infixReport.FunctionNamesResult;
            report.UnexpectedOperatorOperandsResult = infixReport.UnexpectedOperatorOperandsResult;

            //put infix tokens in the report for further use
            report.InfixTokens = infixTokens;
            return report;
        }
        catch (Exception ex) //unexpected tokenizer error
        {
            report.Exception = ParserCompileException.TokenizerException(ex);
            return report;
        }
    }
    #endregion

}