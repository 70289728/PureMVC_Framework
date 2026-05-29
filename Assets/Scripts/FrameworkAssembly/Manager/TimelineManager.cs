using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for creating, playing, and controlling timeline players.
/// Lazy singleton MonoBehaviour (auto-creates GameObject on first access).
/// Drives all active TimelinePlayer instances via its own Update loop.
/// </summary>
public class TimelineManager : MonoBehaviour
{
    #region Singleton
    private static TimelineManager instance;
    public static TimelineManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("TimelineManager");
                instance = go.AddComponent<TimelineManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Member Variables
    private Dictionary<string, TimelinePlayer> players = new Dictionary<string, TimelinePlayer>();
    private List<string> pendingRemoval = new List<string>(); // avoid modifying dictionary during iteration
    private bool isUpdating = false;
    #endregion

    #region Public API — Create
    /// <summary>
    /// Create a new timeline player and register it.
    /// Returns the player for further configuration before calling Play().
    /// </summary>
    /// <param name="clips">Timeline clips sorted by startTime (auto-sorted internally).</param>
    /// <param name="timeMode">Scaled (Time.deltaTime) or Unscaled.</param>
    /// <param name="onComplete">Callback when timeline finishes or is stopped. Receives playerId.</param>
    /// <param name="playerId">Optional custom ID. Auto-generated if null.</param>
    public TimelinePlayer CreateTimeline(
        List<TimelineClip> clips,
        TimelineTimeMode timeMode = TimelineTimeMode.Scaled,
        bool loop = false,
        Action<string> onComplete = null,
        string playerId = null)
    {
        if (clips == null || clips.Count == 0)
        {
            Log.w("CreateTimeline: clips list is null or empty", "TimelineManager");
            return null;
        }

        string id = playerId ?? Guid.NewGuid().ToString();
        if (players.ContainsKey(id))
        {
            Log.w($"CreateTimeline: playerId [{id}] already exists, generating new id", "TimelineManager");
            id = Guid.NewGuid().ToString();
        }

        TimelinePlayer player = new TimelinePlayer(id, clips, timeMode, loop);
        if (onComplete != null)
        {
            player.OnCompleted += onComplete;
        }
        // Auto-remove from manager on completion (respects autoDestroy later)
        player.OnCompleted += OnPlayerCompleted;

        players[id] = player;
        Log.d($"TimelinePlayer created: [{id}], {clips.Count} clips, duration={player.Duration:F2}s, mode={timeMode}", "TimelineManager");
        return player;
    }

    /// <summary>
    /// Create and immediately play a timeline. Convenience method.
    /// </summary>
    public TimelinePlayer PlayTimeline(
        List<TimelineClip> clips,
        TimelineTimeMode timeMode = TimelineTimeMode.Scaled,
        bool loop = false,
        Action<string> onComplete = null,
        string playerId = null)
    {
        TimelinePlayer player = CreateTimeline(clips, timeMode, loop, onComplete, playerId);
        if (player != null)
        {
            player.Play();
        }
        return player;
    }

