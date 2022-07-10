using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParserLibrary;

public class Tokenizer : ITokenizer
{
    private readonly ILogger<Tokenizer> _logger;
    private readonly TokenizerOptions _options;

    public Tokenizer(ILogger<Tokenizer> logger, IOptions<TokenizerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    //The second property contains the number of arguments needed by each corresponding function.
    public List<Token> GetInOrderTokens(string expression)
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

        //open parenthesis with identifier
        string functionPattern = $@"(?<identifier>{_options.TokenPatterns.Identifier}\s*)(?<par>\{_options.TokenPatterns.OpenParenthesis})";
        matches = Regex.Matches(expression, functionPattern);
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(Token.FunctionOpenParenthesisTokenType, m)));

        //close parenthesis
        matches = Regex.Matches(expression, $@"\{_options.TokenPatterns.CloseParenthesis}");
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(Token.CloseParenthesisTokenType, m)));

        ////argument separators
        //matches = Regex.Matches(expression, $@"\{_options.TokenPatterns.ArgumentSeparator}");
        //if (matches.Any())
        //    tokens.AddRange(matches.Select(m => new Token(Token.ArgumentSeparatorTokenType, m)));

        //operators
        foreach (var op in _options.TokenPatterns.Operators)
        {
            matches = Regex.Matches(expression, $@"\{op.Name}");
            if (matches.Any())
                tokens.AddRange(matches.Select(m => new Token(Token.OperatorTokenType, m)));
        }

        //sort by Match.Index (get "infix ordering")
        tokens.Sort();

        //for each captured function we should remove one captured open parenthesis and one identifier! (the tokens must be sorted)
        for (int i = tokens.Count - 2; i >= 1; i--)
        {
            //if (tokens[i].TokenType == Token.FunctionOpenParenthesisTokenType)
            //{
            //    tokens.RemoveAt(i + 1); //this is the plain open parenthesis
            //    tokens.RemoveAt(i - 1); //this is the plain identifier
            //    i--; //current token index is i-1, so counter is readjusted
            //}
            if (tokens[i].TokenType == Token.FunctionOpenParenthesisTokenType)
            {
                tokens.RemoveAt(i + 1); //this is the plain open parenthesis
                tokens.RemoveAt(i); //remote this identifier and keep the plain one without the parenthesis
                tokens[i - 1].TokenType = Token.FunctionOpenParenthesisTokenType; //this is the plain identifier
                i--; //current token index is i-1, so counter is readjusted
            }
        }

        //search for unary operators (assuming they are the same with binary operators)
        //TODO: Search for non-common unary operators (check for postfix/prefix)

        var potentialUnaryOperators = tokens.Where(t =>
        t.TokenType == Token.OperatorTokenType && _options.TokenPatterns.Unary.Any(u => u.Name == t.Text));
        foreach (var token in potentialUnaryOperators)
        {
            int tokenIndex = tokens.IndexOf(token);
            var unary = _options.TokenPatterns.Unary.First(u => u.Name == token.Text);

            //if the previous token is an operator then the latter is a unary!
            if (tokenIndex == 0 ||
                (tokens[tokenIndex - 1].TokenType == Token.OperatorTokenType ||
                tokens[tokenIndex - 1].TokenType == Token.OperatorUnaryTokenType ||
                tokens[tokenIndex - 1].TokenType == Token.OpenParenthesisTokenType ||
                tokens[tokenIndex - 1].TokenType == Token.FunctionOpenParenthesisTokenType
                ) && unary.Prefix)
                token.TokenType = Token.OperatorUnaryTokenType;
        }

        //now we need to convert to postfix
        //https://youtu.be/PAceaOSnxQs

        //https://www.techiedelight.com/expression-tree/

        if (_logger is not null)
        {
            foreach (var token in tokens)
                _logger.LogDebug("{token} ({type})", token.Match.Value, token.TokenType);
        }

        //SHOULD BE REMOVED-------------------------------------------------------
        //Dictionary<Token, int> functionArgumentsCount = new();
        //var functionTokens = tokens.Where(t => t.TokenType == Token.FunctionOpenParenthesisTokenType).ToList();
        //foreach (Token token in functionTokens)
        //{
        //    int tokenIndex = tokens.IndexOf(token);
        //    int nextParenthesisIndex = tokens.FindIndex(tokenIndex, t => t.TokenType == Token.CloseParenthesisTokenType);

        //    int argumentsCount = 0;
        //    if (nextParenthesisIndex == token.Index + 1)
        //        argumentsCount = 0;
        //    else
        //    {
        //        argumentsCount = 1 + 
        //            tokens.Where(t=>
        //            t.Index<nextParenthesisIndex && t.Index>token.Index && t.TokenType==Token.ArgumentSeparatorTokenType)?.Count() ??0;
        //    }

        //    if (!functionArgumentsCount.ContainsKey(token))
        //    {
        //        functionArgumentsCount.Add(token, argumentsCount);

        //        if (argumentsCount > 2)
        //            _logger.LogWarning("There are {count} > 2 arguments used by the function {function}!", argumentsCount,token.Text);
        //    }
        //    else //there is already a function defined, check that the arguments count is the same or throw an exception!
        //    {
        //        if (functionArgumentsCount[token] != argumentsCount)
        //        {
        //            _logger.LogError("The number of arguments for the function {function} at position {position} should be {count}.",
        //                token.Text.TrimEnd('('), token.Index, functionArgumentsCount[token]);
        //            throw new InvalidOperationException($"The number of arguments for the function {token.Text.TrimEnd('(')} at position {token.Index} should be {functionArgumentsCount[token]}.");
        //        }
        //    }
        //}

        return tokens;
    }


    //Infix to postfix 
    //https://www.youtube.com/watch?v=PAceaOSnxQs
    public List<Token> GetPostfixTokens(List<Token> infixTokens)
    {
        List<Token> postfixTokens = new();
        Stack<Token> operatorStack = new();

        void LogState() => _logger.LogDebug("OP STACK: {stack}, POSTFIX {postfix}",
                    //reverse the operator stack so that the head is the last element
                    operatorStack.Any() ? string.Join(" ", operatorStack.Reverse().Select(o => o.Text)) : "<empty>",
                    postfixTokens.Any() ? string.Join(" ", postfixTokens.Select(o => o.Text)) : "<empty>");

        var operators = _options.TokenPatterns.OperatorDictionary;

        _logger.LogDebug("Retrieving postfix tokens...");
        foreach (var token in infixTokens)
        {
            if (token.TokenType == Token.LiteralTokenType || token.TokenType == Token.IdentifierTokenType)
            {
                postfixTokens.Add(token);
                _logger.LogDebug("Push to postfix expression -> {token}", token);
                LogState();
            }
            else if (token.TokenType == Token.OpenParenthesisTokenType
                || token.TokenType == Token.FunctionOpenParenthesisTokenType) //XTRA!
            {
                operatorStack.Push(token);
                _logger.LogDebug("Push to stack (open parenthesis) -> {token}", token);
                LogState();
            }
            else if (token.TokenType == Token.CloseParenthesisTokenType)
            {
                _logger.LogDebug("Pop stack until open parenthesis is found (close parenthesis) -> {token}", token);

                //pop all operators until we find open parenthesis
                do
                {
                    if (!operatorStack.Any())
                    {
                        _logger.LogError("Unmatched closed parenthesis. An open parenthesis should precede.");
                        throw new InvalidOperationException($"Unmatched closed parenthesis (closed parenthesis at {token.Match.Index})");
                    }
                    var stackToken = operatorStack.Pop();
                    if (stackToken.TokenType == Token.OpenParenthesisTokenType) break;
                    postfixTokens.Add(stackToken);
                    _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
                    LogState();
                    if (stackToken.TokenType == Token.FunctionOpenParenthesisTokenType) break;
                } while (true);
            }


            else //operator
            {
                var currentOperator = operators[token.Text]!;
                while (operatorStack.Any())
                {
                    var currentHead = operatorStack.Peek();
                    if (currentHead.TokenType == Token.OpenParenthesisTokenType ||
                        currentHead.TokenType == Token.FunctionOpenParenthesisTokenType)
                    {
                        //this is equivalent to having an empty stack
                        operatorStack.Push(token);
                        _logger.LogDebug("Push to stack (after open parenthesis) -> {token}", token);
                        LogState();
                        goto NextToken;
                    }

                    var currentHeadOperator = operators[currentHead.Text]!;

                    //for higher priority push to the stack!
                    if (currentOperator.Priority > currentHeadOperator.Priority ||
                       currentOperator.Priority == currentHeadOperator.Priority && !currentOperator.LeftToRight)
                    {
                        operatorStack.Push(token);
                        _logger.LogDebug("Push to stack (op with high priority) -> {token}", token);
                        LogState();
                        goto NextToken;
                    }
                    else
                    {
                        var stackToken = operatorStack.Pop();
                        postfixTokens.Add(stackToken);

                        //remove op from stack and put to postfix  
                        _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
                        LogState();
                    }
                }

                //if the stack is empty just push the operator
                operatorStack.Push(token);
                _logger.LogDebug("Push to stack (empty stack) -> {token}", token);
                LogState();
            }
        NextToken:;
        }

        //check the operator stack at the end
        while (operatorStack.Any())
        {
            var stackToken = operatorStack.Pop();
            if (stackToken.TokenType == Token.OpenParenthesisTokenType ||
                stackToken.TokenType == Token.FunctionOpenParenthesisTokenType)
            {
                _logger.LogError("Unmatched open parenthesis. A closed parenthesis should follow.");
                throw new InvalidOperationException($"Unmatched open parenthesis (open parenthesis at {stackToken.Match.Index})");
            }
            if (stackToken.TokenType == Token.FunctionOpenParenthesisTokenType) //XTRA
            {
                _logger.LogError("Unmatched function open parenthesis. A closed parenthesis should follow.");
                throw new InvalidOperationException($"Unmatched function open parenthesis (open parenthesis at {stackToken.Match.Index})");
            }

            postfixTokens.Add(stackToken);
            _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
            LogState();

        }
        return postfixTokens;
    }

}
