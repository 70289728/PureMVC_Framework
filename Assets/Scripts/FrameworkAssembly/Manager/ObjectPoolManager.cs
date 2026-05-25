using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple GameObject object pool.
/// Recycles GameObjects to avoid repeated Instantiate/Destroy overhead.
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    #region Singleton
    private static ObjectPoolManager instance;
    public static ObjectPoolManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("ObjectPoolManager");
                instance = go.AddComponent<ObjectPoolManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Member Variables
    private Dictionary<string, Queue<GameObject>> pool = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, GameObject> prefabRegistry = new Dictionary<string, GameObject>();
    private Transform poolRoot;

    /// <summary>
    /// Default maximum pool size per key. When exceeded, recycled objects are destroyed instead of enqueued.
    /// Set to 0 for unlimited (not recommended for production).
    /// </summary>
    private const int DefaultMaxPoolSize = 50;
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        poolRoot = new GameObject("PoolRoot").transform;
        poolRoot.SetParent(transform);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Pre-register a prefab under a key and optionally pre-warm the pool
    /// </summary>
    public void Register(string key, GameObject prefab, int preWarmCount = 0)
    {
        if (!prefabRegistry.ContainsKey(key))
            prefabRegistry[key] = prefab;

        for (int i = 0; i < preWarmCount; i++)
        {
            GameObject go = CreateNew(key, prefab);
            Recycle(key, go);
        }

        Log.d($"Pool registered: {key} (preWarm: {preWarmCount})", "ObjectPoolManager");
    }

    /// <summary>
    /// Get a GameObject from the pool. Creates a new one if pool is empty.
    /// </summary>
    public GameObject Get(string key, Transform parent = null)
    {
        if (pool.TryGetValue(key, out Queue<GameObject> queue) && queue.Count > 0)
        {
            GameObject go = queue.Dequeue();
            go.transform.SetParent(parent);
            go.SetActive(true);
            return go;
        }

        if (prefabRegistry.TryGetValue(key, out GameObject prefab))
        {
            GameObject go = CreateNew(key, prefab);
            go.transform.SetParent(parent);
            go.SetActive(true);
            return go;
        }

        Log.e($"Pool key not registered: {key}. Call Register() first.", "ObjectPoolManager");
        return null;
    }

    /// <summary>
    /// Get a typed component from a pooled GameObject
    /// </summary>
    public T Get<T>(string key, Transform parent = null) where T : Component
    {
        GameObject go = Get(key, parent);
        return go != null ? go.GetComponent<T>() : null;
    }

    /// <summary>
    /// Return a GameObject to the pool. If pool exceeds max size, destroy the object instead.
    /// </summary>
    public void Recycle(string key, GameObject go)
    {
        if (go == null) return;

        if (!pool.ContainsKey(key))
            pool[key] = new Queue<GameObject>();

        // Enforce max pool size to prevent unbounded memory growth
        if (DefaultMaxPoolSize > 0 && pool[key].Count >= DefaultMaxPoolSize)
        {
            Log.w($"Pool '{key}' full ({DefaultMaxPoolSize} items). Destroying excess object.", "ObjectPoolManager");
            Destroy(go);
            return;
        }

        go.SetActive(false);
        go.transform.SetParent(poolRoot);
        pool[key].Enqueue(go);
    }

    /// <summary>
    /// Clear all pooled objects for a specific key
    /// </summary>
    public void Clear(string key)
    {
        if (!pool.ContainsKey(key)) return;

        while (pool[key].Count > 0)
        {
            GameObject go = pool[key].Dequeue();
            if (go != null) Destroy(go);
        }
        pool.Remove(key);
        Log.d($"Pool cleared: {key}", "ObjectPoolManager");
    }

    /// <summary>
    /// Clear all pools
    /// </summary>
    public void ClearAll()
    {
        foreach (var key in new List<string>(pool.Keys))
            Clear(key);

        Log.d("All pools cleared", "ObjectPoolManager");
    }

    /// <summary>
    /// Get current pool size for a key
    /// </summary>
    public int GetPoolSize(string key)
    {
        return pool.TryGetValue(key, out Queue<GameObject> q) ? q.Count : 0;
    }
    #endregion

    #region Helper
    private GameObject CreateNew(string key, GameObject prefab)
    {
        GameObject go = Instantiate(prefab, poolRoot);
        go.name = key;
        go.SetActive(false);
        return go;
    }
    #endregion
}
