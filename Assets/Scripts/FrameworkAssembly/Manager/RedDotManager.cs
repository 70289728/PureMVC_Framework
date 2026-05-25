using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central red dot system manager.
/// 
/// Manages a tree of RedDotNodes. Business logic sets leaf counts;
/// counts bubble up automatically. UI binds to nodes via key.
/// 
/// Lifecycle:
///   1. RedDotManager.Instance.Initialize() — register tree from config
///   2. Business logic: RedDotManager.Instance.SetLeafCount("bag/newItem", 3)
///   3. UI: RedDotBind.Bind("bag") — auto listens to OnCountChanged
/// </summary>
public class RedDotManager : MonoBehaviour
{
    #region Singleton

    private static RedDotManager instance;
    public static RedDotManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("RedDotManager");
                instance = go.AddComponent<RedDotManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    #endregion

    #region Fields

    /// <summary>Root node — key is "root", count = total red dots.</summary>
    private RedDotNode root;

    /// <summary>Flat lookup by full key path.</summary>
    private readonly Dictionary<string, RedDotNode> nodeMap = new Dictionary<string, RedDotNode>();

    /// <summary>True after Initialize() is called.</summary>
    private bool initialized = false;

    /// <summary>True during batch update. Leaf sets are buffered and flushed together.</summary>
    private bool isBatching = false;

    /// <summary>Nodes modified during batch — flushed on EndBatchUpdate().</summary>
    private readonly HashSet<RedDotNode> dirtyNodes = new HashSet<RedDotNode>();

    /// <summary>Pending leaf count sets during batch.</summary>
    private readonly Dictionary<string, int> pendingLeafCounts = new Dictionary<string, int>();

    #endregion

    #region Initialize

    /// <summary>
    /// Initialize the red dot tree. Must be called once before any Set/Bind operations.
    /// Safe to call multiple times — subsequent calls are ignored.
    /// </summary>
    public void Initialize()
    {
        if (initialized)
        {
            Log.d("RedDotManager already initialized, skipping", "RedDotManager");
            return;
        }

        // Create root node
        root = new RedDotNode("root", RedDotDisplayType.None);
        nodeMap["root"] = root;

        // Load tree from config
        LoadTreeFromConfig();

        initialized = true;
        Log.d($"RedDotManager initialized with {nodeMap.Count} nodes", "RedDotManager");
    }

    /// <summary>
    /// Reload the red dot tree from config (hot-update support).
    /// Nodes with the same key are reused (preserving UI bindings).
    /// New nodes are added; removed nodes notify "count=0" to hide their UI.
    /// Call this after a hot update downloads a new RedDotTree.json.
    /// </summary>
    public void ReloadTree()
    {
        if (!initialized)
        {
            Initialize();
            return;
        }

        Log.d("Reloading red dot tree from config...", "RedDotManager");

        // Load new config
        RedDotTreeConfig newConfig = null;
        string hotUpdatePath = System.IO.Path.Combine(Application.persistentDataPath, "HotUpdate", "RedDotTree.json");
        if (System.IO.File.Exists(hotUpdatePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(hotUpdatePath);
                newConfig = JsonUtility.FromJson<RedDotTreeConfig>(json);
            }
            catch (System.Exception e)
            {
                Log.e($"Failed to parse hot-updated red dot tree: {e.Message}", "RedDotManager");
            }
        }
        if (newConfig == null)
        {
            var asset = Resources.Load<TextAsset>("RedDotTree");
            if (asset != null)
            {
                try { newConfig = JsonUtility.FromJson<RedDotTreeConfig>(asset.text); }
                catch (System.Exception e) { Log.e($"Failed to parse red dot tree config: {e.Message}", "RedDotManager"); }
            }
        }
        if (newConfig == null || newConfig.nodes == null || newConfig.nodes.Length == 0)
            newConfig = GetDefaultConfig();

        // Track old node keys so we can detect removals
        var oldKeys = new HashSet<string>(nodeMap.Keys);

        // Detach all children from root (but keep root alive for existing bindings)
        foreach (var child in new List<RedDotNode>(nodeMap.Values))
        {
            if (child != root && child.Parent != null)
                child.Parent.RemoveChild(child.Key);
        }

        // Re-register from new config — reuse existing nodes, create missing ones
        foreach (var entry in newConfig.nodes)
        {
            string nk = entry.key.Replace('/', '.');
            RedDotNode node;
            if (nodeMap.TryGetValue(nk, out node))
            {
                // Existing node — re-attach to correct parent
                // RemoveChild already detached above, now AddChild re-attaches
            }
            else
            {
                node = new RedDotNode(nk, entry.displayType);
                nodeMap[nk] = node;
            }

            // Re-parent
            string np = entry.parent?.Replace('/', '.');
            RedDotNode parentNode = null;
            if (string.IsNullOrEmpty(np) || np == "root")
                parentNode = root;
            else if (!nodeMap.TryGetValue(np, out parentNode))
                parentNode = root;

            if (parentNode != null && node.Parent != parentNode)
                parentNode.AddChild(node);
        }

        // For removed nodes: zero out entire subtree, remove from map
        foreach (string oldKey in oldKeys)
        {
            if (!nodeMap.ContainsKey(oldKey) || !IsInNewConfig(oldKey, newConfig))
            {
                if (nodeMap.TryGetValue(oldKey, out RedDotNode removed))
                {
                    ZeroSubtree(removed);  // Reset all leaves to 0 so parents recalc and UIs hide
                    nodeMap.Remove(oldKey);
                    Log.d($"Red dot node removed: {oldKey}", "RedDotManager");
                }
            }
        }

        Log.d($"Red dot tree reloaded: {nodeMap.Count} nodes", "RedDotManager");
    }

