using System.Text;

namespace ParserLibrary.ExpressionTree;

/// <summary>
/// Vertical tree printing extensions that create downward-flowing tree layouts
/// </summary>
public static class NodeBasePrintExtensionsVertical
{
    /// <summary>
    /// Vertical tree with centered nodes and proper box-drawing connections
    /// </summary>
    public static string ToVerticalTreeString(this NodeBase root)
    {
        if (root is null) return string.Empty;

        var nodeInfos = new List<NodeInfo>();
        var parentMap = new Dictionary<NodeBase, NodeBase>();
        
        // Collect all nodes with their level and position information
        CollectNodeInfos(root, 0, nodeInfos, parentMap, null);
        
        // Calculate positions for each node
        CalculatePositions(nodeInfos);
        
        // Build the visual representation
        return BuildVerticalTree(nodeInfos, parentMap);
    }

    /// <summary>
    /// Print vertical tree to console
    /// </summary>
    public static void PrintVerticalTree(this NodeBase root)
    {
        Console.WriteLine(root.ToVerticalTreeString());
    }

    #region Private Helper Classes and Methods

    private class NodeInfo
    {
        public NodeBase Node { get; set; }
        public int Level { get; set; }
        public int TraversalOrder { get; set; }
        public List<NodeBase> Children { get; set; } = new();
        public int CenterColumn { get; set; }
        public int LeftBound { get; set; }
        public int RightBound { get; set; }
    }

    private static void CollectNodeInfos(NodeBase node, int level, List<NodeInfo> nodeInfos, Dictionary<NodeBase, NodeBase> parentMap, NodeBase parent)
    {
        if (node == null) return;

        var children = new List<NodeBase>();
        if (node.Left != null) children.Add(node.Left);
        if (node.Right != null) children.Add(node.Right);
        if (node.Other != null) children.AddRange(node.Other);

        var nodeInfo = new NodeInfo
        {
            Node = node,
            Level = level,
            TraversalOrder = nodeInfos.Count,
            Children = children
        };

        nodeInfos.Add(nodeInfo);
        
        if (parent != null)
        {
            parentMap[node] = parent;
        }

        // Process children in order
        foreach (var child in children)
        {
            CollectNodeInfos(child, level + 1, nodeInfos, parentMap, node);
        }
    }

    private static void CalculatePositions(List<NodeInfo> nodeInfos)
    {
        // Group nodes by level
        var levelGroups = nodeInfos.GroupBy(n => n.Level).OrderBy(g => g.Key).ToList();
        
        // Calculate positions for each level, considering the tree structure
        foreach (var levelGroup in levelGroups)
        {
            var nodesInLevel = levelGroup.OrderBy(n => n.TraversalOrder).ToList();
            
            if (levelGroup.Key == 0)
            {
                // Root node - center it
                var rootNode = nodesInLevel[0];
                rootNode.CenterColumn = Math.Max(20, rootNode.Node.Text.Length / 2); // Start with reasonable offset
            }
            else
            {
                // Position child nodes relative to their parents
                foreach (var nodeInfo in nodesInLevel)
                {
                    var parentNode = nodeInfos.FirstOrDefault(n => n.Children.Contains(nodeInfo.Node));
                    if (parentNode != null)
                    {
                        var siblings = parentNode.Children;
                        int siblingIndex = siblings.IndexOf(nodeInfo.Node);
                        
                        if (siblings.Count == 1)
                        {
                            // Single child - center under parent
                            nodeInfo.CenterColumn = parentNode.CenterColumn;
                        }
                        else
                        {
                            // Multiple children - spread them out
                            int spacing = Math.Max(8, parentNode.Node.Text.Length + 4);
                            int totalWidth = (siblings.Count - 1) * spacing;
                            int startCol = parentNode.CenterColumn - totalWidth / 2;
                            
                            nodeInfo.CenterColumn = startCol + siblingIndex * spacing;
                        }
                    }
                }
            }
            
            // Calculate bounds for each node
            foreach (var nodeInfo in nodesInLevel)
            {
                int halfWidth = (int)Math.Ceiling(nodeInfo.Node.Text.Length / 2.0);
                nodeInfo.LeftBound = nodeInfo.CenterColumn - halfWidth;
                nodeInfo.RightBound = nodeInfo.CenterColumn + halfWidth;
            }
        }
    }

