using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for handling timers and delayed/repeated actions
/// </summary>
public class TimerManager : MonoBehaviour
{
    #region Singleton
    private static TimerManager instance;
    public static TimerManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("TimerManager");
                instance = go.AddComponent<TimerManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Member Variables
    private List<Timer> activeTimers = new List<Timer>();
    private bool isPaused = false;
    #endregion

    #region Public Methods
    /// <summary>
    /// Execute an action after a delay
    /// </summary>
    /// <param name="delay">Delay in seconds</param>
    /// <param name="callback">Action to execute</param>
    /// <returns>Timer instance for control</returns>
    public Timer DelayCall(float delay, Action callback)
    {
        Timer timer = new Timer(delay, callback, 1, this);
        timer.Start();
        activeTimers.Add(timer);
        Log.d($"DelayCall created: {delay}s", "TimerManager");
        return timer;
    }

    /// <summary>
    /// Repeat an action at intervals
    /// </summary>
    /// <param name="interval">Interval in seconds</param>
    /// <param name="callback">Action to execute</param>
    /// <param name="repeatCount">Number of repeats (-1 for infinite)</param>
    /// <returns>Timer instance for control</returns>
    public Timer RepeatCall(float interval, Action callback, int repeatCount = -1)
    {
        Timer timer = new Timer(interval, callback, repeatCount, this);
        timer.Start();
        activeTimers.Add(timer);
        Log.d($"RepeatCall created: {interval}s, count: {repeatCount}", "TimerManager");
        return timer;
    }

    /// <summary>
    /// Create a timer without starting it
    /// </summary>
    public Timer CreateTimer(float interval, Action callback, int repeatCount = 1)
    {
        Timer timer = new Timer(interval, callback, repeatCount, this);
        activeTimers.Add(timer);
        return timer;
    }

    /// <summary>
    /// Stop a specific timer
    /// </summary>
    public void StopTimer(Timer timer)
    {
        if (timer != null)
        {
            timer.Stop();
            activeTimers.Remove(timer);
        }
    }

    /// <summary>
    /// Stop all timers
    /// </summary>
    public void StopAllTimers()
    {
        foreach (var timer in activeTimers)
        {
            timer.Stop();
        }
        activeTimers.Clear();
        Log.d("All timers stopped", "TimerManager");
    }

    /// <summary>
    /// Pause all timers
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        foreach (var timer in activeTimers)
        {
            timer.Pause();
        }
        Log.d("TimerManager paused", "TimerManager");
    }

    /// <summary>
    /// Resume all timers
    /// </summary>
    public void Resume()
    {
        isPaused = false;
        foreach (var timer in activeTimers)
        {
            timer.Resume();
        }
        Log.d("TimerManager resumed", "TimerManager");
    }

    /// <summary>
    /// Get active timer count
    /// </summary>
    public int GetActiveTimerCount()
    {
        // Clean up finished timers
        activeTimers.RemoveAll(t => !t.isRunning);
        return activeTimers.Count;
    }

    /// <summary>
    /// Get statistics string
    /// </summary>
    public string GetStatistics()
    {
        return $"Active Timers: {GetActiveTimerCount()}, Paused: {isPaused}";
    }
    #endregion

    #region Unity Lifecycle
    void Update()
    {
        // Clean up finished timers periodically
        if (Time.frameCount % 60 == 0) // Every 60 frames
        {
            activeTimers.RemoveAll(t => !t.isRunning);
        }
    }

    void OnDestroy()
    {
        StopAllTimers();
    }
    #endregion

    #region Helper Methods - Coroutine Utilities
    /// <summary>
    /// Wait for a condition to be true
    /// </summary>
    public Coroutine WaitUntil(Func<bool> condition, Action callback)
    {
        return StartCoroutine(WaitUntilCoroutine(condition, callback));
    }

    private IEnumerator WaitUntilCoroutine(Func<bool> condition, Action callback)
    {
        yield return new WaitUntil(condition);
        callback?.Invoke();
    }

    /// <summary>
    /// Wait for a condition to be false
    /// </summary>
    public Coroutine WaitWhile(Func<bool> condition, Action callback)
    {
        return StartCoroutine(WaitWhileCoroutine(condition, callback));
    }

    private IEnumerator WaitWhileCoroutine(Func<bool> condition, Action callback)
    {
        yield return new WaitWhile(condition);
        callback?.Invoke();
    }

    /// <summary>
    /// Execute action at end of frame
    /// </summary>
    public Coroutine ExecuteAtEndOfFrame(Action callback)
    {
        return StartCoroutine(ExecuteAtEndOfFrameCoroutine(callback));
    }

    private IEnumerator ExecuteAtEndOfFrameCoroutine(Action callback)
    {
        yield return new WaitForEndOfFrame();
        callback?.Invoke();
    }

    /// <summary>
    /// Execute action after fixed update
    /// </summary>
    public Coroutine ExecuteAtFixedUpdate(Action callback)
    {
        return StartCoroutine(ExecuteAtFixedUpdateCoroutine(callback));
    }

    private IEnumerator ExecuteAtFixedUpdateCoroutine(Action callback)
    {
        yield return new WaitForFixedUpdate();
        callback?.Invoke();
    }
    #endregion
}