    private static bool IsInNewConfig(string key, RedDotTreeConfig config)
    {
        foreach (var entry in config.nodes)
        {
            if ((entry.key?.Replace('/', '.')) == key) return true;
        }
        return false;
    }

    /// <summary>
    /// Recursively zero all leaf counts in the subtree rooted at node.
    /// After zeroing, ancestors' counts will recalculate via bubble-up.
    /// The removed node itself is then detached from its parent (count→0 triggers recalculation).
    /// </summary>
    private static void ZeroSubtree(RedDotNode node)
    {
        if (node == null) return;

        // Collect children before iterating (to avoid collection-modified-during-enumeration)
        var children = node.GetChildren();
        var childList = new System.Collections.Generic.List<RedDotNode>();
        foreach (var kvp in children)
            childList.Add(kvp.Value);

        foreach (var child in childList)
            ZeroSubtree(child);

        // Zero this node if it's a leaf (non-leaf children already zeroed above, parents will recalc)
        if (children.Count == 0)
        {
            node.SetLeafCount(0);
        }
    }

    /// <summary>
    /// Load red dot tree with hot-update priority.
    /// 
    /// Priority chain:
    ///   1. persistentDataPath/HotUpdate/RedDotTree.json  (hot-update downloaded)
    ///   2. Resources/RedDotTree.json                       (built-in, can be overridden by AB hot-update)
    ///   3. Code-default GetDefaultConfig()                  (fallback)
    /// </summary>
    private void LoadTreeFromConfig()
    {
        RedDotTreeConfig config = null;

        // Priority 1: persistentDataPath — hot-update downloaded
        string hotUpdatePath = System.IO.Path.Combine(Application.persistentDataPath, "HotUpdate", "RedDotTree.json");
        if (System.IO.File.Exists(hotUpdatePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(hotUpdatePath);
                config = JsonUtility.FromJson<RedDotTreeConfig>(json);
                Log.d("Red dot tree loaded from persistentDataPath (hot-updated)", "RedDotManager");
            }
            catch (System.Exception e)
            {
                Log.e($"Failed to parse hot-updated red dot tree: {e.Message}", "RedDotManager");
            }
        }

        // Priority 2: Resources (built-in or AB hot-updated)
        if (config == null)
        {
            var asset = Resources.Load<TextAsset>("RedDotTree");
            if (asset != null)
            {
                try
                {
                    config = JsonUtility.FromJson<RedDotTreeConfig>(asset.text);
                    Log.d("Red dot tree loaded from Resources", "RedDotManager");
                }
                catch (System.Exception e)
                {
                    Log.e($"Failed to parse red dot tree config: {e.Message}", "RedDotManager");
                }
            }
        }

        // Priority 3: Default fallback
        if (config == null || config.nodes == null || config.nodes.Length == 0)
        {
            Log.w("Red dot tree config not found, using default tree", "RedDotManager");
            config = GetDefaultConfig();
        }

        // Register all nodes
        foreach (var entry in config.nodes)
        {
            RegisterNode(entry.key, entry.parent, entry.displayType);
        }
    }

    /// <summary>
    /// Default red dot tree used when config file is missing.
    /// </summary>
    private static RedDotTreeConfig GetDefaultConfig()
    {
        return new RedDotTreeConfig
        {
            nodes = new RedDotTreeEntry[]
            {
                new RedDotTreeEntry { key = "bag",         parent = "root", displayType = RedDotDisplayType.Dot },
                new RedDotTreeEntry { key = "bag/newItem", parent = "bag",  displayType = RedDotDisplayType.Number },
                new RedDotTreeEntry { key = "bag/full",    parent = "bag",  displayType = RedDotDisplayType.Dot },

                new RedDotTreeEntry { key = "shop",          parent = "root", displayType = RedDotDisplayType.Dot },
                new RedDotTreeEntry { key = "shop/dailyFree", parent = "shop", displayType = RedDotDisplayType.Dot },

                new RedDotTreeEntry { key = "mail",     parent = "root", displayType = RedDotDisplayType.Number },
                new RedDotTreeEntry { key = "mail/new", parent = "mail", displayType = RedDotDisplayType.Number },

                new RedDotTreeEntry { key = "task",        parent = "root", displayType = RedDotDisplayType.Number },
                new RedDotTreeEntry { key = "task/claimable", parent = "task", displayType = RedDotDisplayType.Number },

                new RedDotTreeEntry { key = "signin",       parent = "root", displayType = RedDotDisplayType.Dot },
                new RedDotTreeEntry { key = "signin/today", parent = "signin", displayType = RedDotDisplayType.Dot },
            }
        };
    }

