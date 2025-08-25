using System.Text;

namespace ParserLibrary.ExpressionTree;

public enum PrintType
{
    Ascii,
    SimpleTree,
    Horizontal,
    Parenthesized,
    Detailed,
    Vertical,
}

/// <summary>
/// Alternative tree printing extensions that generate strings instead of directly printing to console
/// </summary>
public static class NodeBasePrintExtensions2
{
    public static string ToString(this NodeBase root, PrintType printType)
    {
        return printType switch
        {
            PrintType.SimpleTree => root.ToSimpleTreeString(),
            PrintType.Ascii => root.ToAsciiTreeString(),
            PrintType.Horizontal => root.ToHorizontalTreeString(),
            PrintType.Detailed => root.ToDetailedTreeString(),
            PrintType.Vertical => root.ToVerticalTreeString(),
            //PrintType.Parenthesized => root.ToParenthesizedString(),
            _ => root.ToParenthesizedString(),
        };
    }

    public static void Print(this NodeBase root, PrintType printType)
    {
        Console.WriteLine(root.ToString(printType));
    }


    /// <summary>
    /// Simple tree representation using indentation and ASCII characters
    /// </summary>
    public static string ToSimpleTreeString(this NodeBase root, string indent = "", bool isLast = true)
    {
        if (root is null) return string.Empty;

        var result = new StringBuilder();
        result.AppendLine(indent + (isLast ? "└── " : "├── ") + root.Text);

        var children = new List<NodeBase>();
        if (root.Left != null) children.Add(root.Left);
        if (root.Right != null) children.Add(root.Right);
        if (root.Other != null) children.AddRange(root.Other);

        for (int i = 0; i < children.Count; i++)
        {
            bool isLastChild = i == children.Count - 1;
            string newIndent = indent + (isLast ? "    " : "│   ");
            result.Append(children[i].ToSimpleTreeString(newIndent, isLastChild));
        }

        return result.ToString();
    }

