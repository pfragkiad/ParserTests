﻿using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Tokenizers;

public class Tokenizer : ITokenizer
{
    protected readonly ILogger _logger;
    protected readonly TokenizerOptions _options;
    protected Operator? ArgumentOperator;

    public Tokenizer(ILogger<Tokenizer> logger, IOptions<TokenizerOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        if (_options.TokenPatterns is null) _options = TokenizerOptions.Default;

    }

    protected Tokenizer(ILogger logger, IOptions<TokenizerOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        if (_options.TokenPatterns is null) _options = TokenizerOptions.Default;

    }


    //The second property contains the number of arguments needed by each corresponding function.
    public List<Token> GetInfixTokens(string expression)
    {
        //inspiration: https://gwerren.com/Blog/Posts/simpleCSharpTokenizerUsingRegex

        _logger.LogDebug("Retrieving infix tokens...");

        List<Token> tokens = [];
        MatchCollection matches;

        TokenPatterns tokenPatterns = _options.TokenPatterns;

        //open parenthesis with identifier (for functions)
        string functionPattern = $@"(?<identifier>{tokenPatterns.Identifier}\s*)(?<par>\{tokenPatterns.OpenParenthesis})";
        matches =
            _options.CaseSensitive ?
            Regex.Matches(expression, functionPattern) :
            Regex.Matches(expression, functionPattern, RegexOptions.IgnoreCase);
        // Track function call positions to avoid duplicate parentheses
        // Track function names to avoid adding them as identifiers later
        HashSet<string> functionNames = [];
        HashSet<int> functionParenthesisPositions = [];
        if (matches.Count != 0)
        {
            //  tokens.AddRange(matches.Select(m => new Token(TokenType.Function, m)));
            foreach (Match m in matches.Cast<Match>())
            {
                Group idGroup = m.Groups["identifier"];
                Group parGroup = m.Groups["par"];

                // Create function token
                tokens.Add(new Token(TokenType.Function, idGroup.Value, idGroup.Index));

                // Store function name to avoid adding it as an identifier later
                functionNames.Add(idGroup.Value.Trim());

                // Mark the parenthesis position as part of a function
                functionParenthesisPositions.Add(parGroup.Index);
            }
        }

        //identifiers
        matches =
            _options.CaseSensitive ?
            Regex.Matches(expression, tokenPatterns.Identifier!) :
            Regex.Matches(expression, tokenPatterns.Identifier!, RegexOptions.IgnoreCase);
        if (matches.Count > 0)
        {
            //tokens.AddRange(matches.Select(m => new Token(TokenType.Identifier, m))); 
            // Only add identifiers that aren't already in the functionNames collection
            tokens.AddRange(matches
                .Where(m => !functionNames.Contains(m.Value.Trim()))
                .Select(m => new Token(TokenType.Identifier, m)));
        }

        //literals
        matches = Regex.Matches(expression, tokenPatterns.Literal!);
        if (matches.Count != 0)
            tokens.AddRange(matches.Select(m => new Token(TokenType.Literal, m)));


        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];
            //if (c == _options.TokenPatterns.OpenParenthesis)
            if (c == tokenPatterns.OpenParenthesis && !functionParenthesisPositions.Contains(i))
                tokens.Add(new Token(TokenType.OpenParenthesis, c, i));
            else if (c == tokenPatterns.CloseParenthesis)
                tokens.Add(new Token(TokenType.ClosedParenthesis, c, i));
        }

        //allow any string as argument separator
        matches = Regex.Matches(expression, Regex.Escape(tokenPatterns.ArgumentSeparator!));
        if (matches.Count != 0)
            tokens.AddRange(matches.Select(m => new Token(TokenType.ArgumentSeparator, m)));


        //match unary operators that do NOT coincide with binary operators
        var uniqueUnary =
            tokenPatterns.Unary.Where(u => !tokenPatterns.OperatorDictionary.ContainsKey(u.Name));
        if (uniqueUnary.Any())
        {
            foreach (var op in uniqueUnary)
            {
                matches = Regex.Matches(expression, $@"\{op.Name}");
                if (matches.Count != 0)
                    tokens.AddRange(matches.Select(m => new Token(TokenType.OperatorUnary, m)));
            }
        }

        //operators
        foreach (var op in tokenPatterns.Operators ?? [])
        {
            //matches = Regex.Matches(expression, $@"\{op.Name}");
            matches = Regex.Matches(expression, Regex.Escape(op.Name));
            if (matches.Count != 0)
                tokens.AddRange(matches.Select(m => new Token(TokenType.Operator, m)));
        }

        //sort by Match.Index (get "infix ordering")
        tokens.Sort();

        ////for each captured function we remove one captured open parenthesis AND one identifier! (the tokens must be sorted)
        ////this should be refactored - 
        //for (int i = tokens.Count - 2; i >= 1; i--)
        //{
        //    //if (tokens[i].TokenType == Token.FunctionOpenParenthesisTokenType)
        //    //{
        //    //    tokens.RemoveAt(i + 1); //this is the plain open parenthesis
        //    //    tokens.RemoveAt(i - 1); //this is the plain identifier
        //    //    i--; //current token index is i-1, so counter is readjusted
        //    //}
        //    if (tokens[i].TokenType == TokenType.Function)
        //    {
        //        tokens.RemoveAt(i + 1); //this is the plain open parenthesis
        //        tokens.RemoveAt(i); //remove this identifier and keep the plain one without the parenthesis
        //        tokens[i - 1].TokenType = TokenType.Function; //this is the plain identifier
        //        i--; //current token index is i-1, so counter is readjusted
        //    }
        //}

        //search for unary operators (assuming they are the same with binary operators)
        var potentialUnaryOperators = tokens.Where(t =>
        t.TokenType == TokenType.Operator && tokenPatterns.Unary.Any(u => u.Name == t.Text));
        //foreach (var token in potentialUnaryOperators)
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            // Only process operators that could be unary
            if (token.TokenType != TokenType.Operator ||
                !tokenPatterns.Unary.Any(u => u.Name == token.Text))
                continue;

            //int i = tokens.IndexOf(token);
            var unaryOp = tokenPatterns.Unary.First(u => u.Name == token.Text);


            if (unaryOp.Prefix)
            {
                var prevToken = i > 0 ? tokens[i - 1] : null;

                //if the previous token is an operator then the latter is a unary!
                TokenType? previousTokenType = i > 0 ? prevToken!.TokenType : null;
                if (i == 0 ||
                    previousTokenType == TokenType.Operator ||
                    previousTokenType == TokenType.ArgumentSeparator ||
                    //the previous unary operator must not be prefix!
                    previousTokenType == TokenType.OperatorUnary 
                        //&&  _options.TokenPatterns.UnaryOperatorDictionary[prevToken!.Text].Prefix 
                    || previousTokenType == TokenType.OpenParenthesis ||
                    previousTokenType == TokenType.Function)
                    token.TokenType = TokenType.OperatorUnary;

                continue;
            }

            //unary.postfix (needs testing)
            //if the previous token is an operator then the latter is a unary!
            var nextToken = i < tokens.Count - 1 ? tokens[i + 1] : null;
            TokenType? nextTokenType = i < tokens.Count - 1 ? nextToken!.TokenType : null;
            if (i == tokens.Count - 1 ||
                nextTokenType == TokenType.Operator ||
                nextTokenType == TokenType.ArgumentSeparator ||
                //the previous unary operator must not be prefix!
                nextTokenType == TokenType.OperatorUnary
                    //&& !_options.TokenPatterns.UnaryOperatorDictionary[nextToken!.Text].Prefix
                || nextTokenType == TokenType.ClosedParenthesis)
                token.TokenType = TokenType.OperatorUnary;
        }

        //now we need to convert to postfix
        //https://youtu.be/PAceaOSnxQs

        //https://www.techiedelight.com/expression-tree/

        if (_logger is not null)
        {
            foreach (var token in tokens)
                _logger.LogDebug("{token} ({type})", token.Text, token.TokenType);
        }



        return tokens;
    }


    //Infix to postfix 
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

        //foreach (var token in infixTokens)
        for (int iToken = 0; iToken < infixTokens.Count; iToken++)
        {
            Token token = infixTokens[iToken];
            //if(token.Text == "asd") Debugger.Break();

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                postfixTokens.Add(token);
                _logger.LogDebug("Push to postfix expression -> {token}", token);
                LogState();
                continue;
            }

            if (token.TokenType == TokenType.OpenParenthesis
                || token.TokenType == TokenType.Function) //XTRA!
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
                if (iToken > 0 && infixTokens[iToken - 1].TokenType == TokenType.Operator)
                {
                    //add null token to postfix expression
                    postfixTokens.Add(Token.Null);
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


            //here we have operator or unary operator or argument separator
            Operator? currentOperator = null;
            UnaryOperator? currentUnaryOperator = null;

            if (token.TokenType == TokenType.Operator)
                currentOperator = operators[token.Text]!;
            else if (token.TokenType == TokenType.ArgumentSeparator)
                currentOperator = argumentOperator;
            else if (token.TokenType == TokenType.OperatorUnary)
                currentUnaryOperator = unary[token.Text!];


            //ADD NULL OPERAND IF NEEDED-----------------------
            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.ArgumentSeparator)
            {
                //check if previous token is a binary operator (including argument separator)
                //this will save the expression tree from having a non-empty stack
                TokenType? previousTokenType = iToken > 0 ? infixTokens[iToken - 1].TokenType : null;

                if (previousTokenType is null
                    || previousTokenType == TokenType.Operator
                    || previousTokenType == TokenType.ArgumentSeparator
                    || previousTokenType == TokenType.OpenParenthesis
                    || previousTokenType == TokenType.Function)
                {
                    //add null token to postfix expression
                    postfixTokens.Add(Token.Null);
                }
            }
            //-------------------------------------------------


            //if (currentUnaryOperator?.Name == "%") Debugger.Break();

            string message = "";
            while (operatorStack.Count != 0)
            {

                Token currentHead = operatorStack.Peek();
                if (currentHead.TokenType == TokenType.OpenParenthesis ||
                    currentHead.TokenType == TokenType.Function)
                {
                    message = "Push to stack (open parenthesis or function) -> {token}";
                    break;

                    //this is equivalent to having an empty stack
                    //operatorStack.Push(token);
                    //_logger.LogDebug("Push to stack (after open parenthesis) -> {token}", token);
                    //LogState();
                    //goto NextToken;
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

                //for higher priority push to the stack!
                if (currentOperator is not null
                   && (currentOperator.Priority > currentHeadPriority || currentOperator.Priority == currentHeadPriority && !currentOperator.LeftToRight)
                   ||
                   currentUnaryOperator is not null
                   && (currentUnaryOperator.Priority > currentHeadPriority || currentUnaryOperator.Priority == currentHeadPriority && currentUnaryOperator.Prefix))
                {
                    message = "Push to stack (op with high priority) -> {token}";
                    break;
                    //operatorStack.Push(token);
                    //_logger.LogDebug("Push to stack (op with high priority) -> {token}", token);
                    //LogState();
                    //goto NextToken;
                }

                //current priority has lower priority so we pop the stack
                var stackToken = operatorStack.Pop();
                postfixTokens.Add(stackToken);

                //remove op from stack and put to postfix  
                _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
                LogState();
            }

            operatorStack.Push(token);
            if (operatorStack.Count == 1) //if it was an empty stack
                message = "Push to stack (empty stack) -> {token}";
            _logger.LogDebug(message, token);
            LogState();

            //NextToken:;
        }

        //add dummy node if the expression ends with an operator
        var lastToken = infixTokens[^1];
        if (lastToken.TokenType == TokenType.Operator || lastToken.TokenType == TokenType.ArgumentSeparator)
            postfixTokens.Add(Token.Null);


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

    

    #region Utility methods

    public bool AreParenthesesMatched(string expression)
    {
        var open = _options.TokenPatterns.OpenParenthesis;
        var close = _options.TokenPatterns.CloseParenthesis;

        int count = 0;
        foreach (char c in expression)
        {
            //if (c.ToString() == open)
            if (c == open)
            {
                count++;
                continue;
            }

            //if (c.ToString() != close) continue;
            if (c != close) continue;

            count--;
            if (count < 0)
                return false;
        }
        return count == 0;
    }

    public ParenthesisCheckResult CheckParentheses(string expression)
    {
        var open = _options.TokenPatterns.OpenParenthesis;
        var close = _options.TokenPatterns.CloseParenthesis;

        List<int> unmatchedClosed = [];
        List<int> openPositions = [];

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            //if (c.ToString() == open)
            if (c == open)
            {
                openPositions.Add(i);
                continue;
            }

            //if (c.ToString() != close) continue;
            if (c != close)
                continue;

            if (openPositions.Count == 0)
            {
                unmatchedClosed.Add(i);
            }
            else
            {
                openPositions.RemoveAt(openPositions.Count - 1);
            }
        }

        return new ParenthesisCheckResult
        {
            UnmatchedClosed = unmatchedClosed,
            UnmatchedOpen = openPositions
        };
    }


    public List<string> GetVariableNames(string expression)
    {
        //returns the identifiers in the expression
        var tokens = GetInfixTokens(expression);
        return [.. tokens
            .Where(t => t.TokenType == TokenType.Identifier)
            .Select(t => t.Text)
            .Distinct()];
    }

    public VariableNamesCheckResult CheckVariableNames(HashSet<string> identifierNames, string expression,
        string[] ignorePrefixes, string[] ignorePostfixes)
    {
        var tokens = GetInfixTokens(expression);

        HashSet<string> matchedNames = [];
        HashSet<string> unmatchedNames = [];
        HashSet<string> ignoredNames = [];

        foreach (var t in tokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (
                identifierNames.Contains(t.Text))
            {
                matchedNames.Add(t.Text);
                continue;
            }

            if (ignorePrefixes.Any(p => t.Text.StartsWith(p) ||
                ignorePostfixes.Any(s => t.Text.EndsWith(s))))
            {
                ignoredNames.Add(t.Text);
                continue;
            }

            unmatchedNames.Add(t.Text);
        }

        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matchedNames],
            UnmatchedNames = [.. unmatchedNames],
            IgnoredNames = [.. ignoredNames]
        };
    }

    public VariableNamesCheckResult CheckVariableNames(HashSet<string> identifierNames, string expression, Regex? ignoreIdentifierPattern = null)
    {
        //returns the identifiers in the expression
        var tokens = GetInfixTokens(expression);
        HashSet<string> matchedNames = [];
        HashSet<string> unmatchedNames = [];
        HashSet<string> ignoredNames = [];
        foreach (var t in tokens.Where(t => t.TokenType == TokenType.Identifier))
        {
            if (identifierNames.Contains(t.Text))
            {
                matchedNames.Add(t.Text);
                continue;
            }

            if (ignoreIdentifierPattern is not null && ignoreIdentifierPattern.IsMatch(t.Text))
            {
                ignoredNames.Add(t.Text);
                continue;
            }

            unmatchedNames.Add(t.Text);
        }
        return new VariableNamesCheckResult
        {
            MatchedNames = [.. matchedNames],
            UnmatchedNames = [.. unmatchedNames],
            IgnoredNames = [.. ignoredNames]
        };
    }



    #endregion


}
