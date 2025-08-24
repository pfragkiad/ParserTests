using System.Text;

namespace ParserLibrary.ExpressionTree;

/// <summary>
/// Vertical tree printing extensions that create downward-flowing tree layouts
/// </summary>
public static class NodeBasePrintExtensionsVertical
{
    public const string DefaultMissingChildPlaceholder = "<e>";

    /// <summary>
    /// Vertical tree with centered nodes and proper box-drawing connections.
    /// </summary>
    /// <param name="root">Root node.</param>
    /// <param name="leftOffset">Extra left margin.</param>
    /// <param name="gap">Child separation factor k (spaces between adjacent child subtrees = 1 + 2*k).</param>
    /// <param name="missingChildPlaceholder">
    /// Text to show for a missing Left or Right child when the sibling exists (pure leaves still suppressed).
    /// Set to null or empty string to disable placeholder injection.
    /// </param>
    public static string ToVerticalTreeString(this NodeBase root, int leftOffset = 0, int gap = 0,
        string? missingChildPlaceholder = DefaultMissingChildPlaceholder)
    {
        if (root is null) return string.Empty;

        var nodeInfos = new List<NodeInfo>();
        var parentMap = new Dictionary<NodeBase, NodeBase>();

        CollectNodeInfos(root, 0, nodeInfos, parentMap, null, missingChildPlaceholder);

        CalculatePositions(nodeInfos, leftOffset, gap);

        return BuildVerticalTree(nodeInfos, parentMap, leftOffset);
    }

    /// <summary>
    /// Print vertical tree to console.
    /// </summary>
    public static void PrintVerticalTree(this NodeBase root, int leftOffset = 0, int gap = 0, string? missingChildPlaceholder = DefaultMissingChildPlaceholder)
        => Console.WriteLine(root.ToVerticalTreeString(leftOffset, gap, missingChildPlaceholder));

    #region Private Helper Classes and Methods

    private class NodeInfo
    {
        public required NodeBase Node { get; set; }
        public int Level { get; set; }
        public int TraversalOrder { get; set; }
        public List<NodeBase> Children { get; set; } = [];
        public int CenterColumn { get; set; }
        public int LeftBound { get; set; }
        public int RightBound { get; set; }
        public int SubtreeWidth { get; set; }
    }

    /// <summary>
    /// Placeholder node used only for visualization when either Left or Right child is missing (but not both).
    /// </summary>
    private sealed class PlaceholderNode : NodeBase
    {
        public PlaceholderNode(string text) : base(text) { }
    }

    private static void CollectNodeInfos(
        NodeBase? node,
        int level,
        List<NodeInfo> nodeInfos,
        Dictionary<NodeBase, NodeBase> parentMap,
        NodeBase? parent,
        string? missingChildPlaceholder)
    {
        if (node is null) return;

        var children = new List<NodeBase>();

        bool hasLeft = node.Left is not null;
        bool hasRight = node.Right is not null;
        bool inject = !string.IsNullOrEmpty(missingChildPlaceholder) && (hasLeft || hasRight);

        if (inject)
        {
            // Preserve positional semantics: always add two entries for L/R when at least one exists.
            children.Add(hasLeft ? node.Left! : new PlaceholderNode(missingChildPlaceholder!));
            children.Add(hasRight ? node.Right! : new PlaceholderNode(missingChildPlaceholder!));
        }

        if (node.Other is not null)
            children.AddRange(node.Other);

        var nodeInfo = new NodeInfo
        {
            Node = node,
            Level = level,
            TraversalOrder = nodeInfos.Count,
            Children = children
        };

        nodeInfos.Add(nodeInfo);

        if (parent is not null)
            parentMap[node] = parent;

        foreach (var child in children)
            CollectNodeInfos(child, level + 1, nodeInfos, parentMap, node, missingChildPlaceholder);
    }

    private static void CalculatePositions(List<NodeInfo> nodeInfos, int leftOffset, int gapFactor)
    {
        int k = Math.Max(0, gapFactor);
        int minGap = 2 + (2 * k);

        var map = nodeInfos.ToDictionary(n => n.Node, n => n);

        foreach (var n in nodeInfos.OrderByDescending(n => n.Level))
        {
            int nodeTextWidth = Math.Max(1, n.Node.Text.Length);

            if (n.Children.Count == 0)
            {
                n.SubtreeWidth = nodeTextWidth;
                continue;
            }

            var childInfos = n.Children.Select(c => map[c]).ToList();
            int childrenTotal = 0;
            for (int i = 0; i < childInfos.Count; i++)
            {
                childrenTotal += childInfos[i].SubtreeWidth;
                if (i > 0) childrenTotal += minGap;
            }

            n.SubtreeWidth = Math.Max(nodeTextWidth, childrenTotal);
        }

        var root = nodeInfos.First(n => n.Level == 0);
        AssignPositionsTopDown(root, leftOffset, minGap, map);

        foreach (var n in nodeInfos)
        {
            BoundsFromCenter(n.CenterColumn, n.Node.Text.Length, out int lb, out int rb);
            n.LeftBound = lb;
            n.RightBound = rb;
        }
    }

