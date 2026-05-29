using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime player for a timeline. Manages clip lifecycle — entering, updating, and exiting clips
/// as time advances. Driven by TimelineManager.
/// </summary>
public class TimelinePlayer
{
    /// <summary>If true, auto-destroy (remove from TimelineManager) on completion.</summary>
    public bool AutoDestroy { get; set; }

    #region Properties
    public string PlayerId { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsCompleted { get; private set; }
    public float CurrentTime { get; private set; }
    public float Duration { get; private set; }
    public TimelineTimeMode TimeMode { get; private set; }
    #endregion

    #region Events
    /// <summary>Fired when the timeline finishes (currentTime reaches Duration) or is stopped.</summary>
    public event Action<string> OnCompleted;
    #endregion

    #region Internal State
    private List<TimelineClip> clips;              // sorted by startTime, set at construction
    private int nextClipIndex;                      // first clip not yet entered
    private List<int> activeClipIndices;            // indices of clips currently active (duration > 0)
    private float lastEnterCheckTime;               // prevTime used for dedup — resets on loop
    private bool loop;                              // loop back to start on completion
    #endregion

    #region Construction
    /// <summary>
    /// Create a new timeline player.
    /// Clips are sorted by startTime internally.
    /// Duration is auto-computed as max(startTime + duration) across all clips.
    /// </summary>
    public TimelinePlayer(string playerId, List<TimelineClip> clips, TimelineTimeMode timeMode = TimelineTimeMode.Scaled, bool loop = false)
    {
        PlayerId = playerId ?? Guid.NewGuid().ToString();
        TimeMode = timeMode;
        this.loop = loop;

        // Sort clips by startTime, then by duration ascending (instant clips first)
        this.clips = new List<TimelineClip>(clips);
        this.clips.Sort((a, b) =>
        {
            int cmp = a.startTime.CompareTo(b.startTime);
            if (cmp != 0) return cmp;
            return a.duration.CompareTo(b.duration);
        });

        // Compute total duration
        Duration = 0f;
        for (int i = 0; i < this.clips.Count; i++)
        {
            float end = this.clips[i].startTime + this.clips[i].duration;
            if (end > Duration) Duration = end;
        }

        activeClipIndices = new List<int>();
        nextClipIndex = 0;
        lastEnterCheckTime = -1f; // -1 = no previous tick (construction / loop / seek)
        AutoDestroy = true;
        CurrentTime = 0f;
        IsPlaying = false;
        IsPaused = false;
        IsCompleted = false;
    }
    #endregion

    #region Playback Control
    /// <summary>Start or resume playback from current position.</summary>
    public void Play()
    {
        if (IsCompleted)
        {
            Log.w($"TimelinePlayer [{PlayerId}] already completed, cannot replay. Use Seek(0) first.", "TimelinePlayer");
            return;
        }
        IsPlaying = true;
        IsPaused = false;
    }

    /// <summary>Pause playback. Resume with Play().</summary>
    public void Pause()
    {
        IsPaused = true;
    }

    /// <summary>Resume paused playback.</summary>
    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
    }

    /// <summary>
    /// Stop the timeline immediately. Exits all active clips and fires OnCompleted.
    /// </summary>
    public void Stop()
    {
        if (IsCompleted) return;

        ExitAllActiveClips();
        IsPlaying = false;
        IsPaused = false;
        IsCompleted = true;
        FireCompleted();
    }

    /// <summary>
    /// Jump to a specific time.
    /// Exits clips that end before the target time, enters clips that start at/before it.
    /// Duration=0 clips at exactly the target time will fire OnEnter+OnExit.
    /// </summary>
    public void Seek(float time)
    {
        float newTime = Mathf.Clamp(time, 0f, Duration);

        // Exit all currently active clips
        ExitAllActiveClips();

        // Find clips that should have started by newTime
        nextClipIndex = 0;
        for (int i = 0; i < clips.Count; i++)
        {
            TimelineClip clip = clips[i];
            if (clip.startTime > newTime)
            {
                nextClipIndex = i;
                break;
            }

            TimelineContext ctx = MakeContext(i);
            clip.action.OnEnter(ctx);

            float clipEnd = clip.startTime + clip.duration;
            if (clip.duration > 0f && clipEnd > newTime)
            {
                // Clip is still active at newTime
                activeClipIndices.Add(i);
            }
            else
            {
                // Clip has completed by newTime (or is duration=0)
                clip.action.OnExit(ctx);
            }

            nextClipIndex = i + 1;
        }

        CurrentTime = newTime;
        lastEnterCheckTime = newTime; // already checked up to newTime in the loop above
        IsPlaying = false;
        IsPaused = false;
        IsCompleted = false;
    }
    #endregion

