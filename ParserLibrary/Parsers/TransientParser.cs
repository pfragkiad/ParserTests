using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

/// <summary>
/// This class can be use for a single evaluation, not for parallel evaluations, because the nodeValueDictionary and stack fields keep the state of the currently evaluated expression.
/// </summary>
public class TransientParser : ITransientParser
{
    protected readonly ILogger<Parser> _logger;
    protected readonly ITokenizer _tokenizer;
    protected readonly TokenizerOptions _options;

    //created for simplifying and caching dictionaries
    protected internal Dictionary<Node<Token>, object> nodeValueDictionary;
    protected Dictionary<Token, Node<Token>> nodeDictionary = new();
    protected Stack<Token> stack;



    public TransientParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options)
    {
        _logger = logger;
        _tokenizer = tokenizer;
        _options = options.Value;

        if (_options.TokenPatterns is null) _options = TokenizerOptions.Default;
    }

    #region Evaluation virtual functions for Custom Evaluation Classes (Parsers derived from Parser class)
    protected virtual object EvaluateFunction(Node<Token> functionNode)
    {
        throw new InvalidOperationException($"Unknown function ({functionNode.Text})");
        //V functionResult = default(V);
        //if (nodeValueDictionary.ContainsKey(functionNode.Right as Node<Token>))
        //{
        //    var arg1 = nodeValueDictionary[functionNode.Right as Node<Token>];
        //    functionResult = funcs1Arg[token.Text](arg1);
        //} //else it contains more than one arguments
        //else //argument separator
        //{
        //    var separatorNode = functionNode.Right as Node<Token>;
        //    var arg1 = nodeValueDictionary[separatorNode.Left as Node<Token>];
        //    var arg2 = nodeValueDictionary[separatorNode.Right as Node<Token>];
        //    functionResult = funcs2Arg[token.Text](arg1, arg2);
        //}
    }

    protected virtual object EvaluateOperator(Node<Token> operatorNode)
    {
        throw new InvalidOperationException($"Unknown operator ({operatorNode.Text})");
        //var result =
        //        operators[token.Text](
        //            nodeValueDictionary[operatorNode.Left as Node<Token>],
        //            nodeValueDictionary[operatorNode.Right as Node<Token>]);
    }

    protected virtual object EvaluateUnaryOperator(Node<Token> operatorNode)
    {
        throw new InvalidOperationException($"Unknown unary operator ({operatorNode.Text})");
        //var result =
        //        operators[token.Text](
        //            nodeValueDictionary[operatorNode.Left as Node<Token>],
        //            nodeValueDictionary[operatorNode.Right as Node<Token>]);
    }

    protected virtual object EvaluateLiteral(string s)
    {
        return null;
    }
    protected object GetUnaryArgument(bool isPrefix, Node<Token> unaryOperatorNode) =>
        unaryOperatorNode.GetUnaryArgument(isPrefix, nodeValueDictionary);

    protected (object LeftOperand, object RightOperand) GetBinaryArguments(Node<Token> binaryOperatorNode) =>
        binaryOperatorNode.GetBinaryArguments(nodeValueDictionary);

    protected object[] GetFunctionArguments(Node<Token> functionNode) =>
        functionNode.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator, nodeValueDictionary);

    protected object GetFunctionArgument(Node<Token> functionNode) =>
        functionNode.GetFunctionArgument(nodeValueDictionary);


    public object Evaluate(string s, Dictionary<string, object> variables = null)
    {
        //these properties are reset for each evaluation!
        nodeValueDictionary = new Dictionary<Node<Token>, object>();
        nodeDictionary = new Dictionary<Token, Node<Token>>();
        stack = new Stack<Token>();

        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);

        return Evaluate(postfixTokens, variables);
    }

    protected virtual object Evaluate(List<Token> postfixTokens, Dictionary<string, object> variables = null)
    {
        _logger.LogDebug("Evaluating...");

        //build expression tree from postfix 
        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = GetFunctionNode(token);

                //EVALUATE FUNCTION 1d XTRA-------------------------------------------------------
                object functionResult = EvaluateFunction(functionNode);
                nodeValueDictionary.Add(functionNode, functionResult);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (ShouldPushToStack(token))
            {
                var tokenNode = PushToStack(token);

                //XTRA CALCULATION HERE
                object value = null;
                if (token.TokenType == TokenType.Literal)
                    nodeValueDictionary.Add(tokenNode, value = EvaluateLiteral(token.Text));
                else if (token.TokenType == TokenType.Identifier)
                    nodeValueDictionary.Add(tokenNode, value = variables[token.Text]);

                _logger.LogDebug("Push {token} to stack (value: {value})", token, value);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary)
            {
                Node<Token> operatorNode = GetOperatorNode(token);

                //XTRA
                if (token.Text != _options.TokenPatterns.ArgumentSeparator)
                {
                    var result =
                        token.TokenType == TokenType.Operator ?
                        EvaluateOperator(operatorNode) :
                        EvaluateUnaryOperator(operatorNode);
                    nodeValueDictionary.Add(operatorNode, result);
                    _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
            }
        }

        ThrowExceptionIfStackIsInvalid();

        var root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[root];
    }

    #endregion

    private void ThrowExceptionIfStackIsInvalid()
    {
        if (stack.Count > 1)
        {
            string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Text));
            _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
            throw new InvalidOperationException(
                string.Format("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {0}", stackItemsString));
        }
    }

    private Node<Token> GetOperatorNode(Token token)
    {
        //else it is an operator (needs 2 items )
        Node<Token> operatorNode = new(token);

        if (token.TokenType == TokenType.Operator)
        {
            //pop the 2 items on the current stack
            Token rightToken = stack.Pop(), leftToken = stack.Pop();
            operatorNode.Right = nodeDictionary[rightToken];
            operatorNode.Left = nodeDictionary[leftToken];
            _logger.LogDebug("Pop {rightToken} from stack (right child)", rightToken);
            _logger.LogDebug("Pop {leftToken} from stack (left child)", leftToken);
        }
        else // if(token.TokenType==TokenType.OperatorUnary)
        {
            Token childToken = stack.Pop();
            UnaryOperator op = _options.TokenPatterns.UnaryOperatorDictionary[token.Text];
            if (op.Prefix)
            {
                operatorNode.Right = nodeDictionary[childToken];
                _logger.LogDebug("Pop {rightToken} from stack (right child)", childToken);
            }
            else
            {
                operatorNode.Left = nodeDictionary[childToken];
                _logger.LogDebug("Pop {leftToken} from stack (left child)", childToken);
            }
        }

        //remember the operatorNode
        nodeDictionary.Add(token, operatorNode);
        //and push to the stack
        stack.Push(token);
        return operatorNode;
    }

    private Node<Token> GetFunctionNode(Token token)
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


        return functionNode;
    }

    private bool ShouldPushToStack(Token token)
    {
        return stack.Count == 0 ||
               stack.Count == 1 && token.TokenType == TokenType.Operator ||
               token.TokenType == TokenType.Literal ||
               token.TokenType == TokenType.Identifier;
    }

    private Node<Token> PushToStack(Token token)
    {
        var tokenNode = new Node<Token>(token);
        nodeDictionary.Add(token, tokenNode);
        stack.Push(token);
        return tokenNode;

    }
}