    private static void AssignPositionsTopDown(NodeInfo parent, int subtreeLeft, int gap, Dictionary<NodeBase, NodeInfo> map)
    {
        int nodeTextWidth = Math.Max(1, parent.Node.Text.Length);

        if (parent.Children.Count == 0)
        {
            parent.CenterColumn = subtreeLeft + nodeTextWidth / 2;
            return;
        }

        var children = parent.Children.Select(c => map[c]).ToList();

        int childrenTotal = 0;
        for (int i = 0; i < children.Count; i++)
        {
            childrenTotal += children[i].SubtreeWidth;
            if (i > 0) childrenTotal += gap;
        }

        int blockWidth = Math.Max(nodeTextWidth, childrenTotal);
        int childrenLeft = subtreeLeft + Math.Max(0, (blockWidth - childrenTotal) / 2);
        int currentLeft = childrenLeft;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            AssignPositionsTopDown(child, currentLeft, gap, map);
            currentLeft += child.SubtreeWidth + (i < children.Count - 1 ? gap : 0);
        }

        for (int i = 1; i < children.Count; i++)
        {
            var leftRoot = children[i - 1];
            var rightRoot = children[i];
            int maxShiftLeft = ComputeMaxLeftShift(rightRoot, leftRoot, map, gap);
            if (maxShiftLeft > 0)
                ShiftSubtree(rightRoot, map, maxShiftLeft);
        }

        if (children.Count == 1)
        {
            var child = children[0];
            bool isLeftChild = ReferenceEquals(parent.Node.Left, child.Node);
            bool isRightChild = ReferenceEquals(parent.Node.Right, child.Node);
            if (!isLeftChild && !isRightChild) isLeftChild = true;
            const int singleChildOffset = 1;
            parent.CenterColumn = child.CenterColumn + (isLeftChild ? +singleChildOffset : -singleChildOffset);
            return;
        }

        var ordered = children.OrderBy(c => c.CenterColumn).ToList();
        var leftMost = ordered.First();
        var rightMost = ordered.Last();
        int span = rightMost.CenterColumn - leftMost.CenterColumn;

        if ((span & 1) == 1)
        {
            ShiftSubtreeBy(rightMost, map, +1);
            ordered = children.OrderBy(c => c.CenterColumn).ToList();
            leftMost = ordered.First();
            rightMost = ordered.Last();
        }

