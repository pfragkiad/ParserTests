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

    #region Cloning


    /// <summary>
    /// Creates a deep clone of this node and all its descendants.
    /// </summary>
    /// <returns>A new Node&lt;T&gt; instance with cloned structure</returns>
    public Node<T> DeepClone()
    {
        var cloneMap = new Dictionary<Node<T>, Node<T>>();
        return DeepCloneInternal(cloneMap);
    }

    /// <summary>
    /// Creates a deep clone of this node and all its descendants, using the provided mapping
    /// to handle circular references and maintain node relationships.
    /// </summary>
    /// <param name="cloneMap">Map to track original to cloned node relationships</param>
    /// <returns>A new Node&lt;T&gt; instance with cloned structure</returns>
    public Node<T> DeepClone(Dictionary<Node<T>, Node<T>> cloneMap)
    {
        return DeepCloneInternal(cloneMap);
    }

    /// <summary>
    /// Internal implementation for deep cloning with circular reference handling.
    /// </summary>
    /// <param name="cloneMap">Map to track cloned nodes</param>
    /// <returns>Cloned node</returns>
    private Node<T> DeepCloneInternal(Dictionary<Node<T>, Node<T>> cloneMap)
    {
        // Check if we've already cloned this node
        if (cloneMap.TryGetValue(this, out var existingClone))
            return existingClone;

        // Create new node with cloned value
        var cloned = new Node<T>(CloneValue(Value))
        {
            Text = this.Text // Explicitly set Text in case it differs from Value.ToString()
        };

        // Add to map before cloning children to handle circular references
        cloneMap[this] = cloned;

        // Clone children
        cloned.Left = CloneChild(this.Left as Node<T>, cloneMap);
        cloned.Right = CloneChild(this.Right as Node<T>, cloneMap);

        // Clone Other collection if it exists
        if (this.Other?.Count > 0)
        {
            cloned.Other = new List<NodeBase>();
            foreach (var otherNode in this.Other)
            {
                if (otherNode is Node<T> typedOtherNode)
                {
                    cloned.Other.Add(typedOtherNode.DeepCloneInternal(cloneMap));
                }
                else
                {
                    // For non-generic NodeBase instances, create a basic clone
                    // You might need to adjust this based on your actual usage
                    var basicClone = new Node<object>(otherNode.Text) { Text = otherNode.Text };
                    cloned.Other.Add(basicClone);
                }
            }
        }

        return cloned;
    }

    /// <summary>
    /// Helper method to clone a child node.
    /// </summary>
    /// <param name="child">The child node to clone</param>
    /// <param name="cloneMap">Map to track cloned nodes</param>
    /// <returns>Cloned child node or null</returns>
    private static Node<T>? CloneChild(Node<T>? child, Dictionary<Node<T>, Node<T>> cloneMap)
    {
        return child?.DeepCloneInternal(cloneMap);
    }

    /// <summary>
    /// Clones the value of type T. Override this method if T requires special cloning logic.
    /// </summary>
    /// <param name="original">The original value to clone</param>
    /// <returns>A cloned value</returns>
    private static T? CloneValue(T? original)
    {
        if (original == null) return default;

        // Handle Token specifically since it's likely to be your T type
        if (original is Token token)
        {
            return (T)(object)token.Clone();
        }

        // For value types and strings, return as-is (they're immutable)
        if (typeof(T).IsValueType || typeof(T) == typeof(string))
        {
            return original;
        }

        // For ICloneable types
        if (original is ICloneable cloneable)
        {
            return (T)cloneable.Clone();
        }

        // For reference types without special cloning needs, return the reference
        // This assumes the value is effectively immutable for cloning purposes
        return original;
    }

    #endregion

    public object? GetUnaryArgument(bool isPrefix, Dictionary<Node<T>, object?> nodeValueDictionary)
    {
        return nodeValueDictionary[
            ((isPrefix ? Right : Left) as Node<T>)!];
    }

    public Node<T> GetUnaryArgumentNode(bool isPrefix)
    {
        return ((isPrefix ? Right : Left) as Node<T>)!;
    }


    public (object? LeftOperand, object? RightOperand) GetBinaryArguments(Dictionary<Node<T>, object?> nodeValueDictionary)
    {
        return (LeftOperand: nodeValueDictionary[(this.Left as Node<T>)!],
                RightOperand: nodeValueDictionary[(this.Right as Node<T>)!]);
    }
    public (Node<T> LeftOperand, Node<T> RightOperand) GetBinaryArgumentNodes()
    {
        return (LeftOperand: (this.Left as Node<T>)!,
                RightOperand: (this.Right as Node<T>)!);
    }

    public object? GetFunctionArgument(Dictionary<Node<T>, object?> nodeValueDictionary)
    {
        return nodeValueDictionary[(Right as Node<T>)!]; //a1  
    }

    /// <summary>
    /// Return the function arguments assuming that the node is a valid "function node".
    /// </summary>
    /// <returns></returns>
    public int GetFunctionArgumentsCount(string argumentSeparator)
    {
        if (Left is not null) throw new InvalidOperationException($"The function node '{Text}' cannot contain a non-null Left child node.");
        
        if (Right is null) return 0;

        // Zero-arg placeholder: Right is a Token.Null literal (Match.Empty)
        if (Right is Node<Token> rt && rt.Value is not null && rt.Value.IsNull)
            return 0;

        if (Right.Text != argumentSeparator) return 1;
        //if ((Right as Node<Token>).Value.TokenType != TokenType.ArgumentSeparator) return 1;

        //an argument separator exists, so we have at least 2 arguments
        int iArguments = 2;

        NodeBase? leftNode = Right.Left;
        while (leftNode != null)
        {
            if (leftNode.Text == argumentSeparator) iArguments++;
            leftNode = leftNode.Left;
        }
        return iArguments;
    }

    //public object[] GetFunctionArguments(string argumentSeparator, Dictionary<Node<T>, object> nodeValueDictionary)
    //{
    //    int argumentsCount = GetFunctionArgumentsCount(argumentSeparator);
    //    return GetFunctionArguments(argumentsCount, nodeValueDictionary);
    //}

    public object?[] GetFunctionArguments(string argumentSeparator, Dictionary<Node<T>, object?> nodeValueDictionary)
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
    public object?[] GetFunctionArguments(int count, Dictionary<Node<T>, object?> nodeValueDictionary)
    {
        if (count == 0) return [];

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
        object?[] arguments = new object?[count];
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


    public Node<T>[] GetFunctionArgumentNodes(string argumentSeparator)
    {
        int argumentsCount = GetFunctionArgumentsCount(argumentSeparator);
        return GetFunctionArgumentNodes(argumentsCount);
    }

    public Node<T>[] GetFunctionArgumentNodes(int count)
    {
        if (count == 0) return [];

        if (count == 1) return [(Right as Node<T>)!]; //a1

        if (count == 2) return [
             (Right!.Left as Node<T>)!, //a1
             (Right!.Right as Node<T>)!, //a2
        ];

        if (count == 3) return
        [
            (Right!.Left!.Left as Node<T>)!, //a1
            (Right!.Left!.Right as Node<T>)!, //a2
            (Right!.Right as Node<T>)! //a3
        ];

        if (count == 4) return
        [
            (Right!.Left!.Left!.Left as Node<T>)!, //a1
            (Right.Left.Left.Right as Node <T>)!, //a2
            (Right.Left.Right as Node <T>)!, //a3
            (Right.Right as Node <T>)! //a4
        ];

        //generic case for arguments >=5
        Node<T>[] arguments = new Node<T>[count];
        var leftFarNode = Right;
        for (int iDepth = 0; iDepth <= count - 2; iDepth++)
            leftFarNode = leftFarNode!.Left;
        arguments[0] = (leftFarNode as Node<T>)!;

        for (int iArg = 1; iArg < count - 1; iArg++)
        {
            var internalNode = Right;
            for (int iDepth = 0; iDepth <= count - iArg - 2; iDepth++)
                internalNode = internalNode!.Left;
            arguments[iArg] = (internalNode!.Right as Node<T>)!;
        }
        arguments[count - 1] = (Right!.Right as Node<T>)!;
        return arguments;
    }
}