    /// <summary>
    /// ASCII tree representation with proper box-drawing characters
    /// </summary>
    public static string ToAsciiTreeString(this NodeBase root)
    {
        if (root is null) return string.Empty;

        var lines = new List<string>();
        BuildAsciiTree(root, "", true, lines);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Horizontal tree layout showing nodes by level
    /// </summary>
    public static string ToHorizontalTreeString(this NodeBase root, int spacing = 4)
    {
        if (root is null) return string.Empty;

        var levels = new Dictionary<int, List<string>>();
        CollectNodesByLevel(root, 0, levels);

        var result = new StringBuilder();
        int maxLevel = levels.Keys.Max();

        for (int level = 0; level <= maxLevel; level++)
        {
            if (levels.ContainsKey(level))
            {
                string levelStr = string.Join(new string(' ', spacing), levels[level]);
                result.AppendLine($"Level {level}: {levelStr}");
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Compact parenthesized representation of the tree
    /// </summary>
    public static string ToParenthesizedString(this NodeBase root)
    {
        if (root is null) return string.Empty;

        var result = new StringBuilder();
        result.Append(root.Text);

        var children = new List<NodeBase>();
        if (root.Left != null) children.Add(root.Left);
        if (root.Right != null) children.Add(root.Right);
        if (root.Other != null) children.AddRange(root.Other);

        if (children.Count > 0)
        {
            result.Append('(');
            for (int i = 0; i < children.Count; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(children[i].ToParenthesizedString());
            }
            result.Append(')');
        }

        return result.ToString();
    }

    /// <summary>
    /// Detailed tree representation with node information
    /// </summary>
    public static string ToDetailedTreeString(this NodeBase root, string indent = "", bool isLast = true)
    {
        if (root is null) return string.Empty;

        var result = new StringBuilder();
        string connector = isLast ? "└── " : "├── ";

        // Add node information
        string nodeInfo = $"{root.Text}";
        //if (root.Left != null || root.Right != null || (root.Other?.Count ?? 0) > 0)
        //{
        //    var childCount = (root.Left != null ? 1 : 0) +
        //                   (root.Right != null ? 1 : 0) +
        //                   (root.Other?.Count ?? 0);
        //    nodeInfo += $" [{childCount} children]";
        //}

        result.AppendLine(indent + connector + nodeInfo);

        var children = new List<(NodeBase node, string label)>();
        if (root.Left != null) children.Add((root.Left, "L"));
        if (root.Right != null) children.Add((root.Right, "R"));
        if (root.Other != null)
        {
            for (int i = 0; i < root.Other.Count; i++)
            {
                children.Add((root.Other[i], $"O{i}"));
            }
        }

        for (int i = 0; i < children.Count; i++)
        {
            bool isLastChild = i == children.Count - 1;
            string newIndent = indent + (isLast ? "    " : "│   ");
            var (childNode, label) = children[i];

            // Add label for the child type
            string childConnector = isLastChild ? "└── " : "├── ";
            result.AppendLine(newIndent + childConnector + $"[{label}] {childNode.Text}");

            // Recursively add child's children
            string childIndent = newIndent + (isLastChild ? "    " : "│   ");
            string childTree = childNode.ToDetailedTreeString(childIndent, true);
            if (!string.IsNullOrEmpty(childTree) && childTree.Trim().Length > 0)
            {
                var childLines = childTree.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                if (childLines.Length > 1) // Only add if there are actual children
                {
                    for (int j = 1; j < childLines.Length; j++) // Skip first line as we already added it
                    {
                        result.AppendLine(childLines[j]);
                    }
                }
            }
        }

        return result.ToString();
    }

    ///// <summary>
    ///// Print simple tree to console
    ///// </summary>
    //public static void PrintSimpleTree(this NodeBase root)
    //{
    //    Console.WriteLine(root.ToSimpleTreeString());
    //}

    ///// <summary>
    ///// Print ASCII tree to console
    ///// </summary>
    //public static void PrintAsciiTree(this NodeBase root)
    //{
    //    Console.WriteLine(root.ToAsciiTreeString());
    //}

    ///// <summary>
    ///// Print horizontal tree to console
    ///// </summary>
    //public static void PrintHorizontalTree(this NodeBase root, int spacing = 4)
    //{
    //    Console.WriteLine(root.ToHorizontalTreeString(spacing));
    //}

    ///// <summary>
    ///// Print detailed tree to console
    ///// </summary>
    //public static void PrintDetailedTree(this NodeBase root)
    //{
    //    Console.WriteLine(root.ToDetailedTreeString());
    //}

    ///// <summary>
    ///// Print parenthesized representation to console
    ///// </summary>
    //public static void PrintParenthesized(this NodeBase root)
    //{
    //    Console.WriteLine(root.ToParenthesizedString());
    //}

    #region Private Helper Methods

    private static void BuildAsciiTree(NodeBase? node, string prefix, bool isLast, List<string> lines)
    {
        if (node is null) return;

        // Add current node
        lines.Add(prefix + (isLast ? "└─ " : "├─ ") + node.Text);

        // Prepare children list
        var children = new List<NodeBase>();
        if (node.Left is not null) children.Add(node.Left);
        if (node.Right is not null) children.Add(node.Right);
        if (node.Other is not null) children.AddRange(node.Other);

        // Process children
        for (int i = 0; i < children.Count; i++)
        {
            bool isLastChild = i == children.Count - 1;
            string newPrefix = prefix + (isLast ? "   " : "│  ");
            BuildAsciiTree(children[i], newPrefix, isLastChild, lines);
        }
    }

    private static void CollectNodesByLevel(NodeBase? node, int level, Dictionary<int, List<string>> levels)
    {
        if (node is null) return;

        if (!levels.ContainsKey(level))
            levels[level] = [];

        levels[level].Add(node.Text);

        CollectNodesByLevel(node.Left, level + 1, levels);
        CollectNodesByLevel(node.Right, level + 1, levels);

        if (node.Other is not null)
        {
            foreach (var otherNode in node.Other)
            {
                CollectNodesByLevel(otherNode, level + 1, levels);
            }
        }
    }

    #endregion
}