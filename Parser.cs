using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserTests.ExpressionTree;
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

    public Tree Parse(string s)
    {
        TokensFunctions result = _tokenizer.GetInOrderTokensAndFunctions(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(result.OrderTokens);

        return GetExpressionTree(new TokensFunctions(postfixTokens, result.FunctionsArgumentsCount));
    }

    public Tree GetExpressionTree(TokensFunctions tokensResult)
    {
        _logger.LogDebug("Building expresion tree from postfix tokens...");

        //build expression tree from postfix 
        Stack<Token> stack = new();

        Dictionary<Token, NodeBase> nodeDictionary = new();
        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in tokensResult.OrderTokens) //these should be PostfixOrder tokens
        {
            //functions take a single parameter (just like unary operators)
            List<NodeBase> argumentNodes = new();
            ListToString<Token> argumentTokens = new();
            if (token.TokenType == Token.FunctionOpenParenthesisTokenType)
            {
                Node<Token> functionNode = new(token);
                for (int iArgument = 0; iArgument < tokensResult.FunctionsArgumentsCount[token]; iArgument++)
                //pop the <function argument count> items on the current stack
                {
                    Token tokenInFunction = stack.Pop();
                    argumentNodes.Add(nodeDictionary[tokenInFunction]);
                    argumentTokens.Add(tokenInFunction);
                    _logger.LogDebug("Pop {token} from stack (function child)", tokenInFunction);
                }

                //the problem is that this does NOT allow expressions
                ////all arguments behave as a single one, in order for the inorder to work as expected!`
                //argumentTokens.Reverse();
                //functionNode.Right = new Node<ListToString<Token>>(argumentTokens);
                //_logger.LogDebug("Function arguments: {arguments} are stored as Left Node", argumentTokens.ToString());

                if (argumentNodes.Count > 0)
                {
                    functionNode.Right = argumentNodes[0];
                    _logger.LogDebug("Function argument child {argument} is stored as Right Node", argumentNodes[0].Text);
                }

                if (argumentNodes.Count > 1)
                {
                    functionNode.Left = argumentNodes[1];
                    _logger.LogDebug("Function argument child {argument} is stored as Left Node", argumentNodes[1].Text);
                }
                //WATCH: OTHER ARGUMENTS (>2) in argumentNodes CAN BE RETRIEVED BUT NOT STORED IN A BINARY TREE!

                if (argumentNodes.Count > 2) functionNode.Other = new List<NodeBase>();
                for (int iArgument = 2; iArgument < tokensResult.FunctionsArgumentsCount[token]; iArgument++)
                {
                    functionNode.Other!.Add(argumentNodes[iArgument]);
                    _logger.LogWarning("Function argument child {argument} is stored as Other Node [{index}]", argumentNodes[iArgument].Text, iArgument - 2);

                }

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
            string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Value));
            _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
            throw new InvalidOperationException(
                string.Format("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {0}", stackItemsString));
        }

        //the last item on the postfix expression is the root node
        Tree tree = new Tree() { Root = nodeDictionary[stack.Pop()] };
        tree.NodeDictionary = nodeDictionary;
        return tree;
    }




}
