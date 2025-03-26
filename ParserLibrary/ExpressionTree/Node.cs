namespace ParserLibrary.ExpressionTree;

public class Node<T> : NodeBase
{
    public Node(T? value) : base(value?.ToString() ?? "")
    {
        Value = value;
    }

    public Node() : base("") { }

    protected T? _value;
    public T? Value
    {
        get => _value;
        set
        {
            _value = value;
            base.Text = _value?.ToString() ?? "";
        }
    }

    public object GetUnaryArgument(bool isPrefix, Dictionary<Node<T>, object> nodeValueDictionary)
    {
        return nodeValueDictionary[
            ((isPrefix ? Right : Left) as Node<T>)!];
    }

    public (object LeftOperand, object RightOperand) GetBinaryArguments(Dictionary<Node<T>, object> nodeValueDictionary)
    {
        return (LeftOperand: nodeValueDictionary[(this.Left as Node<T>)!],
                RightOperand: nodeValueDictionary[(this.Right as Node<T>)!]);
    }

    public object GetFunctionArgument(Dictionary<Node<T>, object> nodeValueDictionary)
    {
        return nodeValueDictionary[(Right as Node<T>)!]; //a1  
    }

    /// <summary>
    /// Return the function arguments assuming that the node is a valid "function node".
    /// </summary>
    /// <returns></returns>
    public int GetFunctionArgumentsCount(string argumentSeparator)
    {
        if (Left is null && Right is null) return 0;

        if (Left is not null) throw new InvalidOperationException($"The function node '{Text}' cannot contain a non-null Left child node.");

        if (Right is not null && Right.Text != argumentSeparator) return 1;

        //an argument separator exists, so we have at least 2 arguments
        int iArguments = 2;

        //here Right is not null
        NodeBase? leftNode = Right?.Left;
        while (leftNode != null)
        {
            if (leftNode.Text == argumentSeparator) iArguments++;
            leftNode = leftNode.Left;
        }
        return iArguments;
    }

    public object[] GetFunctionArguments(string argumentSeparator, Dictionary<Node<T>, object> nodeValueDictionary)
    {
        int argumentsCount = GetFunctionArgumentsCount(argumentSeparator);
        return GetFunctionArguments(argumentsCount, nodeValueDictionary);
    }

    /// <summary>
    /// If we already know the number of arguments then we should call this function for better performance.
    /// </summary>
    /// <param name="count"></param>
    /// <param name="nodeValueDictionary"></param>
    /// <returns></returns>
    public object[] GetFunctionArguments(int count, Dictionary<Node<T>, object> nodeValueDictionary)
    {
        if (count == 1) return [nodeValueDictionary[(Right as Node<T>)!]]; //a1

        if (count == 2) return [
             nodeValueDictionary[(Right!.Left as Node<T>)!], //a1
             nodeValueDictionary[(Right!.Right as Node<T>)!], //a2
        ];

        if (count == 3) return
        [
            nodeValueDictionary[(Right!.Left!.Left as Node<T>)!], //a1
            nodeValueDictionary[(Right!.Left!.Right as Node<T>)!], //a2
            nodeValueDictionary[(Right!.Right as Node<T>)!], //a3
        ];

        if (count == 4) return
        [
            nodeValueDictionary[(Right!.Left!.Left!.Left as Node<T>)!], //a1
            nodeValueDictionary[(Right.Left.Left.Right as Node <T>)!], //a2
            nodeValueDictionary[(Right.Left.Right as Node <T>)!], //a3
            nodeValueDictionary[(Right.Right as Node <T>)!], //a4
        ];

        //generic case for arguments >=5
        object[] arguments = new object[count];
        var leftFarNode = Right;
        for (int iDepth = 0; iDepth <= count - 2; iDepth++)
            leftFarNode = leftFarNode!.Left;
        arguments[0] = nodeValueDictionary[(leftFarNode as Node<T>)!];

        for (int iArg = 1; iArg < count - 1; iArg++)
        {
            var internalNode = Right;
            for (int iDepth = 0; iDepth <= count - iArg - 2; iDepth++)
                internalNode = internalNode!.Left;
            arguments[iArg] = nodeValueDictionary[(internalNode!.Right as Node<T>)!];
        }
        arguments[count - 1] = nodeValueDictionary[(Right!.Right as Node<T>)!];
        return arguments;
    }
}


