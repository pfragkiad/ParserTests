using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserTests.ExpressionTree;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);

        return GetExpressionTree(postfixTokens);
    }

    public Tree<Token> GetExpressionTree(List<Token> postfixTokens)
    {
        _logger.LogDebug("Building expresion tree from postfix tokens...");

        //build expression tree from postfix 
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = new();

        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in postfixTokens) //these should be PostfixOrder tokens
        {
            if (token.TokenType == Token.FunctionOpenParenthesisTokenType)
            {
                Node<Token> functionNode = new(token);
                //this is the result of an expression (i.e. operator) or a comma operator for multiple arguments
                Token tokenInFunction = stack.Pop();
                functionNode.Right = nodeDictionary[tokenInFunction];
                _logger.LogDebug("Pop {token} from stack (function right child)", tokenInFunction);


                //remember the functionNode
                nodeDictionary.Add(token, functionNode);
                //and push to the stack
                stack.Push(token);
                _logger.LogDebug("Pushing {token} from stack (function node)", token);
                continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (
                stack.Count < 2 || //
                token.TokenType == Token.LiteralTokenType ||
                token.TokenType == Token.IdentifierTokenType)
            {
                nodeDictionary.Add(token, new Node<Token>(token));
                stack.Push(token);
                _logger.LogDebug("Push {token} to stack", token);
                continue;
            }

            //else it is an operator (needs 2 items )
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
            string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Text));
            _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
            throw new InvalidOperationException(
                string.Format("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {0}", stackItemsString));
        }

        //the last item on the postfix expression is the root node
        Tree<Token> tree = new() { Root = nodeDictionary[stack.Pop()] };
        tree.NodeDictionary = nodeDictionary;
        return tree;
    }

    public V Evaluate<V>(
        string s,
        Func<string, V> literalParser,
        Dictionary<string, V> variables,
        Dictionary<string, Func<V, V, V>> operators,
        Dictionary<string, Func<V, V>> funcs1Arg
        )
    {
        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);
        return Evaluate(
            postfixTokens,
            literalParser,variables,operators,funcs1Arg
            );
    }


    public V Evaluate<V>( //code is practically the same with building the expression tree
        List<Token> postfixTokens,
        Func<string, V> literalParser,
        Dictionary<string, V> variables,
        Dictionary<string, Func<V, V, V>> operators,
        Dictionary<string, Func<V, V>> funcs1Arg
        //,Dictionary<string, Func<V, V, V>> funcs2Arg
        )
    {
        _logger.LogDebug("Evaluating...");

        //build expression tree from postfix 
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = new();
        Dictionary<Node<Token>, V> nodeValueDictionary = new();

        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in postfixTokens)
        {
            if (token.TokenType == Token.FunctionOpenParenthesisTokenType)
            {
                Node<Token> functionNode = new(token);
                //this is the result of an expression (i.e. operator) or a comma operator for multiple arguments
                Token tokenInFunction = stack.Pop();
                 functionNode.Right = nodeDictionary[tokenInFunction];
               _logger.LogDebug("Pop {token} from stack (function right child)", tokenInFunction);

                //remember the functionNode
                nodeDictionary.Add(token, functionNode);

                //EVALUATE FUNCTION 1d XTRA
                V functionResult = default(V);
                if (nodeValueDictionary.ContainsKey(functionNode.Right as Node<Token>))
                {
                    functionResult =  funcs1Arg[token.Text](nodeValueDictionary[functionNode.Right as Node<Token>]);
                    nodeValueDictionary.Add(functionNode, functionResult);
                }
                //and push to the stack
                stack.Push(token);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (
                stack.Count < 2 || //
                token.TokenType == Token.LiteralTokenType ||
                token.TokenType == Token.IdentifierTokenType)
            {
                var tokenNode = new Node<Token>(token);
                nodeDictionary.Add(token, tokenNode);
                
                //XTRA
                V value=default(V);
                if (token.TokenType == Token.LiteralTokenType)
                    nodeValueDictionary.Add(tokenNode, value =literalParser(token.Text));
                else if (token.TokenType == Token.IdentifierTokenType)
                    nodeValueDictionary.Add(tokenNode, value = variables[token.Text]);

                stack.Push(token);
                _logger.LogDebug("Push {token} to stack (value: {value})", token,value);
                continue;
            }

            //else it is an operator (needs 2 items )
            Node<Token> operatorNode = new(token);
            //pop the 2 items on the current stack
            Token rightToken = stack.Pop(), leftToken = stack.Pop();
            operatorNode.Right = nodeDictionary[rightToken];
            operatorNode.Left = nodeDictionary[leftToken];
            _logger.LogDebug("Pop {rightToken} from stack (right child)", rightToken);
            _logger.LogDebug("Pop {leftToken} from stack (left child)", leftToken);

            //remember the operatorNode
            nodeDictionary.Add(token, operatorNode);

            //XTRA
            var result =
                    operators[token.Text](
                        nodeValueDictionary[operatorNode.Left as Node<Token>],
                        nodeValueDictionary[operatorNode.Right as Node<Token>]);
            nodeValueDictionary.Add(operatorNode, result);
           //if (nodeValueDictionary.ContainsKey(operatorNode.Right as Node<Token>) && nodeValueDictionary.ContainsKey(operatorNode.Left as Node<Token>))
           //  );

           //and push to the stack
           stack.Push(token);
            _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token,result);
        }

        if (stack.Count > 1)
        {
            string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Text));
            _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
            throw new InvalidOperationException(
                string.Format("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {0}", stackItemsString));
        }

        //the last item on the postfix expression is the root node
        //Tree<Token> tree = new() {  };
        //tree.NodeDictionary = nodeDictionary;
        var Root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[Root];
    }


}
