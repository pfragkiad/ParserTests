using OneOf;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

public class Parser : ParserBase, IParser
{

    public Parser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options)
        :base(logger, tokenizer, options)
    { }

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
        Dictionary<Token, Node<Token>> nodeDictionary = [];

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
        Tree<Token> tree = new()
        {
            Root = root,
            NodeDictionary = nodeDictionary
        };
        return tree;
    }

    public V Evaluate<V>(
        string s,
        Func<string, V>? literalParser = null,
        Dictionary<string, V>? variables = null,
        Dictionary<string, Func<V, V, V>>? binaryOperators = null,
        Dictionary<string, Func<V, V>>? unaryOperators = null,

        Dictionary<string, Func<V, V>>? funcs1Arg = null,
        Dictionary<string, Func<V, V, V>>? funcs2Arg = null,
        Dictionary<string, Func<V, V, V, V>>? funcs3Arg = null
        )
    {
        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);
        return Evaluate(
            postfixTokens,
            literalParser, variables,
            binaryOperators, unaryOperators,
            funcs1Arg, funcs2Arg, funcs3Arg
            );
    }

    protected V Evaluate<V>( //code is practically the same with building the expression tree
        List<Token> postfixTokens,
        Func<string, V>? literalParser,
        Dictionary<string, V>? variables = null,
        Dictionary<string, Func<V, V, V>>? binaryOperators = null,
        Dictionary<string, Func<V, V>>? unaryOperators = null,
        Dictionary<string, Func<V, V>>? funcs1Arg = null,
        Dictionary<string, Func<V, V, V>>? funcs2Arg = null,
        Dictionary<string, Func<V, V, V, V>>? funcs3Arg = null
        )
    {
        _logger.LogDebug("Evaluating...");

        //build expression tree from postfix 
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        Dictionary<Node<Token>, V?> nodeValueDictionary = [];

        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = GetFunctionNode(stack, nodeDictionary, token);

                //EVALUATE FUNCTION 1d XTRA-------------------------------------------------------

                //get function arguments
                V[] args = [.. functionNode.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator,
                    //convert to generic nodeValueDictionary<string,object>
                    nodeValueDictionary.Select(e => (e.Key, Value: (object?)e.Value!)).ToDictionary(e => e.Key, e => e.Value))
                        .Select(v => (V)v)];

                int l = args.Length;
                V? functionResult = args.Length switch
                {
                    1 when funcs1Arg is not null => funcs1Arg[token.Text](args[0]),
                    2 when funcs2Arg is not null => funcs2Arg[token.Text](args[0], args[1]),
                    3 when funcs3Arg is not null => funcs3Arg[token.Text](args[0], args[1], args[2]),
                    _ => default
                };
                nodeValueDictionary.Add(functionNode, functionResult);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;

                //if (nodeValueDictionary.ContainsKey(functionNode.Right as Node<Token>))
                //{
                //    var arg1 = nodeValueDictionary[functionNode.Right as Node<Token>];
                //    functionResult = funcs1Arg[token.Text](arg1);
                //    nodeValueDictionary.Add(functionNode, functionResult);
                //    _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                //} //else it contains more than one arguments
                //else //argument separator
                //{
                //    var separatorNode = functionNode.Right as Node<Token>;
                //    var arg1 = nodeValueDictionary[separatorNode.Left as Node<Token>];
                //    var arg2 = nodeValueDictionary[separatorNode.Right as Node<Token>];
                //    functionResult = funcs2Arg[token.Text](arg1, arg2);
                //    nodeValueDictionary.Add(functionNode, functionResult);
                //    _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                //}
                //---------------------------------------------------------------------------------
                //continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (ShouldPushToStack(stack, token))
            {
                var tokenNode = PushToStack(stack, nodeDictionary, token);

                //XTRA
                V? value = default;
                if (token.TokenType == TokenType.Literal && literalParser is not null)
                    nodeValueDictionary.Add(tokenNode, value = literalParser(token.Text));
                else if (token.TokenType == TokenType.Identifier && variables is not null)
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
                    V? result = default;
                    if (token.TokenType == TokenType.Operator && binaryOperators is not null)//binary operator
                    {
                        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(
                            nodeValueDictionary.Select(e => (e.Key, Value: (object)e.Value!)).ToDictionary(e => e.Key, e => e.Value));
                        result = binaryOperators[token.Text]((V)LeftOperand, (V)RightOperand);
                    }
                    else if (unaryOperators is not null) //unary operator
                    {
                        V operand = (V)operatorNode.GetUnaryArgument(_options.TokenPatterns.UnaryOperatorDictionary[token.Text].Prefix,
                            nodeValueDictionary.Select(e => (e.Key, Value: (object)e.Value!)).ToDictionary(e => e.Key, e => e.Value));
                        result = unaryOperators[token.Text](operand);
                    }
                    //var result =
                    //        operators[token.Text](
                    //            nodeValueDictionary[operatorNode.Left as Node<Token>],
                    //            nodeValueDictionary[operatorNode.Right as Node<Token>]);
                    //if (nodeValueDictionary.ContainsKey(operatorNode.Right as Node<Token>) && nodeValueDictionary.ContainsKey(operatorNode.Left as Node<Token>))
                    //  );
                    nodeValueDictionary.Add(operatorNode, result);
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
        Node<Token> root = nodeDictionary[stack.Pop()];
        return nodeValueDictionary[root]!;
    }

  


    #region Evaluation virtual functions for Custom Evaluation Classes (Parsers derived from Parser class)

    protected virtual object EvaluateFunction(
        Node<Token> functionNode,
        Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //checks only for custom functions registered with RegisterFunction method  
        var functionName = functionNode.Value!.Text;
        if (_customFunctions.TryGetValue(functionName, out var funcDef))
        {
            var args = GetFunctionArguments(functionNode, nodeValueDictionary);
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            // Build a variable dictionary for the function body
            var localVars = new Dictionary<string, object>();
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            // Evaluate the function body with the local variables
            return Evaluate(funcDef.Body, localVars);
        }

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

    protected virtual Type EvaluateFunctionType(
        Node<Token> functionNode,
        Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //checks only for custom functions registered with RegisterFunction method  
        var functionName = functionNode.Value!.Text;
        if (_customFunctions.TryGetValue(functionName, out var funcDef))
        {
            var args = GetFunctionArguments(functionNode, nodeValueDictionary);
            if (args.Length != funcDef.Parameters.Length)
                throw new ArgumentException($"Function '{functionName}' expects {funcDef.Parameters.Length} arguments.");

            // Build a variable dictionary for the function body
            var localVars = new Dictionary<string, object>();
            for (int i = 0; i < funcDef.Parameters.Length; i++)
                localVars[funcDef.Parameters[i]] = args[i];

            // Evaluate the function body with the local variables
            return EvaluateType(funcDef.Body, localVars);
        }
        throw new InvalidOperationException($"Unknown function ({functionNode.Text})");
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

    protected virtual Type EvaluateOperatorType( //used only if we want to check the output type of the operator
        Node<Token> operatorNode,
        Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        throw new InvalidOperationException($"Unknown operator ({operatorNode.Text})");
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

    protected virtual Type EvaluateUnaryOperatorType( //used only if we want to check the output type of the unary operator
        Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        throw new InvalidOperationException($"Unknown unary operator ({operatorNode.Text})");
    }

    protected virtual object EvaluateLiteral(string s)
    {
        return new();
    }

    protected virtual Type EvaluateLiteralType(string s) //used only if we want to check the output type of the literal
    {
        return EvaluateLiteral(s).GetType();
    }

    protected object GetUnaryArgument(bool isPrefix, Node<Token> unaryOperatorNode, Dictionary<Node<Token>, object> nodeValueDictionary) =>
    unaryOperatorNode.GetUnaryArgument(isPrefix, nodeValueDictionary);

    protected (object LeftOperand, object RightOperand) GetBinaryArguments(Node<Token> binaryOperatorNode, Dictionary<Node<Token>, object> nodeValueDictionary) =>
        binaryOperatorNode.GetBinaryArguments(nodeValueDictionary);

    protected object[] GetFunctionArguments(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary) =>
        functionNode.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator, nodeValueDictionary);

    protected object GetFunctionArgument(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary) =>
        functionNode.GetFunctionArgument(nodeValueDictionary);



    public object Evaluate(string s, Dictionary<string, object>? variables = null)
    {
        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);

        return Evaluate(postfixTokens, variables);
    }

    public Type EvaluateType(string s, Dictionary<string, object>? variables = null)
    {
        var inOrderTokens = _tokenizer.GetInOrderTokens(s);
        var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);

        return EvaluateType(postfixTokens, variables);
    }

    //public OneOf<T1, T2> Evaluate<T1, T2>(string s, Dictionary<string, OneOf<T1, T2>> variables)
    //{
    //    object result = Evaluate(s, variables.ToDictionary(kv => kv.Key, kv => (object)kv.Value));
    //    //return based on the type of result
    //    if (result is T1 t1) return t1;
    //    if (result is T2 t2) return t2;
    //    throw new InvalidOperationException("Could not evaluate return type.");
    //}

    //public OneOf<T1,T2,T3> Evaluate<T1, T2, T3>(string s, Dictionary<string, OneOf<T1, T2, T3>> variables)
    //{
    //    object result = Evaluate(s, variables.ToDictionary(kv => kv.Key, kv => (object)kv.Value));
    //    //return based on the type of result
    //    if (result is T1 t1) return t1;
    //    if (result is T2 t2) return t2;
    //    if (result is T3 t3) return t3;
    //    throw new InvalidOperationException("Could not evaluate return type.");
    //}

    //public OneOf<T1, T2, T3, T4> Evaluate<T1, T2, T3, T4>(string s, Dictionary<string, OneOf<T1, T2, T3, T4>> variables)
    //{
    //    object result = Evaluate(s, variables.ToDictionary(kv => kv.Key, kv => (object)kv.Value));
    //    //return based on the type of result
    //    if (result is T1 t1) return t1;
    //    if (result is T2 t2) return t2;
    //    if (result is T3 t3) return t3;
    //    if (result is T4 t4) return t4;
    //    throw new InvalidOperationException("Could not evaluate return type.");
    //}

    //public OneOf<T1, T2, T3, T4, T5> Evaluate<T1, T2, T3, T4, T5>(string s, Dictionary<string, OneOf<T1, T2, T3, T4, T5>> variables)
    //{
    //    object result = Evaluate(s, variables.ToDictionary(kv => kv.Key, kv => (object)kv.Value));
    //    //return based on the type of result
    //    if (result is T1 t1) return t1;
    //    if (result is T2 t2) return t2;
    //    if (result is T3 t3) return t3;
    //    if (result is T4 t4) return t4;
    //    if (result is T5 t5) return t5;
    //    throw new InvalidOperationException("Could not evaluate return type.");
    //}

    //public OneOf<T1, T2, T3, T4, T5, T6> Evaluate<T1, T2, T3, T4, T5, T6>(string s, Dictionary<string, OneOf<T1, T2, T3, T4, T5, T6>> variables)
    //{
    //    object result = Evaluate(s, variables.ToDictionary(kv => kv.Key, kv => (object)kv.Value));
    //    //return based on the type of result
    //    if (result is T1 t1) return t1;
    //    if (result is T2 t2) return t2;
    //    if (result is T3 t3) return t3;
    //    if (result is T4 t4) return t4;
    //    if (result is T5 t5) return t5;
    //    if (result is T6 t6) return t6;
    //    throw new InvalidOperationException("Could not evaluate return type.");
    //}


    protected virtual object Evaluate(List<Token> postfixTokens, Dictionary<string, object>? variables = null)
    {
        _logger.LogDebug("Evaluating...");

        //build expression tree from postfix 
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        //XTRA
        Dictionary<Node<Token>, object> nodeValueDictionary = [];

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
                object? value = null;
                if (token.TokenType == TokenType.Literal)
                    nodeValueDictionary.Add(tokenNode, value = EvaluateLiteral(token.Text));
                else if (token.TokenType == TokenType.Identifier && variables is not null)
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

    protected virtual Type EvaluateType(List<Token> postfixTokens, Dictionary<string, object>? variables = null)
    {
        _logger.LogDebug("Evaluating...");

        //build expression tree from postfix 
        Stack<Token> stack = new();
        Dictionary<Token, Node<Token>> nodeDictionary = [];
        //XTRA
        Dictionary<Node<Token>, object> nodeValueDictionary = [];

        //https://www.youtube.com/watch?v=WHs-wSo33MM
        foreach (var token in postfixTokens)
        {
            if (token.TokenType == TokenType.Function)
            {
                Node<Token> functionNode = GetFunctionNode(stack, nodeDictionary, token);

                //EVALUATE FUNCTION 1d XTRA-------------------------------------------------------
                var functionResult = EvaluateFunctionType(functionNode, nodeValueDictionary);
                nodeValueDictionary.Add(functionNode, functionResult);
                _logger.LogDebug("Pushing {token} from stack (function node) (result: {result})", token, functionResult);
                continue;
            }

            //from now on we deal with operators, literals and identifiers only
            if (ShouldPushToStack(stack, token))
            {
                var tokenNode = PushToStack(stack, nodeDictionary, token);

                //XTRA CALCULATION HERE
                object? value = null;
                if (token.TokenType == TokenType.Literal)
                    nodeValueDictionary.Add(tokenNode, value = EvaluateLiteralType(token.Text));
                else if (token.TokenType == TokenType.Identifier && variables is not null)
                {
                    if (variables[token.Text] is Type) //useful for custom functions
                        nodeValueDictionary.Add(tokenNode, value = variables[token.Text]);
                    else nodeValueDictionary.Add(tokenNode, value = variables[token.Text].GetType());
                }

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
                        EvaluateOperatorType(operatorNode, nodeValueDictionary) :
                        EvaluateUnaryOperatorType(operatorNode, nodeValueDictionary);
                    nodeValueDictionary.Add(operatorNode, result);
                    _logger.LogDebug("Pushing {token} from stack (operator node) (result: {result})", token, result);
                }
                else
                    _logger.LogDebug("Pushing {token} from stack (argument separator node)", token);
            }
        }

        ThrowExceptionIfStackIsInvalid(stack);

        var root = nodeDictionary[stack.Pop()];
        return (Type)nodeValueDictionary[root];
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
