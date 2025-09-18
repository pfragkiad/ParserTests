using Microsoft.Extensions.DependencyInjection;
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
        if (_patterns.Identifier is null) throw new InvalidOperationException("Identifier pattern cannot be null.");
        if (_patterns.Literal is null) throw new InvalidOperationException("Literal pattern cannot be null.");

        var identifierOptions = _options.CaseSensitive ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.IgnoreCase;
        var literalOptions = identifierOptions; // case sensitivity rule is shared

        _identifierRegex = new Regex(_patterns.Identifier, identifierOptions);
        _identifierGroupNames = [.. _identifierRegex
            .GetGroupNames()
            .Where(n => !int.TryParse(n, out _))];
        _identifierHasNamedGroups = _identifierGroupNames.Length > 0;

        _literalRegex = new Regex(_patterns.Literal, literalOptions);
        _literalGroupNames = [.. _literalRegex
            .GetGroupNames()
            .Where(n => !int.TryParse(n, out _))];
        _literalHasNamedGroups = _literalGroupNames.Length > 0;

        // Precompile per-operator regexes (binary & same-name unary/binary). Keep behavior: case-sensitive.
        _operatorRegexes = [.. (_patterns.Operators ?? []).Select(o => (op: o, regex: new Regex(Regex.Escape(o.Name), RegexOptions.Compiled)))];

        // Precompile unique unary operator regexes (those not sharing name with binary operators)
        _uniqueUnaryOperatorRegexes = _patterns.UniqueUnaryOperators is { Count: > 0 }
            ? [.. _patterns.UniqueUnaryOperators.Select(u => (op: u, regex: new Regex(Regex.Escape(u.Name), RegexOptions.Compiled)))]
            : [];
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

    private static string? FirstSuccessfulNamedGroup(Match m, string[] groupNames)
    {
        foreach (var g in groupNames)
            if (m.Groups[g].Success) return g;
        return null;
    }


    //The second property contains the number of arguments needed by each corresponding function.
    public List<Token> GetInfixTokens(string expression)
    {
        _logger.LogDebug("Retrieving infix tokens...");

        List<Token> tokens = [];
        HashSet<int> functionParenthesisPositions = [];

        // IDENTIFIERS (+ possible functions) - uses cached regex
        MatchCollection idMatches = _identifierRegex.Matches(expression);
        if (idMatches.Count > 0)
        {
            foreach (Match m in idMatches)
            {
                string? group = _identifierHasNamedGroups ? FirstSuccessfulNamedGroup(m, _identifierGroupNames) : null;

                int i = m.Index + m.Length;
                // Skip optional whitespace between identifier and '('
                while (i < expression.Length && char.IsWhiteSpace(expression[i])) i++;

                if (i < expression.Length && expression[i] == _patterns.OpenParenthesis)
                {
                    // Function: add function token and remember '(' position to avoid re-adding it
                    tokens.Add(Token.FromMatch(m, TokenType.Function, group));
                    functionParenthesisPositions.Add(i);
                    continue;
                }

                // Plain identifier
                tokens.Add(Token.FromMatch(m, TokenType.Identifier, group));
            }
        }

        // LITERALS - cached regex
        MatchCollection litMatches = _literalRegex.Matches(expression);
        if (litMatches.Count > 0)
        {
            foreach (Match m in litMatches)
            {
                string? group = _literalHasNamedGroups ? FirstSuccessfulNamedGroup(m, _literalGroupNames) : null;
                tokens.Add(Token.FromMatch(m, TokenType.Literal, group));
            }
        }

        // Parentheses & argument separators (single pass over chars)
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

        // Unique unary operators (precompiled)
        if (_uniqueUnaryOperatorRegexes.Length > 0)
        {
            foreach (var (op, regex) in _uniqueUnaryOperatorRegexes)
            {
                var matches = regex.Matches(expression);
                if (matches.Count == 0) continue;
                foreach (Match m in matches)
                    tokens.Add(Token.FromMatch(m, TokenType.OperatorUnary));
            }
        }

        // Operators (binary or ambiguous)
        if (_operatorRegexes.Length > 0)
        {
            foreach (var (op, regex) in _operatorRegexes)
            {
                var matches = regex.Matches(expression);
                if (matches.Count == 0) continue;
                foreach (Match m in matches)
                    tokens.Add(Token.FromMatch(m, TokenType.Operator));
            }
        }

        // sort by index (infix ordering)
        tokens.Sort();

        FixUnaryOperators(tokens);

        //if (_logger is not null)
        //{
        //    foreach (var token in tokens)
        //        _logger.LogDebug("{token} ({type}) [cg={cg}]", token.Text, token.TokenType, token.CaptureGroup ?? "-");
        //}

        return tokens;
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
        List<Token> postfixTokens = [];
        Stack<Token> operatorStack = new();

        //void LogState() => _logger.LogDebug("OP STACK: {stack}, POSTFIX {postfix}",
        //            //reverse the operator stack so that the head is the last element
        //            operatorStack.Count != 0 ? string.Join(" ", operatorStack.Reverse().Select(o => o.Text)) : "<empty>",
        //            postfixTokens.Count != 0 ? string.Join(" ", postfixTokens.Select(o => o.Text)) : "<empty>");

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
                //LogState();
                continue;
            }

            if (token.TokenType == TokenType.OpenParenthesis
                || token.TokenType == TokenType.Function)
            {
                operatorStack.Push(token);
                _logger.LogDebug("Push to stack (open parenthesis) -> {token}", token);
                //LogState();
                continue;
            }

            if (token.TokenType == TokenType.ClosedParenthesis)
            {
                _logger.LogDebug("Pop stack until open parenthesis is found (close parenthesis) -> {token}", token);

                //check if previous token is a binary operator (including argument separator)
                //this will save the expression tree from having a non-empty stack

                if (iToken > 0)
                {
                    var previousToken = infixTokens[iToken - 1];
                    var previousTokenType = previousToken.TokenType;

                    // +) ,) func()) or (-)  -> add NULL to postfix
                    if (previousTokenType == TokenType.Operator ||   //  +)
                        previousTokenType == TokenType.ArgumentSeparator || // ,)
                        previousTokenType == TokenType.Function ||   // func())
                        (previousTokenType == TokenType.OperatorUnary
                         && unary[previousToken.Text].Prefix)) // (-)
                    {
                        //add null token to postfix expression
                        postfixTokens.Add(Token.Null);
                    }
                }

                //pop all operators until we find open parenthesis
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
                    //LogState();
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

            ////----------------
            //// ADD NULL FOR MISSING RIGHT OPERAND OF PRECEDING BINARY OP
            //// Case: a+!  -> need Null after '+' (before it is popped) AND later Null for '!' itself.
            //// We only add this when:
            ////  - current token is a PREFIX unary operator
            ////  - its own operand is missing (end of expression OR next token cannot start an operand)
            ////  - previous token is a binary operator (TokenType.Operator) that already has a left operand (so we did NOT add a Null above)
            //if (token.TokenType == TokenType.OperatorUnary && currentUnaryOperator is not null && currentUnaryOperator.Prefix)
            //{
            //    bool unaryMissingOperand;
            //    if (iToken == infixTokens.Count - 1)
            //    {
            //        unaryMissingOperand = true; // end of expression
            //    }
            //    else
            //    {
            //        var nextToken = infixTokens[iToken + 1];
            //        var nt = nextToken.TokenType;

            //        bool nextStartsOperand =
            //            nt == TokenType.Literal ||
            //            nt == TokenType.Identifier ||
            //            nt == TokenType.OpenParenthesis ||
            //            nt == TokenType.Function ||
            //            (nt == TokenType.OperatorUnary &&
            //            unary[nextToken.Text].Prefix); // chained prefixes allowed

            //        unaryMissingOperand = !nextStartsOperand;
            //    }

            //    if (unaryMissingOperand && iToken > 0)
            //    {
            //        var previous = infixTokens[iToken - 1];
            //        if (previous.TokenType == TokenType.Operator)
            //        {
            //            // Ensure we did not already emit a Null for the right operand (we wouldn't have,
            //            // because that logic only covers missing LEFT operands).
            //            postfixTokens.Add(Token.Null);
            //        }
            //    }
            //}
            ////----------------

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
                //LogState();
            }

            operatorStack.Push(token);
            if (operatorStack.Count == 1)
                message = "Push to stack (empty stack) -> {token}";
            _logger.LogDebug(message, token);
            //LogState();
        }

        //add dummy node if the expression ends with an operator or separator
        var lastToken = infixTokens[^1];
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
            //LogState();
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

    public ParenthesisCheckResult ValidateParentheses(string expression)
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

    // Full validation report (convenience method if no Parser validation is needed)
    public TokenizerValidationReport Validate(
        string expression,
        VariableNamesOptions nameOptions)
    {
        if (string.IsNullOrWhiteSpace(expression)) return TokenizerValidationReport.Success;
        var parenthesesResult = _tokenizerValidator.CheckParentheses(expression);
        if (!parenthesesResult.IsSuccess)
            return new TokenizerValidationReport
            {
                Expression = expression,
                ParenthesesResult = parenthesesResult
            };

        //calculate the infix tokens only if we need to check variable names
        List<Token> infixTokens = GetInfixTokens(expression);

        var namesResult = _tokenizerValidator.CheckVariableNames(infixTokens, nameOptions);

        return new TokenizerValidationReport
        {
            Expression = expression,
            ParenthesesResult = parenthesesResult,
            VariableNamesResult = namesResult
        };
    }

    #endregion


}