using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

public class Parser : IParser
{
    protected readonly ILogger<Parser> _logger;
    protected readonly ITokenizer _tokenizer;
    protected readonly TokenizerOptions _options;

    public Parser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options)
    {
        _logger = logger;
        _tokenizer = tokenizer;
        _options = options.Value;

        if (_options.TokenPatterns is null) _options = TokenizerOptions.Default;
    }

    public Tree<Token> GetExpressionTree(string s)
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
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = GetFunctionNode(stack, nodeDictionary, token);
                _logger.LogDebug("Pushing {token} from stack (function node)", token);
                continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (ShouldPushToStack(stack, token))
            {
                var tokenNode = PushToStack(stack, nodeDictionary, token);
                _logger.LogDebug("Push {token} to stack", token);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary)
            {
                Node<Token> operatorNode = GetOperatorNode(stack, nodeDictionary, token);
                _logger.LogDebug("Pushing {token} from stack (operator node)", token);
            }
            else
            {
                _logger.LogError("Unexpected token type {type} for token {token}", token.TokenType, token);
                throw new InvalidOperationException($"Unexpected token type {token.TokenType} for token {token}");
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);

        //the last item on the postfix expression is the root node
        var root = nodeDictionary[stack.Pop()];
        Tree<Token> tree = new() { Root = root };
        tree.NodeDictionary = nodeDictionary;
        return tree;
    }

    public V Evaluate<V>(
        string s,
        Func<string, V> literalParser = null,
        Dictionary<string, V> variables = null,
        Dictionary<string, Func<V, V, V>> operators = null,
        Dictionary<string, Func<V, V>>? funcs1Arg = null,
        Dictionary<string, Func<V, V, V>>? funcs2Arg = null
        )
    {
        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);
        return Evaluate(
            postfixTokens,
            literalParser, variables, operators, funcs1Arg, funcs2Arg
            );
    }


    public V Evaluate<V>( //code is practically the same with building the expression tree
        List<Token> postfixTokens,
        Func<string, V> literalParser,
        Dictionary<string, V> variables = null,
        Dictionary<string, Func<V, V, V>> operators = null,
        Dictionary<string, Func<V, V>>? funcs1Arg = null,
        Dictionary<string, Func<V, V, V>>? funcs2Arg = null
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
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = GetFunctionNode(stack, nodeDictionary, token);

                //EVALUATE FUNCTION 1d XTRA-------------------------------------------------------
                V functionResult = default;

                if (nodeValueDictionary.ContainsKey(functionNode.Right as Node<Token>))
                {
                    var arg1 = nodeValueDictionary[functionNode.Right as Node<Token>];
                    functionResult = funcs1Arg[token.Text](arg1);
                    nodeValueDictionary.Add(functionNode, functionResult);
                    _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                } //else it contains more than one arguments
                else //argument separator
                {
                    var separatorNode = functionNode.Right as Node<Token>;
                    var arg1 = nodeValueDictionary[separatorNode.Left as Node<Token>];
                    var arg2 = nodeValueDictionary[separatorNode.Right as Node<Token>];
                    functionResult = funcs2Arg[token.Text](arg1, arg2);
                    nodeValueDictionary.Add(functionNode, functionResult);
                    _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                }
                //---------------------------------------------------------------------------------
                continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (ShouldPushToStack(stack, token))
            {
                var tokenNode = PushToStack(stack, nodeDictionary, token);

                //XTRA
                V value = default;
                if (token.TokenType == TokenType.Literal)
                    nodeValueDictionary.Add(tokenNode, value = literalParser(token.Text));
                else if (token.TokenType == TokenType.Identifier)
                    nodeValueDictionary.Add(tokenNode, value = variables[token.Text]);

                _logger.LogDebug("Push {token} to stack (value: {value})", token, value);
                continue;
            }

            if (token.TokenType == TokenType.Operator || token.TokenType == TokenType.OperatorUnary)
            {
                Node<Token> operatorNode = GetOperatorNode(stack, nodeDictionary, token);

                //XTRA
                if (token.Text != _options.TokenPatterns.ArgumentSeparator)
                {
                    var result =
                            operators[token.Text](
                                nodeValueDictionary[operatorNode.Left as Node<Token>],
                                nodeValueDictionary[operatorNode.Right as Node<Token>]);
                    nodeValueDictionary.Add(operatorNode, result);
                    //if (nodeValueDictionary.ContainsKey(operatorNode.Right as Node<Token>) && nodeValueDictionary.ContainsKey(operatorNode.Left as Node<Token>))
                    //  );
                    _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);

        //the last item on the postfix expression is the root node
        //Tree<Token> tree = new() {  };
        //tree.NodeDictionary = nodeDictionary;
        var root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[root];
    }


    #region Evaluation virtual functions for Custom Evaluation Classes
    protected virtual object EvaluateFunction(
        Node<Token> functionNode,
        Dictionary<Node<Token>, object> nodeValueDictionary)
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

    protected virtual object EvaluateOperator(
    Node<Token> operatorNode,
    Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        throw new InvalidOperationException($"Unknown operator ({operatorNode.Text})");
        //var result =
        //        operators[token.Text](
        //            nodeValueDictionary[operatorNode.Left as Node<Token>],
        //            nodeValueDictionary[operatorNode.Right as Node<Token>]);
    }
    protected virtual object EvaluateUnaryOperator(
        Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
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


    public object Evaluate(string s, Dictionary<string, object> variables = null)
    {
        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);

        return Evaluate(postfixTokens, variables);
    }

    public virtual object Evaluate(List<Token> postfixTokens, Dictionary<string, object> variables = null)
    {
        _logger.LogDebug("Evaluating...");

        //build expression tree from postfix 
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = new();
        //XTRA
        Dictionary<Node<Token>, object> nodeValueDictionary = new();

        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = GetFunctionNode(stack, nodeDictionary, token);

                //EVALUATE FUNCTION 1d XTRA-------------------------------------------------------
                object functionResult = EvaluateFunction(functionNode, nodeValueDictionary);
                nodeValueDictionary.Add(functionNode, functionResult);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (ShouldPushToStack(stack, token))
            {
                var tokenNode = PushToStack(stack, nodeDictionary, token);

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
                Node<Token> operatorNode = GetOperatorNode(stack, nodeDictionary, token);

                //XTRA
                if (token.Text != _options.TokenPatterns.ArgumentSeparator)
                {
                    var result =
                        token.TokenType == TokenType.Operator ?
                        EvaluateOperator(operatorNode, nodeValueDictionary) :
                        EvaluateUnaryOperator(operatorNode, nodeValueDictionary);
                    nodeValueDictionary.Add(operatorNode, result);
                    _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);

        var root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[root];
    }



    #endregion

    private void ThrowExceptionIfStackIsInvalid(Stack<Token> stack)
    {
        if (stack.Count > 1)
        {
            string stackItemsString = string.Join(" ", stack.Reverse().Select(t => t.Text));
            _logger.LogError("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {items}", stackItemsString);
            throw new InvalidOperationException(
                string.Format("The stack should be empty at the end of operations. Check the postfix expression. Current items in stack: {0}", stackItemsString));
        }
    }

    private Node<Token> GetOperatorNode(Stack<Token> stack, Dictionary<Token, Node<Token>> nodeDictionary, Token token)
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

    private Node<Token> GetFunctionNode(Stack<Token> stack, Dictionary<Token, Node<Token>> nodeDictionary, Token token)
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

    private bool ShouldPushToStack(Stack<Token> stack, Token token)
    {
        return stack.Count == 0 ||
               stack.Count == 1 && token.TokenType == TokenType.Operator ||
               token.TokenType == TokenType.Literal ||
               token.TokenType == TokenType.Identifier;
    }

    private Node<Token> PushToStack(Stack<Token> stack, Dictionary<Token, Node<Token>> nodeDictionary, Token token)
    {
        var tokenNode = new Node<Token>(token);
        nodeDictionary.Add(token, tokenNode);
        stack.Push(token);
        return tokenNode;

    }
}
