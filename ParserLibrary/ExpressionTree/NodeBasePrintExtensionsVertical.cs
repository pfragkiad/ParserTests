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
    /// <param name="root">The root node of the tree</param>
    /// <param name="leftOffset">Extra character offset from the left margin (default: 0)</param>
    /// <returns>String representation of the vertical tree</returns>
    public static string ToVerticalTreeString(this NodeBase root, int leftOffset = 0)
    {
        if (root is null) return string.Empty;

        var nodeInfos = new List<NodeInfo>();
        var parentMap = new Dictionary<NodeBase, NodeBase>();
        
        // Collect all nodes with their level and position information
        CollectNodeInfos(root, 0, nodeInfos, parentMap, null);
        
        // Calculate positions for each node
        CalculatePositions(nodeInfos, leftOffset);
        
        // Build the visual representation
        return BuildVerticalTree(nodeInfos, parentMap, leftOffset);
    }

    /// <summary>
    /// Print vertical tree to console
    /// </summary>
    /// <param name="root">The root node of the tree</param>
    /// <param name="leftOffset">Extra character offset from the left margin (default: 0)</param>
    public static void PrintVerticalTree(this NodeBase root, int leftOffset = 0)
    {
        Console.WriteLine(root.ToVerticalTreeString(leftOffset));
    }

    #region Private Helper Classes and Methods

    private class NodeInfo
    {
        public NodeBase Node { get; set; }
        public int Level { get; set; }
        public int TraversalOrder { get; set; }
        public List<NodeBase> Children { get; set; } = [];
        public int CenterColumn { get; set; }
        public int LeftBound { get; set; }
        public int RightBound { get; set; }

        // Computed layout width that guarantees no overlap within this subtree
        public int SubtreeWidth { get; set; }
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

    private static void CalculatePositions(List<NodeInfo> nodeInfos, int leftOffset)
    {
        // Minimal spacing between adjacent child subtrees
        const int minGap = 2;

        // Quick lookups
        var map = nodeInfos.ToDictionary(n => n.Node, n => n);

        // 1) Bottom-up: compute SubtreeWidth for each node so siblings fit with min gap
        foreach (var n in nodeInfos.OrderByDescending(n => n.Level))
        {
            int nodeTextWidth = Math.Max(1, n.Node.Text.Length);

            if (n.Children.Count == 0)
            {
                n.SubtreeWidth = nodeTextWidth;
                continue;
            }

            // Siblings in declared order
            var childInfos = n.Children.Select(c => map[c]).ToList();
            int childrenTotal = 0;
            for (int i = 0; i < childInfos.Count; i++)
            {
                childrenTotal += childInfos[i].SubtreeWidth;
                if (i > 0) childrenTotal += minGap;
            }

            n.SubtreeWidth = Math.Max(nodeTextWidth, childrenTotal);
        }

        // 2) Top-down: assign absolute positions, compact siblings, then center parent over compacted children
        var root = nodeInfos.First(n => n.Level == 0);
        AssignPositionsTopDown(root, leftOffset, minGap, map);

        // 3) Bounds
        foreach (var n in nodeInfos)
        {
            int halfWidth = (int)Math.Ceiling(n.Node.Text.Length / 2.0);
            n.LeftBound = n.CenterColumn - halfWidth;
            n.RightBound = n.CenterColumn + halfWidth;
        }
    }

    private static void AssignPositionsTopDown(NodeInfo parent, int subtreeLeft, int gap, Dictionary<NodeBase, NodeInfo> map)
    {
        int nodeTextWidth = Math.Max(1, parent.Node.Text.Length);

        if (parent.Children.Count == 0)
        {
            // Leaf: center within its subtree
            parent.CenterColumn = subtreeLeft + nodeTextWidth / 2;
            return;
        }

        var children = parent.Children.Select(c => map[c]).ToList();

        // First place children naively within the allotted subtree block
        int childrenTotal = 0;
        for (int i = 0; i < children.Count; i++)
        {
            childrenTotal += children[i].SubtreeWidth;
            if (i > 0) childrenTotal += gap;
        }

        int blockWidth = Math.Max(nodeTextWidth, childrenTotal);
        int childrenLeft = subtreeLeft + Math.Max(0, (blockWidth - childrenTotal) / 2);
        int currentLeft = childrenLeft;

        // Recurse into children to place their subtrees
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            AssignPositionsTopDown(child, currentLeft, gap, map);
            currentLeft += child.SubtreeWidth + (i < children.Count - 1 ? gap : 0);
        }

        // Now compact siblings as much as allowed, left-to-right
        for (int i = 1; i < children.Count; i++)
        {
            var leftRoot = children[i - 1];
            var rightRoot = children[i];

            int maxShiftLeft = ComputeMaxLeftShift(rightRoot, leftRoot, map, gap);
            if (maxShiftLeft > 0)
            {
                ShiftSubtree(rightRoot, map, maxShiftLeft);
            }
        }

        // Center parent over the compacted children span,
        // then nudge to avoid being collinear with any child.
        var (minLeft, maxRight) = GetChildrenSpan(children, map);
        int proposedCenter = (minLeft + maxRight) / 2;

        // Ensure parent is strictly between leftmost and rightmost child centers
        const int parentChildOffset = 1; // at least one column away
        int leftChildCenter = children.Min(c => c.CenterColumn);
        int rightChildCenter = children.Max(c => c.CenterColumn);

        if (proposedCenter <= leftChildCenter)
            proposedCenter = leftChildCenter + parentChildOffset;

        if (proposedCenter >= rightChildCenter)
            proposedCenter = rightChildCenter - parentChildOffset;

        parent.CenterColumn = proposedCenter;
    }

    // Compute how much we can move the 'right' subtree to the left without overlapping the 'left' subtree,
    // considering all nodes that appear on the same absolute level.
    private static int ComputeMaxLeftShift(NodeInfo right, NodeInfo left, Dictionary<NodeBase, NodeInfo> map, int minGap)
    {
        var rightNodes = EnumerateSubtree(right, map);
        var leftNodes  = EnumerateSubtree(left, map);

        // Build level -> min(leftBound) for right subtree
        var rightMinLeftByLevel = new Dictionary<int, int>();
        foreach (var n in rightNodes)
        {
            int lb = n.CenterColumn - (int)Math.Ceiling(n.Node.Text.Length / 2.0);
            if (!rightMinLeftByLevel.TryGetValue(n.Level, out int curr))
                rightMinLeftByLevel[n.Level] = lb;
            else
                rightMinLeftByLevel[n.Level] = Math.Min(curr, lb);
        }

        // Build level -> max(rightBound) for left subtree
        var leftMaxRightByLevel = new Dictionary<int, int>();
        foreach (var n in leftNodes)
        {
            int rb = n.CenterColumn + (int)Math.Ceiling(n.Node.Text.Length / 2.0);
            if (!leftMaxRightByLevel.TryGetValue(n.Level, out int curr))
                leftMaxRightByLevel[n.Level] = rb;
            else
                leftMaxRightByLevel[n.Level] = Math.Max(curr, rb);
        }

        int allowed = int.MaxValue;
        foreach (var level in rightMinLeftByLevel.Keys)
        {
            if (!leftMaxRightByLevel.TryGetValue(level, out int leftMax)) continue;

            int rightMinLeft = rightMinLeftByLevel[level];
            int required = rightMinLeft - (leftMax + minGap);

            allowed = Math.Min(allowed, required);
        }

        return Math.Max(0, allowed == int.MaxValue ? 0 : allowed);
    }

    private static void ShiftSubtree(NodeInfo root, Dictionary<NodeBase, NodeInfo> map, int shiftLeft)
    {
        if (shiftLeft <= 0) return;
        foreach (var n in EnumerateSubtree(root, map))
        {
            n.CenterColumn -= shiftLeft;
        }
    }

    private static IEnumerable<NodeInfo> EnumerateSubtree(NodeInfo root, Dictionary<NodeBase, NodeInfo> map)
    {
        var stack = new Stack<NodeInfo>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            // Push children
            for (int i = n.Children.Count - 1; i >= 0; i--)
            {
                var c = n.Children[i];
                if (map.TryGetValue(c, out var ci))
                    stack.Push(ci);
            }
        }
    }

    private static (int minLeft, int maxRight) GetChildrenSpan(List<NodeInfo> children, Dictionary<NodeBase, NodeInfo> map)
    {
        int minLeft = int.MaxValue;
        int maxRight = int.MinValue;

        foreach (var child in children)
        {
            foreach (var n in EnumerateSubtree(child, map))
            {
                int half = (int)Math.Ceiling(n.Node.Text.Length / 2.0);
                int lb = n.CenterColumn - half;
                int rb = n.CenterColumn + half;

                if (lb < minLeft) minLeft = lb;
                if (rb > maxRight) maxRight = rb;
            }
        }

        if (minLeft == int.MaxValue) minLeft = 0;
        if (maxRight == int.MinValue) maxRight = 0;

        return (minLeft, maxRight);
    }

    private static string BuildVerticalTree(List<NodeInfo> nodeInfos, Dictionary<NodeBase, NodeBase> parentMap, int leftOffset)
    {
        var levelGroups = nodeInfos.GroupBy(n => n.Level).OrderBy(g => g.Key).ToList();
        var grid = new Dictionary<(int row, int col), char>();

        int rowsPerLevel = 3; // Fixed 3 rows per level (node, connection, space)

        for (int levelIndex = 0; levelIndex < levelGroups.Count; levelIndex++)
        {
            var levelGroup = levelGroups[levelIndex];
            int nodeRow = levelIndex * rowsPerLevel;

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
                grid[(connectionRow + 2, childCol)] = '│';
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