    /// <summary>
    /// Create a timeline from a deserialized TimelineAsset.
    /// Clips are created via TimelineActionFactory from actionType/actionData.
    /// </summary>
    public TimelinePlayer CreateTimeline(
        TimelineAsset asset,
        Action<string> onComplete = null,
        string playerId = null)
    {
        if (asset == null || asset.clips == null || asset.clips.Count == 0)
        {
            Log.w("CreateTimeline: asset is null or has no clips", "TimelineManager");
            return null;
        }

        TimelineTimeMode mode = TimelineTimeMode.Scaled;
        if (asset.timeMode != null && asset.timeMode.Equals("Unscaled", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = TimelineTimeMode.Unscaled;
        }

        var clips = new List<TimelineClip>();
        for (int i = 0; i < asset.clips.Count; i++)
        {
            var cd = asset.clips[i];
            ITimelineAction action = TimelineActionFactory.Create(cd.actionType, cd.actionData);
            if (action == null)
            {
                Log.w($"CreateTimeline: clip[{i}] actionType [{cd.actionType}] failed to create, skipping", "TimelineManager");
                continue;
            }
            clips.Add(new TimelineClip
            {
                startTime = cd.startTime,
                duration = cd.duration,
                action = action,
            });
        }

        if (clips.Count == 0)
        {
            Log.w("CreateTimeline: all clips failed to create from asset", "TimelineManager");
            return null;
        }

        return CreateTimeline(clips, mode, asset.loop, onComplete, playerId);
    }

    /// <summary>
    /// Create and immediately play a timeline from a TimelineAsset.
    /// </summary>
    public TimelinePlayer PlayTimeline(
        TimelineAsset asset,
        Action<string> onComplete = null,
        string playerId = null)
    {
        TimelinePlayer player = CreateTimeline(asset, onComplete, playerId);
        if (player != null)
        {
            player.Play();
        }
        return player;
    }
    #endregion

    #region Public API — Control
    /// <summary>Start playing a created timeline.</summary>
    public void Play(string playerId)
    {
        TimelinePlayer player = GetPlayer(playerId);
        if (player != null) player.Play();
    }

    /// <summary>Stop a timeline. Fires OnCompleted after exiting all active clips.</summary>
    public void Stop(string playerId)
    {
        TimelinePlayer player = GetPlayer(playerId);
        if (player != null)
        {
            player.Stop();
            // Stop triggers OnCompleted → which triggers OnPlayerCompleted → removal
        }
    }

    /// <summary>Stop all running timelines immediately.</summary>
    public void StopAll()
    {
        var snapshot = new List<TimelinePlayer>(players.Values);
        foreach (var p in snapshot)
        {
            p.Stop();
        }
        // All stopped players will be removed in OnPlayerCompleted (batched after Update loop)
        if (!isUpdating)
        {
            players.Clear();
        }
        Log.d($"StopAll: {snapshot.Count} timelines stopped", "TimelineManager");
    }

    /// <summary>Pause a timeline. Resume with Play().</summary>
    public void Pause(string playerId)
    {
        GetPlayer(playerId)?.Pause();
    }

    /// <summary>Resume a paused timeline.</summary>
    public void Resume(string playerId)
    {
        GetPlayer(playerId)?.Resume();
    }

    /// <summary>Seek a timeline to a specific time. Stops playback; call Play() after.</summary>
    public void Seek(string playerId, float time)
    {
        GetPlayer(playerId)?.Seek(time);
    }

    /// <summary>Get a player by ID. Returns null if not found.</summary>
    public TimelinePlayer GetPlayer(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return null;
        players.TryGetValue(playerId, out TimelinePlayer player);
        return player;
    }

    /// <summary>Check if a timeline is playing (not paused, not completed).</summary>
    public bool IsPlaying(string playerId)
    {
        TimelinePlayer player = GetPlayer(playerId);
        return player != null && player.IsPlaying && !player.IsPaused && !player.IsCompleted;
    }

    /// <summary>Get count of registered players (including paused/completed).</summary>
    public int PlayerCount => players.Count;

    /// <summary>Get statistics string for debugging.</summary>
    public string GetStatistics()
    {
        int playing = 0, paused = 0;
        foreach (var p in players.Values)
        {
            if (p.IsCompleted) continue;
            if (p.IsPaused) paused++;
            else if (p.IsPlaying) playing++;
        }
        return $"TimelineManager: {players.Count} total, {playing} playing, {paused} paused";
    }
    #endregion

    #region Unity Lifecycle
    void Update()
    {
        isUpdating = true;

        foreach (var kvp in players)
        {
            TimelinePlayer player = kvp.Value;
            if (!player.IsPlaying || player.IsPaused || player.IsCompleted) continue;

            float dt = player.TimeMode == TimelineTimeMode.Unscaled
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            player.Tick(dt);
        }

        isUpdating = false;

        // Remove completed/stopped players
        if (pendingRemoval.Count > 0)
        {
            foreach (var id in pendingRemoval)
            {
                players.Remove(id);
            }
            pendingRemoval.Clear();
        }
    }

    void OnDestroy()
    {
        StopAll();
    }
    #endregion

    #region Callbacks
    private void OnPlayerCompleted(string playerId)
    {
        Log.d($"TimelinePlayer completed: [{playerId}]", "TimelineManager");

        // Respect AutoDestroy: only auto-remove if player wants it
        if (players.TryGetValue(playerId, out var player) && !player.AutoDestroy)
        {
            Log.d($"TimelinePlayer [{playerId}] AutoDestroy=false, keeping in manager", "TimelineManager");
            return;
        }

        if (isUpdating)
        {
            if (!pendingRemoval.Contains(playerId))
                pendingRemoval.Add(playerId);
        }
        else
        {
            players.Remove(playerId);
        }
    }
    #endregion
}
