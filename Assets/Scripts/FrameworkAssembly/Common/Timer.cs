using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Timer class for delayed or repeated actions
/// </summary>
public class Timer
{
    public float interval;
    public Action callback;
    public int repeatCount; // -1 = infinite, 0 = stopped, >0 = remaining count
    public bool isPaused = false;
    public bool isRunning = false;

    private Coroutine coroutine;
    private MonoBehaviour owner;

    public Timer(float interval, Action callback, int repeatCount = 1, MonoBehaviour owner = null)
    {
        this.interval = interval;
        this.callback = callback;
        this.repeatCount = repeatCount;
        this.owner = owner;
    }

    public void Start()
    {
        if (isRunning) return;

        if (owner == null)
        {
            owner = TimerManager.Instance;
        }

        coroutine = owner.StartCoroutine(TimerCoroutine());
        isRunning = true;
    }

    public void Stop()
    {
        if (coroutine != null && owner != null)
        {
            owner.StopCoroutine(coroutine);
        }
        isRunning = false;
        repeatCount = 0;
    }

    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        isPaused = false;
    }

    public void Reset()
    {
        Stop();
        isRunning = false;
    }

    private IEnumerator TimerCoroutine()
    {
        while (repeatCount != 0)
        {
            // Count down manually so pause actually freezes the timer
            float elapsed = 0f;
            while (elapsed < interval)
            {
                if (!isPaused)
                    elapsed += Time.deltaTime;
                yield return null;
            }

            try
            {
                callback?.Invoke();
            }
            catch (Exception e)
            {
                Log.e($"Timer callback error: {e.Message}", "Timer");
            }

            if (repeatCount > 0)
            {
                repeatCount--;
            }
        }

        isRunning = false;
    }
}