    #endregion

    #region Register

    /// <summary>
    /// Register a node in the tree. If the node already exists, no-op.
    /// Use "." as path separator (e.g. "bag/newItem").
    /// </summary>
    public RedDotNode RegisterNode(string key, string parentKey, RedDotDisplayType displayType)
    {
        // If key contains "/" or ".", support both
        string normalizedKey = key.Replace('/', '.');

        if (nodeMap.TryGetValue(normalizedKey, out RedDotNode existing))
            return existing;

        // Ensure parent exists
        RedDotNode parent = null;
        if (!string.IsNullOrEmpty(parentKey) && parentKey != "root")
        {
            string normalizedParent = parentKey.Replace('/', '.');
            if (!nodeMap.TryGetValue(normalizedParent, out parent))
            {
                Log.w($"Red dot parent node not found: {normalizedParent} (for {normalizedKey}) — attaching to root", "RedDotManager");
                parent = root;
            }
        }
        else
        {
            parent = root;
        }

        var node = new RedDotNode(normalizedKey, displayType);
        nodeMap[normalizedKey] = node;

        if (parent != null)
            parent.AddChild(node);

        Log.d($"Registered red dot node: {normalizedKey} (parent={parent?.Key ?? "null"})", "RedDotManager");
        return node;
    }

    #endregion

    #region Set Leaf Count

    /// <summary>
    /// Set the red dot count for a leaf node.
    /// Count bubbles up to all ancestors automatically.
    /// 
    /// During batch update, the set is buffered and applied on EndBatchUpdate().
    /// </summary>
    public void SetLeafCount(string key, int count)
    {
        string normalizedKey = key.Replace('/', '.');

        if (!nodeMap.TryGetValue(normalizedKey, out RedDotNode node))
        {
            Log.w($"Red dot node not found: {normalizedKey} — register it first", "RedDotManager");
            return;
        }

        if (isBatching)
        {
            pendingLeafCounts[normalizedKey] = count;
            dirtyNodes.Add(node);
            return;
        }

        node.SetLeafCount(count);
    }

    /// <summary>
    /// Get the current count for a node.
    /// </summary>
    public int GetCount(string key)
    {
        string normalizedKey = key.Replace('/', '.');
        return nodeMap.TryGetValue(normalizedKey, out RedDotNode node) ? node.Count : 0;
    }

    /// <summary>
    /// Get a node by key. Returns null if not registered.
    /// </summary>
    public RedDotNode GetNode(string key)
    {
        string normalizedKey = key.Replace('/', '.');
        nodeMap.TryGetValue(normalizedKey, out RedDotNode node);
        return node;
    }

    #endregion

    #region Batch Update

    /// <summary>
    /// Begin a batch update. All SetLeafCount calls are buffered and applied atomically
    /// on EndBatchUpdate(). Reduces UI callback overhead when multiple counts change.
    /// 
    /// Usage: call in Proxy methods that update multiple red dots at once.
    /// </summary>
    public void BeginBatchUpdate()
    {
        isBatching = true;
        pendingLeafCounts.Clear();
        dirtyNodes.Clear();
    }

    /// <summary>
    /// End a batch update. Applies all buffered SetLeafCount calls and
    /// fires OnCountChanged for each affected node exactly once.
    /// </summary>
    public void EndBatchUpdate()
    {
        if (!isBatching) return;
        isBatching = false;

        // Apply all pending leaf counts
        foreach (var kvp in pendingLeafCounts)
        {
            if (nodeMap.TryGetValue(kvp.Key, out RedDotNode node))
                node.SetLeafCount(kvp.Value);
        }

        pendingLeafCounts.Clear();
        dirtyNodes.Clear();
    }

    #endregion

    #region UI Bind / Unbind

    /// <summary>
    /// Bind a callback to a red dot node's OnCountChanged event.
    /// UI components use this to auto-update visuals.
    /// </summary>
    public void BindUI(string key, System.Action<RedDotNode> callback)
    {
        string normalizedKey = key.Replace('/', '.');
        if (nodeMap.TryGetValue(normalizedKey, out RedDotNode node))
        {
            node.OnCountChanged += callback;

            // Immediately invoke so UI shows current state
            callback(node);
        }
    }

    /// <summary>
    /// Unbind a callback from a red dot node.
    /// </summary>
    public void UnbindUI(string key, System.Action<RedDotNode> callback)
    {
        string normalizedKey = key.Replace('/', '.');
        if (nodeMap.TryGetValue(normalizedKey, out RedDotNode node))
        {
            node.OnCountChanged -= callback;
        }
    }

    #endregion
}

// ── Config Data Types ────────────────────────────────────────

/// <summary>
/// Red dot tree configuration. Loaded from Resources/Config/reddot_tree.json.
/// </summary>
[System.Serializable]
public class RedDotTreeConfig
{
    public RedDotTreeEntry[] nodes;
}

[System.Serializable]
public class RedDotTreeEntry
{
    public string key;
    public string parent;
    public RedDotDisplayType displayType;
}