    private static string BuildVerticalTree(List<NodeInfo> nodeInfos, Dictionary<NodeBase, NodeBase> parentMap)
    {
        var levelGroups = nodeInfos.GroupBy(n => n.Level).OrderBy(g => g.Key).ToList();
        var grid = new Dictionary<(int row, int col), char>();

        for (int levelIndex = 0; levelIndex < levelGroups.Count; levelIndex++)
        {
            var levelGroup = levelGroups[levelIndex];
            int nodeRow = levelIndex * 3; // 3 rows per level (node, connection, space)

            // Place nodes
            foreach (var nodeInfo in levelGroup)
            {
                PlaceNodeInGrid(grid, nodeInfo, nodeRow);
            }

            // Draw connections to children (except for last level)
            if (levelIndex < levelGroups.Count - 1)
            {
                foreach (var nodeInfo in levelGroup)
                {
                    if (nodeInfo.Children.Count > 0)
                    {
                        DrawConnections(grid, nodeInfo, nodeInfos, nodeRow);
                    }
                }
            }
        }

        return GridToString(grid);
    }

    private static void PlaceNodeInGrid(Dictionary<(int row, int col), char> grid, NodeInfo nodeInfo, int row)
    {
        string nodeText = nodeInfo.Node.Text;
        int startCol = nodeInfo.CenterColumn - nodeText.Length / 2;
        
        // Ensure we don't go negative
        startCol = Math.Max(0, startCol);
        
        for (int i = 0; i < nodeText.Length; i++)
        {
            grid[(row, startCol + i)] = nodeText[i];
        }
    }

    private static void DrawConnections(Dictionary<(int row, int col), char> grid, NodeInfo parentNode, List<NodeInfo> allNodes, int parentRow)
    {
        var children = allNodes.Where(n => parentNode.Children.Contains(n.Node)).ToList();
        if (!children.Any()) return;

        int parentCol = parentNode.CenterColumn;
        int connectionRow = parentRow + 1;

        if (children.Count == 1)
        {
            // Single child - direct vertical line or L-shape
            var child = children[0];
            int childCol = child.CenterColumn;
            
            if (parentCol == childCol)
            {
                // Direct vertical connection
                grid[(connectionRow, parentCol)] = '│';
            }
            else
            {
                // L-shaped connection
                grid[(connectionRow, parentCol)] = '│';
                
                int startCol = Math.Min(parentCol, childCol);
                int endCol = Math.Max(parentCol, childCol);
                
                // Horizontal line
                for (int c = startCol; c <= endCol; c++)
                {
                    grid[(connectionRow + 1, c)] = '─';
                }
                
                // Correct corner characters
                grid[(connectionRow + 1, parentCol)] = '┘';
                grid[(connectionRow + 1, childCol)] = '└';
                grid[(connectionRow, childCol)] = '│';
            }
        }
        else
        {
            // Multiple children - use correct corner characters
            grid[(connectionRow, parentCol)] = '│';
            
            int leftmostCol = children.Min(c => c.CenterColumn);
            int rightmostCol = children.Max(c => c.CenterColumn);
            
            // Horizontal line connecting all children
            for (int c = leftmostCol; c <= rightmostCol; c++)
            {
                grid[(connectionRow + 1, c)] = '─';
            }
            
            // Correct T-junction: ┴ points down from parent
            grid[(connectionRow + 1, parentCol)] = '┴';
            
            // Correct corner characters for children: ┌ and ┐ instead of ┬
            foreach (var child in children)
            {
                int childCol = child.CenterColumn;
                if (childCol < parentCol)
                {
                    grid[(connectionRow + 1, childCol)] = '┌'; // Left corner
                }
                else if (childCol > parentCol)
                {
                    grid[(connectionRow + 1, childCol)] = '┐'; // Right corner  
                }
                else
                {
                    // Child directly under parent - just continue the vertical line
                    grid[(connectionRow + 1, childCol)] = '┴';
                }
                grid[(connectionRow + 2, childCol)] = '│';
            }
        }
    }

    private static string GridToString(Dictionary<(int row, int col), char> grid)
    {
        if (!grid.Any()) return string.Empty;

        int maxRow = grid.Keys.Max(k => k.row);
        int maxCol = grid.Keys.Max(k => k.col);
        
        var lines = new List<string>();
        
        for (int row = 0; row <= maxRow; row++)
        {
            var line = new StringBuilder();
            for (int col = 0; col <= maxCol; col++)
            {
                if (grid.TryGetValue((row, col), out char c))
                {
                    line.Append(c);
                }
                else
                {
                    line.Append(' ');
                }
            }
            lines.Add(line.ToString().TrimEnd());
        }
        
        return string.Join(Environment.NewLine, lines);
    }

    #endregion
}