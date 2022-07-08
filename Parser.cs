using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserTests.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests;


public class Parser
{
    private readonly ILogger<Parser> _logger;
    private readonly ITokenizer _tokenizer;
    private readonly TokenizerOptions _options;

    public Parser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options)
    {
        _logger = logger;
        _tokenizer = tokenizer;
        _options = options.Value;
    }

    public Tree<Token> Parse(string s)
    {
        var postfixTokens = GetPostfixTokens(s);

        return GetExpressionTree(postfixTokens);
    }

    public Tree<Token> GetExpressionTree(List<Token> postfixTokens)
    {
        _logger.LogDebug("Building expresion tree from postfix tokens...");

        //build expression tree from postfix 
        Stack<Token> stack = new();

        Dictionary<Token, Node<Token>> nodeDictionary = new();
        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in postfixTokens)
        {
            if (stack.Count < 2 ||
                token.TokenType == Token.LiteralTokenType ||
                token.TokenType == Token.IdentifierTokenType)
            {
                nodeDictionary.Add(token, new Node<Token>(token));
                stack.Push(token);
                _logger.LogDebug("Push {token} to stack", token);
                continue;
            }

            //else it is an operator
            Node<Token> operatorNode = new(token);
            //pop the 2 items on the current stack
            Token rightToken = stack.Pop(), leftToken = stack.Pop();
            operatorNode.Right = nodeDictionary[rightToken];
            operatorNode.Left = nodeDictionary[leftToken];
            _logger.LogDebug("Pop {rightToken} from stack (right child)", rightToken);
            _logger.LogDebug("Pop {leftToken} from stack (left child)", leftToken);



            //remember the operatorNode
            nodeDictionary.Add(token, operatorNode);
            //and push to the stack
            stack.Push(token);
            _logger.LogDebug("Pushing {token} from stack (operator node)", token);
        }

        if (stack.Count > 1)
        {
            string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Value));
            _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
            throw new InvalidOperationException(
                string.Format("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {0}", stackItemsString));
        }

        //the last item on the postfix expression is the root node
        Tree<Token> tree = new Tree<Token>() { Root = nodeDictionary[stack.Pop()] };
        tree.NodeDictionary = nodeDictionary;
        return tree;
    }

    //https://www.youtube.com/watch?v=PAceaOSnxQs

    public List<Token> GetPostfixTokens(string s)
    {
        var infixTokens = _tokenizer.Tokenize(s);
        return GetPostfixTokens(infixTokens);
    }

    public List<Token> GetPostfixTokens(List<Token> infixTokens)
    {
        List<Token> postfixTokens = new();
        Stack<Token> operatorStack = new();

        void LogState() => _logger.LogDebug("OP STACK: {stack}, POSTFIX {postfix}",
                    //reverse the operator stack so that the head is the last element
                    operatorStack.Any() ? string.Join(" ", operatorStack.Reverse().Select(o => o.Value)) : "<empty>",
                    postfixTokens.Any() ? string.Join(" ", postfixTokens.Select(o => o.Value)) : "<empty>");

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
            else if (token.TokenType == Token.OpenParenthesisTokenType)
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
                } while (true);
            }


            else //operator
            {
                var currentOperator = operators[token.Value]!;
                while (operatorStack.Any())
                {
                    var currentHead = operatorStack.Peek();
                    if (currentHead.TokenType == Token.OpenParenthesisTokenType)
                    {
                        //this is equivalent to having an empty stack
                        operatorStack.Push(token);
                        _logger.LogDebug("Push to stack (after open parenthesis) -> {token}", token);
                        LogState();
                        goto NextToken;
                    }

                    var currentHeadOperator = operators[currentHead.Value]!;

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
            if (stackToken.TokenType == Token.OpenParenthesisTokenType)
            {
                _logger.LogError("Unmatched open parenthesis. A closed parenthesis should follow.");
                throw new InvalidOperationException($"Unmatched open parenthesis (open parenthesis at {stackToken.Match.Index})");
            }

            postfixTokens.Add(stackToken);
            _logger.LogDebug("Pop stack to postfix expression -> {token}", stackToken);
            LogState();

        }
        return postfixTokens;
    }

}