        parent.CenterColumn = (leftMost.CenterColumn + rightMost.CenterColumn) / 2;
    }

    private static int ComputeMaxLeftShift(NodeInfo right, NodeInfo left, Dictionary<NodeBase, NodeInfo> map, int minGap)
    {
        var rightNodes = EnumerateSubtree(right, map);
        var leftNodes = EnumerateSubtree(left, map);

        var rightMinLeftByLevel = new Dictionary<int, int>();
        foreach (var n in rightNodes)
        {
            BoundsFromCenter(n.CenterColumn, n.Node.Text.Length, out int lb, out _);
            if (!rightMinLeftByLevel.TryGetValue(n.Level, out int curr))
                rightMinLeftByLevel[n.Level] = lb;
            else
                rightMinLeftByLevel[n.Level] = Math.Min(curr, lb);
        }

        var leftMaxRightByLevel = new Dictionary<int, int>();
        foreach (var n in leftNodes)
        {
            BoundsFromCenter(n.CenterColumn, n.Node.Text.Length, out _, out int rb);
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
            n.CenterColumn -= shiftLeft;
    }

    private static void ShiftSubtreeBy(NodeInfo root, Dictionary<NodeBase, NodeInfo> map, int delta)
    {
        if (delta == 0) return;
        foreach (var n in EnumerateSubtree(root, map))
            n.CenterColumn += delta;
    }

    private static IEnumerable<NodeInfo> EnumerateSubtree(NodeInfo root, Dictionary<NodeBase, NodeInfo> map)
    {
        var stack = new Stack<NodeInfo>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            for (int i = n.Children.Count - 1; i >= 0; i--)
            {
                var c = n.Children[i];
                if (map.TryGetValue(c, out var ci))
                    stack.Push(ci);
            }
        }
    }

    private static void BoundsFromCenter(int center, int textLen, out int leftBound, out int rightBound)
    {
        int startCol = center - textLen / 2;
        leftBound = startCol;
        rightBound = startCol + textLen - 1;
    }

    private static string BuildVerticalTree(List<NodeInfo> nodeInfos, Dictionary<NodeBase, NodeBase> parentMap, int leftOffset)
    {
        var levelGroups = nodeInfos.GroupBy(n => n.Level).OrderBy(g => g.Key).ToList();
        var grid = new Dictionary<(int row, int col), char>();

        int rowsPerLevel = 3;

        for (int levelIndex = 0; levelIndex < levelGroups.Count; levelIndex++)
        {
            var levelGroup = levelGroups[levelIndex];
            int nodeRow = levelIndex * rowsPerLevel;

            foreach (var nodeInfo in levelGroup)
                PlaceNodeInGrid(grid, nodeInfo, nodeRow);

            if (levelIndex >= levelGroups.Count - 1) continue;

            foreach (var nodeInfo in levelGroup)
            {
                if (nodeInfo.Children.Count > 0)
                    DrawConnections(grid, nodeInfo, nodeInfos, nodeRow);
            }
        }

        return GridToString(grid);
    }

    private static void PlaceNodeInGrid(Dictionary<(int row, int col), char> grid, NodeInfo nodeInfo, int row)
    {
        string nodeText = nodeInfo.Node.Text;
        int startCol = nodeInfo.CenterColumn - nodeText.Length / 2;
        startCol = Math.Max(0, startCol);

        for (int i = 0; i < nodeText.Length; i++)
            grid[(row, startCol + i)] = nodeText[i];
    }

    private static void DrawConnections(Dictionary<(int row, int col), char> grid, NodeInfo parentNode, List<NodeInfo> allNodes, int parentRow)
    {
        var children = allNodes.Where(n => parentNode.Children.Contains(n.Node)).ToList();
        if (children.Count == 0) return;

        int parentCol = parentNode.CenterColumn;
        int connectionRow = parentRow + 1;

        if (children.Count == 1)
        {
            var child = children[0];
            int childCol = child.CenterColumn;

            grid[(connectionRow, parentCol)] = '│';

            int startCol = Math.Min(parentCol, childCol);
            int endCol = Math.Max(parentCol, childCol);
            for (int c = startCol; c <= endCol; c++)
                grid[(connectionRow + 1, c)] = '─';

            if (childCol < parentCol)
            {
                grid[(connectionRow + 1, parentCol)] = '┘';
                grid[(connectionRow + 1, childCol)] = '└';
            }
            else
            {
                grid[(connectionRow + 1, parentCol)] = '└';
                grid[(connectionRow + 1, childCol)] = '┘';
            }

            grid[(connectionRow + 2, childCol)] = '│';
            return;
        }

        grid[(connectionRow, parentCol)] = '│';

        int leftmostCol = children.Min(c => c.CenterColumn);
        int rightmostCol = children.Max(c => c.CenterColumn);

        for (int c = leftmostCol; c <= rightmostCol; c++)
            grid[(connectionRow + 1, c)] = '─';

        grid[(connectionRow + 1, parentCol)] = '┴';

        foreach (var child in children)
        {
            int childCol = child.CenterColumn;
            if (childCol < parentCol) grid[(connectionRow + 1, childCol)] = '┌';
            else if (childCol > parentCol) grid[(connectionRow + 1, childCol)] = '┐';
            else grid[(connectionRow + 1, childCol)] = '┴';
            grid[(connectionRow + 2, childCol)] = '│';
        }
    }

    private static string GridToString(Dictionary<(int row, int col), char> grid)
    {
        if (grid.Count == 0) return string.Empty;

        int maxRow = grid.Keys.Max(k => k.row);
        int maxCol = grid.Keys.Max(k => k.col);

        var lines = new List<string>();
        for (int row = 0; row <= maxRow; row++)
        {
            var line = new StringBuilder();
            for (int col = 0; col <= maxCol; col++)
                line.Append(grid.TryGetValue((row, col), out char c) ? c : ' ');
            lines.Add(line.ToString().TrimEnd());
        }

        return string.Join(Environment.NewLine, lines);
    }

    #endregion
}