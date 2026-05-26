using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized frame-split work scheduler.
/// 
/// All long-running batch work goes through here instead of blocking the main thread.
/// Each frame processes up to 'BudgetPerFrame' items total across all registered queues.
/// 
/// Usage:
///   var queue = FrameScheduler.Instance.CreateQueue<int>(item => AddFriendUI(item), 5);
///   foreach (var friend in friends) queue.Enqueue(friend);
///   queue.OnComplete += () => Log.d("All friends loaded");
/// </summary>
public class FrameScheduler : MonoBehaviour
{
    #region Singleton

    private static FrameScheduler _instance;
    public static FrameScheduler Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("FrameScheduler");
                _instance = go.AddComponent<FrameScheduler>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    #endregion

    #region Config

    /// <summary>Total work items processed per frame across all queues.</summary>
    public int BudgetPerFrame = 20;

    #endregion

    #region State

    private readonly List<IFrameWorkQueue> _queues = new List<IFrameWorkQueue>();

    /// <summary>
    /// Get remaining work count across all queues.
    /// </summary>
    public int TotalRemaining
    {
        get
        {
            int total = 0;
            foreach (var q in _queues) total += q.Count;
            return total;
        }
    }

    /// <summary>
    /// Pause all frame-split processing.
    /// Call during loading screens or heavy sync operations.
    /// </summary>
    public bool IsPaused { get; set; } = false;

    #endregion

    #region Public API

    /// <summary>
    /// Create a new work queue for type T.
    /// Each frame, up to 'budget' items from this queue are processed.
    /// </summary>
    public FrameWorkQueue<T> CreateQueue<T>(Action<T> handler, int budget)
    {
        var queue = new FrameWorkQueue<T>(handler, budget);
        _queues.Add(queue);
        return queue;
    }

    /// <summary>
    /// Remove a queue from the scheduler (e.g. after completion).
    /// </summary>
    public void RemoveQueue<T>(FrameWorkQueue<T> queue)
    {
        _queues.Remove(queue);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (IsPaused) return;

        int remaining = BudgetPerFrame;

        // Round-robin: each queue gets a turn, up to its own budget
        for (int i = _queues.Count - 1; i >= 0; i--)
        {
            var q = _queues[i];
            if (q.IsDone)
            {
                _queues.RemoveAt(i);
                q.FireComplete();
                continue;
            }

            int taken = q.Process(Math.Min(remaining, q.Budget));
            remaining -= taken;
            if (remaining <= 0) break;
        }
    }

    #endregion
}

#region FrameWorkQueue

/// <summary>
/// Non-generic interface for scheduler management.
/// </summary>
internal interface IFrameWorkQueue
{
    int Count { get; }
    int Budget { get; }
    bool IsDone { get; }
    int Process(int budget);
    void FireComplete();
}

/// <summary>
/// Typed frame-split work queue. Enqueue items; Process() called by FrameScheduler each frame.
/// </summary>
public class FrameWorkQueue<T> : IFrameWorkQueue
{
    private readonly Queue<T> _queue = new Queue<T>();
    private readonly Action<T> _handler;

    public int Budget { get; }
    public int Count => _queue.Count;
    public bool IsDone => _queue.Count == 0;

    /// <summary>Fired when the queue becomes empty.</summary>
    public event Action OnComplete;

    public FrameWorkQueue(Action<T> handler, int budget)
    {
        _handler = handler;
        Budget = budget;
    }

    public void Enqueue(T item) => _queue.Enqueue(item);

    /// <summary>Process up to 'budget' items. Returns count processed.</summary>
    public int Process(int budget)
    {
        int processed = 0;
        while (processed < budget && _queue.Count > 0)
        {
            _handler(_queue.Dequeue());
            processed++;
        }
        return processed;
    }

    public void Clear() => _queue.Clear();

    void IFrameWorkQueue.FireComplete() => OnComplete?.Invoke();
}

#endregion
