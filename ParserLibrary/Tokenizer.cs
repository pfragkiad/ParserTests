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
            tokens.AddRange(matches.Select(m => new Token(TokenType.Identifier, m)));

        //literals
        matches = Regex.Matches(expression, _options.TokenPatterns.Literal);
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(TokenType.Literal, m)));

        //open parenthesis
        matches = Regex.Matches(expression, $@"\{_options.TokenPatterns.OpenParenthesis}");
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(TokenType.OpenParenthesis, m)));

        //open parenthesis with identifier
        string functionPattern = $@"(?<identifier>{_options.TokenPatterns.Identifier}\s*)(?<par>\{_options.TokenPatterns.OpenParenthesis})";
        matches = Regex.Matches(expression, functionPattern);
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(TokenType.Function, m)));

        //close parenthesis
        matches = Regex.Matches(expression, $@"\{_options.TokenPatterns.CloseParenthesis}");
        if (matches.Any())
            tokens.AddRange(matches.Select(m => new Token(TokenType.ClosedParenthesis, m)));

        //match unary operators that do NOT coincide with binary operators
        var uniqueUnary =
            _options.TokenPatterns.Unary?.Where(u => !_options.TokenPatterns.OperatorDictionary.ContainsKey(u.Name));
        if (uniqueUnary?.Any() ?? false)
        {
            foreach (var op in uniqueUnary)
            {
                matches = Regex.Matches(expression, $@"\{op.Name}");
                if (matches.Any())
                    tokens.AddRange(matches.Select(m => new Token(TokenType.OperatorUnary, m)));
            }
        }

        //operators
        foreach (var op in _options.TokenPatterns.Operators)
        {
            matches = Regex.Matches(expression, $@"\{op.Name}");
            if (matches.Any())
                tokens.AddRange(matches.Select(m => new Token(TokenType.Operator, m)));
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
            if (tokens[i].TokenType == TokenType.Function)
            {
                tokens.RemoveAt(i + 1); //this is the plain open parenthesis
                tokens.RemoveAt(i); //remote this identifier and keep the plain one without the parenthesis
                tokens[i - 1].TokenType = TokenType.Function; //this is the plain identifier
                i--; //current token index is i-1, so counter is readjusted
            }
        }

        //search for unary operators (assuming they are the same with binary operators)
        //TODO: Search for non-common unary operators (check for postfix/prefix)

        var potentialUnaryOperators = tokens.Where(t =>
        t.TokenType == TokenType.Operator && _options.TokenPatterns.Unary.Any(u => u.Name == t.Text));
        foreach (var token in potentialUnaryOperators)
        {
            int tokenIndex = tokens.IndexOf(token);
            var unary = _options.TokenPatterns.Unary.First(u => u.Name == token.Text);

            //if the previous token is an operator then the latter is a unary!
            if (tokenIndex == 0 ||
                (tokens[tokenIndex - 1].TokenType == TokenType.Operator ||
                tokens[tokenIndex - 1].TokenType == TokenType.OperatorUnary ||
                tokens[tokenIndex - 1].TokenType == TokenType.OpenParenthesis ||
                tokens[tokenIndex - 1].TokenType == TokenType.Function
                ) && unary.Prefix)
                token.TokenType = TokenType.OperatorUnary;
        }

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
        var unary = _options.TokenPatterns.UnaryOperatorDictionary;

        _logger.LogDebug("Retrieving postfix tokens...");
        foreach (var token in infixTokens)
        {
            //if(token.Text == "asd") Debugger.Break();

            if (token.TokenType == TokenType.Literal || token.TokenType == TokenType.Identifier)
            {
                postfixTokens.Add(token);
                _logger.LogDebug("Push to postfix expression -> {token}", token);
                LogState();
            }
            else if (token.TokenType == TokenType.OpenParenthesis
                || token.TokenType == TokenType.Function) //XTRA!
            {
                operatorStack.Push(token);
                _logger.LogDebug("Push to stack (open parenthesis) -> {token}", token);
                LogState();
            }
            else if (token.TokenType == TokenType.ClosedParenthesis)
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
                    if (stackToken.TokenType == TokenType.OpenParenthesis) break;
                    postfixTokens.Add(stackToken);
                    _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
                    LogState();
                    if (stackToken.TokenType == TokenType.Function) break;
                } while (true);
            }

            else  //operator or unary operator
            {

                Operator? currentOperator = null;
                UnaryOperator? currentUnaryOperator = null;

                if (token.TokenType == TokenType.Operator)
                    currentOperator = operators[token.Text]!;
                else if (token.TokenType == TokenType.OperatorUnary)
                    currentUnaryOperator = unary[token.Text!];

                while (operatorStack.Any())
                {
                    Token currentHead = operatorStack.Peek();
                    if (currentHead.TokenType == TokenType.OpenParenthesis ||
                        currentHead.TokenType == TokenType.Function)
                    {
                        //this is equivalent to having an empty stack
                        operatorStack.Push(token);
                        _logger.LogDebug("Push to stack (after open parenthesis) -> {token}", token);
                        LogState();
                        goto NextToken;
                    }

                    Operator? currentHeadOperator = null;
                    UnaryOperator? currentHeadUnaryOperator = null;
                    if (currentHead.TokenType == TokenType.Operator)
                        currentHeadOperator = operators[currentHead.Text]!;
                    else if (currentHead.TokenType == TokenType.OperatorUnary)
                        currentHeadUnaryOperator = unary[currentHead.Text!];

                    int? currentHeadPriority = currentHeadOperator?.Priority ?? currentHeadUnaryOperator?.Priority;


                    //for higher priority push to the stack!
                    if (currentOperator is not null
                        && (currentOperator.Priority > currentHeadPriority || currentOperator.Priority == currentHeadPriority && !currentOperator.LeftToRight)
                       ||
                       currentUnaryOperator is not null
                       && currentUnaryOperator.Priority >= currentHeadPriority)
                    {
                        operatorStack.Push(token);
                        _logger.LogDebug("Push to stack (op with high priority) -> {token}", token);
                        LogState();
                        goto NextToken;
                    }

                    var stackToken = operatorStack.Pop();
                    postfixTokens.Add(stackToken);

                    //remove op from stack and put to postfix  
                    _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
                    LogState();

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
            if (stackToken.TokenType == TokenType.OpenParenthesis ||
                stackToken.TokenType == TokenType.Function)
            {
                _logger.LogError("Unmatched open parenthesis. A closed parenthesis should follow.");
                throw new InvalidOperationException($"Unmatched open parenthesis (open parenthesis at {stackToken.Match.Index})");
            }
            if (stackToken.TokenType == TokenType.Function) //XTRA
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
