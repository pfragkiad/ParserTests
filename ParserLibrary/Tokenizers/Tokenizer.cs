using Microsoft.Extensions.DependencyInjection;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers.CheckResults;
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

        if (_options.TokenPatterns is null) _options = TokenizerOptions.Default;

        _tokenizerValidator = tokenizerValidator ?? throw new ArgumentNullException(nameof(tokenizerValidator));
    }


    protected readonly TokenizerOptions _options;
    public TokenizerOptions TokenizerOptions => _options;


    //The second property contains the number of arguments needed by each corresponding function.
    public List<Token> GetInfixTokens(string expression)
    {
        _logger.LogDebug("Retrieving infix tokens...");

        List<Token> tokens = [];
        MatchCollection matches;

        TokenPatterns tokenPatterns = _options.TokenPatterns;

        // Track function '(' positions to avoid duplicate '(' tokens
        HashSet<int> functionParenthesisPositions = [];

        // Identify identifiers and function calls without a regex for "identifier + '('"
        matches =
            _options.CaseSensitive
                ? Regex.Matches(expression, tokenPatterns.Identifier!)
                : Regex.Matches(expression, tokenPatterns.Identifier!, RegexOptions.IgnoreCase);

        if (matches.Count > 0)
        {
            foreach (Match m in matches.Cast<Match>())
            {
                int i = m.Index + m.Length;
                // Skip optional whitespace between identifier and '('
                while (i < expression.Length && char.IsWhiteSpace(expression[i])) i++;

                if (i < expression.Length && expression[i] == tokenPatterns.OpenParenthesis)
                {
                    // Function: add function token and remember '(' position to avoid re-adding it
                    tokens.Add(new Token(TokenType.Function, m.Value, m.Index));
                    functionParenthesisPositions.Add(i);
                    continue;
                }

                // Plain identifier
                tokens.Add(new Token(TokenType.Identifier, m));
            }
        }

        // literals
        matches = Regex.Matches(expression, tokenPatterns.Literal!);
        if (matches.Count != 0)
            tokens.AddRange(matches.Select(m => new Token(TokenType.Literal, m)));

        // parentheses and argument separators
        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];
            if (c == tokenPatterns.OpenParenthesis && !functionParenthesisPositions.Contains(i))
                tokens.Add(new Token(TokenType.OpenParenthesis, c, i));
            else if (c == tokenPatterns.CloseParenthesis)
                tokens.Add(new Token(TokenType.ClosedParenthesis, c, i));
            else if( c== tokenPatterns.ArgumentSeparator)
                tokens.Add(new Token(TokenType.ArgumentSeparator, c, i));
        }

        // match unary operators that do NOT coincide with binary operators
        var uniqueUnary = tokenPatterns.Unary.Where(u => !tokenPatterns.OperatorDictionary.ContainsKey(u.Name));
        if (uniqueUnary.Any())
        {
            foreach (var op in uniqueUnary)
            {
                matches = Regex.Matches(expression, Regex.Escape(op.Name));
                if (matches.Count != 0)
                    tokens.AddRange(matches.Select(m => new Token(TokenType.OperatorUnary, m)));
            }
        }

        // operators
        foreach (var op in tokenPatterns.Operators ?? [])
        {
            matches = Regex.Matches(expression, Regex.Escape(op.Name));
            if (matches.Count != 0)
                tokens.AddRange(matches.Select(m => new Token(TokenType.Operator, m)));
        }

        // sort by index (infix ordering)
        tokens.Sort();

        FixUnaryOperators(tokens);

        if (_logger is not null)
        {
            foreach (var token in tokens)
                _logger.LogDebug("{token} ({type})", token.Text, token.TokenType);
        }

        return tokens;
    }

    private void FixUnaryOperators(List<Token> tokens)
    {
        var unaryDictionary = _options.TokenPatterns.UnaryOperatorDictionary;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.TokenType != TokenType.Operator) continue;

            // Only process operators that could be unary
            bool foundSameUnary = _options.TokenPatterns.UnaryOperatorDictionary.TryGetValue(token.Text, out UnaryOperator? matchedUnaryOp);
            if (!foundSameUnary) continue;

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

        void LogState() => _logger.LogDebug("OP STACK: {stack}, POSTFIX {postfix}",
                    //reverse the operator stack so that the head is the last element
                    operatorStack.Count != 0 ? string.Join(" ", operatorStack.Reverse().Select(o => o.Text)) : "<empty>",
                    postfixTokens.Count != 0 ? string.Join(" ", postfixTokens.Select(o => o.Text)) : "<empty>");

        var operators = _options.TokenPatterns.OperatorDictionary;
        var unary = _options.TokenPatterns.UnaryOperatorDictionary;
        var argumentOperator = _options.TokenPatterns.ArgumentSeparatorOperator;

        _logger.LogDebug("Retrieving postfix tokens...");

        for (int iToken = 0; iToken < infixTokens.Count; iToken++)
        {
            Token token = infixTokens[iToken];

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                postfixTokens.Add(token);
                _logger.LogDebug("Push to postfix expression -> {token}", token);
                LogState();
                continue;
            }

            if (token.TokenType == TokenType.OpenParenthesis
                || token.TokenType == TokenType.Function)
            {
                operatorStack.Push(token);
                _logger.LogDebug("Push to stack (open parenthesis) -> {token}", token);
                LogState();
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
                         && _options.TokenPatterns.UnaryOperatorDictionary[previousToken.Text].Prefix)) // (-)
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
                    LogState();
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

            // ADD NULL OPERAND IF NEEDED
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
                LogState();
            }

            operatorStack.Push(token);
            if (operatorStack.Count == 1)
                message = "Push to stack (empty stack) -> {token}";
            _logger.LogDebug(message, token);
            LogState();
        }

        //add dummy node if the expression ends with an operator or separator
        var lastToken = infixTokens[^1];
        if (lastToken.TokenType == TokenType.Operator            // ... + 
            || lastToken.TokenType == TokenType.ArgumentSeparator // ... ,
            || (lastToken.TokenType == TokenType.OperatorUnary    // ... -
                && _options.TokenPatterns.UnaryOperatorDictionary[lastToken.Text].Prefix)) // prefix-unary at end
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
            LogState();
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
        string[] ignorePrefixes,
        string[] ignorePostfixes)
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
        string[] ignoreCaptureGroups)
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