    #region Tick (called by TimelineManager)
    /// <summary>
    /// Advance the timeline by deltaTime. Called by TimelineManager.Update.
    /// </summary>
    public void Tick(float dt)
    {
        if (!IsPlaying || IsPaused || IsCompleted) return;
        if (clips.Count == 0)
        {
            Complete();
            return;
        }

        float prevTime = CurrentTime;
        CurrentTime += dt;

        bool reachedEnd = CurrentTime >= Duration;
        if (reachedEnd) CurrentTime = Duration;

        // 1. Enter new clips whose startTime is in (prevTime, CurrentTime]
        EnterNewClips(prevTime);

        // 2. Update active clips with current elapsed time
        UpdateActiveClips();

        // 3. Exit active clips whose end time has passed
        ExitCompletedClips();

        if (reachedEnd)
        {
            Complete();
        }
    }
    #endregion

    #region Internal Tick Helpers
    private void EnterNewClips(float prevTime)
    {
        bool isFirstTick = lastEnterCheckTime < 0f;
        float checkFrom = isFirstTick ? prevTime : lastEnterCheckTime;

        for (int i = nextClipIndex; i < clips.Count; i++)
        {
            TimelineClip clip = clips[i];
            if (clip.startTime > CurrentTime) break; // not yet

            // Only enter if startTime was NOT reached in a previous tick.
            // On first tick (lastEnterCheckTime == -1), enter all clips at or before CurrentTime.
            if (!isFirstTick && clip.startTime <= checkFrom) continue;

            TimelineContext ctx = MakeContext(i);
            clip.action.OnEnter(ctx);

            if (clip.duration > 0f)
            {
                activeClipIndices.Add(i);
            }
            else
            {
                clip.action.OnExit(ctx);
            }

            nextClipIndex = i + 1;
        }

        lastEnterCheckTime = CurrentTime;
    }

    private void UpdateActiveClips()
    {
        for (int i = activeClipIndices.Count - 1; i >= 0; i--)
        {
            int idx = activeClipIndices[i];
            TimelineClip clip = clips[idx];
            float elapsed = CurrentTime - clip.startTime;
            clip.action.OnUpdate(MakeContext(idx), elapsed);
        }
    }

    private void ExitCompletedClips()
    {
        for (int i = activeClipIndices.Count - 1; i >= 0; i--)
        {
            int idx = activeClipIndices[i];
            TimelineClip clip = clips[idx];
            if (clip.startTime + clip.duration <= CurrentTime)
            {
                clip.action.OnExit(MakeContext(idx));
                activeClipIndices.RemoveAt(i);
            }
        }
    }

    private void ExitAllActiveClips()
    {
        for (int i = activeClipIndices.Count - 1; i >= 0; i--)
        {
            int idx = activeClipIndices[i];
            clips[idx].action.OnExit(MakeContext(idx));
        }
        activeClipIndices.Clear();
    }

    private void Complete()
    {
        ExitAllActiveClips();

        if (loop)
        {
            // Reset state for next loop
            CurrentTime = 0f;
            nextClipIndex = 0;
            lastEnterCheckTime = -1f;
            activeClipIndices.Clear();
            // IsPlaying stays true, IsCompleted stays false
            Log.d($"TimelinePlayer [{PlayerId}] looping", "TimelinePlayer");
            return;
        }

        IsPlaying = false;
        IsPaused = false;
        IsCompleted = true;
        FireCompleted();
    }

    private void FireCompleted()
    {
        if (OnCompleted != null)
        {
            var handler = OnCompleted;
            OnCompleted = null; // prevent double-fire
            handler.Invoke(PlayerId);
        }
    }

    private TimelineContext MakeContext(int clipIndex)
    {
        return new TimelineContext
        {
            playerId = PlayerId,
            clipIndex = clipIndex,
            clipStartTime = clips[clipIndex].startTime,
            clipDuration = clips[clipIndex].duration,
            userData = null,
        };
    }
    #endregion
}
