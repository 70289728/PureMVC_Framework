using System;
using System.Collections.Generic;

/// <summary>
/// Red dot tree node.
/// Leaf nodes: count set directly by business logic.
/// Non-leaf nodes: count = sum of all children's counts.
/// </summary>
public class RedDotNode
{
    /// <summary>Unique identifier, e.g. "bag/newItem".</summary>
    public string Key { get; }

    /// <summary>Current red dot count. Leaf = set manually; non-leaf = sum of children.</summary>
    public int Count { get; private set; }

    /// <summary>How the red dot is displayed on UI.</summary>
    public RedDotDisplayType DisplayType { get; }

    /// <summary>Parent node. Null for root.</summary>
    public RedDotNode Parent { get; private set; }

    /// <summary>Child nodes mapped by key suffix (relative to this node).</summary>
    private readonly Dictionary<string, RedDotNode> children = new Dictionary<string, RedDotNode>();

    /// <summary>Fired when Count changes. UI Bind components listen to this.</summary>
    public event Action<RedDotNode> OnCountChanged;

    public RedDotNode(string key, RedDotDisplayType displayType = RedDotDisplayType.Dot)
    {
        Key = key;
        DisplayType = displayType;
        Count = 0;
    }

    // ── Tree Operations ──────────────────────────────────────

    /// <summary>
    /// Attach a child node. Updates parent reference and bubbles count up.
    /// </summary>
    public void AddChild(RedDotNode child)
    {
        if (child == null || children.ContainsKey(child.Key))
            return;

        children[child.Key] = child;
        child.Parent = this;

        if (child.Count > 0)
            RecalculateCount();
    }

    /// <summary>
    /// Detach a child node. Bubbles count change up.
    /// </summary>
    public void RemoveChild(string childKey)
    {
        if (children.TryGetValue(childKey, out RedDotNode child))
        {
            children.Remove(childKey);
            child.Parent = null;

            if (child.Count > 0)
                RecalculateCount();
        }
    }

    /// <summary>
    /// Get a child node by key.
    /// </summary>
    public RedDotNode GetChild(string childKey)
    {
        children.TryGetValue(childKey, out RedDotNode child);
        return child;
    }

    /// <summary>
    /// Get all children. Used by RedDotManager for tree traversal.
    /// </summary>
    public IReadOnlyDictionary<string, RedDotNode> GetChildren()
    {
        return children;
    }

    // ── Count Management ─────────────────────────────────────

    /// <summary>
    /// Set count for a leaf node. Only valid for leaf nodes.
    /// Automatically bubbles count change up the tree.
    /// </summary>
    public void SetLeafCount(int value)
    {
        if (value < 0) value = 0;

        // Non-leaf nodes are driven by children — ignore manual set on non-leaf
        if (children.Count > 0)
            return;

        if (Count == value)
            return;

        Count = value;
        OnCountChanged?.Invoke(this);
        BubbleUp();
    }

    /// <summary>
    /// Notify self, then walk upward: force parent to recalculate, notify parent, continue.
    /// One walk — no recursive re-entry.
    /// </summary>
    private void BubbleUp()
    {
        RedDotNode current = this.Parent;
        while (current != null)
        {
            int sum = 0;
            foreach (var child in current.children.Values)
                sum += child.Count;

            if (current.Count != sum)
            {
                current.Count = sum;
                current.OnCountChanged?.Invoke(current);
            }

            current = current.Parent;
        }
    }

    /// <summary>
    /// Recalculate this node's count from children and bubble up.
    /// Called when child structure changes, not when leaf count is set directly.
    /// </summary>
    internal void RecalculateCount()
    {
        int sum = 0;
        foreach (var child in children.Values)
            sum += child.Count;

        if (Count == sum)
            return;

        Count = sum;
        OnCountChanged?.Invoke(this);
        BubbleUp();
    }

    public override string ToString()
    {
        return $"[RedDot] {Key} = {Count} ({DisplayType})";
    }
}

/// <summary>
/// How to display the red dot on UI.
/// </summary>
public enum RedDotDisplayType
{
    /// <summary>Show only a red dot (no number).</summary>
    Dot,

    /// <summary>Show the count number.</summary>
    Number,

    /// <summary>No visual — used for logical grouping only.</summary>
    None,
}
