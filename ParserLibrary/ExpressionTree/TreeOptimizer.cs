using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

public class TreeOptimizer<T>
{
    public Tree<T> OptimizeForDataTypes(Tree<T> originalTree, Dictionary<string, Type>? variableTypes = null)
    {
        variableTypes ??= [];
        var optimizedTree = originalTree.DeepClone();
        OptimizeNode(optimizedTree.Root, variableTypes);
        return optimizedTree;
    }

    private void OptimizeNode(Node<T> node, Dictionary<string, Type> variableTypes)
    {
        if (node == null) return;

        // Recursively optimize children first
        if (node.Left != null) OptimizeNode((Node<T>)node.Left, variableTypes);
        if (node.Right != null) OptimizeNode((Node<T>)node.Right, variableTypes);

        // Check if this node represents a commutative operation
        if (TreeOptimizer<T>.IsCommutativeOperation(node))
        {
            RearrangeCommutativeOperation(node, variableTypes);
        }
    }

    private static bool IsCommutativeOperation(Node<T> node)
    {
        if (node.Value is not Token token) return false;
        
        return token.TokenType == TokenType.Operator && 
               (token.Text == "+" || token.Text == "*");
    }

    private void RearrangeCommutativeOperation(Node<T> node, Dictionary<string, Type> variableTypes)
    {
        // Collect all operands from the same commutative operation
        var operands = CollectCommutativeOperands(node, GetOperatorText(node));
        
        if (operands.Count <= 2) return; // No optimization needed
        
        // Group operands by type priority
        var groupedOperands = GroupOperandsByTypePriority(operands, variableTypes);
        
        if (groupedOperands.Count <= 1) return; // All same type
        
        // Rebuild the tree with optimized ordering
        var optimizedNode = BuildOptimizedTree(groupedOperands, GetOperatorText(node));
        
        // Replace the current node's structure
        node.Left = optimizedNode.Left;
        node.Right = optimizedNode.Right;
    }

    private List<Node<T>> CollectCommutativeOperands(Node<T> node, string operatorText)
    {
        var operands = new List<Node<T>>();
        CollectOperandsRecursive(node, operatorText, operands);
        return operands;
    }

    private void CollectOperandsRecursive(Node<T> node, string operatorText, List<Node<T>> operands)
    {
        if (node == null) return;

        var currentOp = GetOperatorText(node);
        if (currentOp == operatorText)
        {
            // This node has the same operator, collect its operands
            CollectOperandsRecursive((Node<T>)node.Left, operatorText, operands);
            CollectOperandsRecursive((Node<T>)node.Right, operatorText, operands);
        }
        else
        {
            // This is an operand (leaf node or different operation)
            operands.Add(node);
        }
    }

    private List<List<Node<T>>> GroupOperandsByTypePriority(List<Node<T>> operands, Dictionary<string, Type> variableTypes)
    {
        var typeGroups = new Dictionary<int, List<Node<T>>>();
        
        foreach (var operand in operands)
        {
            int priority = GetTypePriority(operand, variableTypes);
            
            if (!typeGroups.ContainsKey(priority))
                typeGroups[priority] = new List<Node<T>>();
            
            typeGroups[priority].Add(operand);
        }
        
        // Return groups ordered by priority (lower number = higher priority)
        return typeGroups.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
    }

    private int GetTypePriority(Node<T> operand, Dictionary<string, Type> variableTypes)
    {
        if (operand.Value is not Token token) return int.MaxValue;
        
        return token.TokenType switch
        {
            TokenType.Literal => GetLiteralTypePriority(token.Text),
            TokenType.Identifier when variableTypes.TryGetValue(token.Text, out var type) => GetTypePriorityValue(type),
            TokenType.Function => 1000, // Functions last
            _ => int.MaxValue
        };
    }

    private int GetLiteralTypePriority(string literalText)
    {
        // Check for integer literals
        if (int.TryParse(literalText, out _))
            return 2; // Integer literals
        
        // Check for floating-point literals
        if (double.TryParse(literalText, System.Globalization.CultureInfo.InvariantCulture, out _))
            return 1; // Float literals
        
        // All other literals (including timespan literals) are treated as unrecognized
        return 0; // Unrecognized literals get highest priority (safest approach)
    }

    private int GetTypePriorityValue(Type type)
    {
        // Customize this based on your specific types
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return 1;
        if (type == typeof(int) || type == typeof(long)) return 2;
        //if (type.Name.Contains("Item")) return 100; // Your custom Item types
        
        return 50; // Default priority for other types
    }

    private Node<T> BuildOptimizedTree(List<List<Node<T>>> groupedOperands, string operatorText)
    {
        var allOperands = groupedOperands.SelectMany(g => g).ToList();
        return BuildLeftAssociativeTree(allOperands, operatorText);
    }

    private Node<T> BuildLeftAssociativeTree(List<Node<T>> operands, string operatorText)
    {
        if (operands.Count == 1) return operands[0];
        
        var result = operands[0];
        
        for (int i = 1; i < operands.Count; i++)
        {
            var operatorNode = CreateOperatorNode(operatorText);
            operatorNode.Left = result;
            operatorNode.Right = operands[i];
            result = operatorNode;
        }
        
        return result;
    }

    private Node<T> CreateOperatorNode(string operatorText)
    {
        // Create a new operator token
        var operatorToken = new Token(TokenType.Operator, operatorText, -1);
        return new Node<T>((T)(object)operatorToken);
    }

    private string GetOperatorText(Node<T> node)
    {
        return node.Value is Token token ? token.Text : "";
    }
}