using System;
using System.Collections.Generic;

/// <summary>
/// Proxy for managing timeline lifecycle within the PureMVC framework.
/// Wraps TimelineManager and provides notification-based control.
///
/// Usage:
///   Facade.RegisterProxy(new TimelineProxy());
///   var proxy = Facade.RetrieveProxy(TimelineProxy.NAME) as TimelineProxy;
///   string playerId = proxy.Play("tutorial_01");
///
/// Notifications sent:
///   TIMELINE_STARTED   — body=TimelineStartedBody { timelineId, duration }
///   TIMELINE_COMPLETED — body=TimelineCompletedBody { timelineId }
///   TIMELINE_STOPPED   — body=TimelineStoppedBody { timelineId }
///   TIMELINE_EVENT     — body=TimelineEventBody { timelineId, clipIndex, actionType }
/// </summary>
public class TimelineProxy : ProxyBase
{
    public new const string NAME = "TimelineProxy";

    private Dictionary<string, TimelineAsset> registry = new Dictionary<string, TimelineAsset>();
    private Dictionary<string, string> timelineIdToPlayerId = new Dictionary<string, string>(); // registered ID → player ID
    private Dictionary<string, string> playerIdToTimelineId = new Dictionary<string, string>(); // player ID → registered ID

    public TimelineProxy() : base(NAME)
    {
    }

    #region Registration
    /// <summary>Register a timeline asset by ID. Overwrites existing.</summary>
    public void RegisterTimeline(string id, TimelineAsset asset)
    {
        if (string.IsNullOrEmpty(id))
        {
            Log.w("RegisterTimeline: id is null or empty", "TimelineProxy");
            return;
        }
        if (asset == null)
        {
            Log.w($"RegisterTimeline: asset is null for id [{id}]", "TimelineProxy");
            return;
        }
        registry[id] = asset;
        Log.d($"TimelineProxy: registered [{id}] with {asset.clips.Count} clips", "TimelineProxy");
    }

    /// <summary>Register a timeline from JSON string.</summary>
    public void RegisterTimelineFromJson(string id, string json)
    {
        var asset = TimelineAsset.FromJson(json);
        if (asset == null)
        {
            Log.w($"RegisterTimelineFromJson: failed to parse JSON for id [{id}]", "TimelineProxy");
            return;
        }
        asset.id = id;
        RegisterTimeline(id, asset);
    }

    /// <summary>Check if a timeline is registered.</summary>
    public bool HasTimeline(string id)
    {
        return registry.ContainsKey(id);
    }

    /// <summary>Get a registered timeline asset.</summary>
    public TimelineAsset GetTimelineAsset(string id)
    {
        registry.TryGetValue(id, out var asset);
        return asset;
    }

    /// <summary>Remove a registered timeline. Does not stop running players.</summary>
    public void UnregisterTimeline(string id)
    {
        registry.Remove(id);
    }
    #endregion

    #region Playback
    /// <summary>
    /// Play a registered timeline by ID.
    /// Returns the playerId for manual control, or null if not found.
    /// </summary>
    public string Play(string id, Action<string> onComplete = null)
    {
        if (!registry.TryGetValue(id, out var asset))
        {
            Log.w($"Play: timeline [{id}] not registered", "TimelineProxy");
            return null;
        }

        var player = TimelineManager.Instance.CreateTimeline(asset, onComplete, id);
        if (player == null)
        {
            Log.w($"Play: failed to create player for [{id}]", "TimelineProxy");
            return null;
        }

        string actualPlayerId = player.PlayerId;
        timelineIdToPlayerId[id] = actualPlayerId;
        playerIdToTimelineId[actualPlayerId] = id;
        player.OnCompleted += OnPlayerCompletedInternal;
        player.Play();

        SendNotification(NotificationConst.TIMELINE_STARTED, new TimelineStartedBody
        {
            timelineId = id,
            duration = player.Duration,
        });

        Log.d($"TimelineProxy: started [{id}] playerId={actualPlayerId}, duration={player.Duration:F2}s", "TimelineProxy");
        return actualPlayerId;
    }

    /// <summary>Stop a running timeline by registered ID.</summary>
    public void Stop(string id)
    {
        if (!timelineIdToPlayerId.TryGetValue(id, out var playerId))
        {
            return;
        }

        var player = TimelineManager.Instance.GetPlayer(playerId);
        if (player != null && !player.IsCompleted)
        {
            player.Stop();
            SendNotification(NotificationConst.TIMELINE_STOPPED, new TimelineStoppedBody { timelineId = id });
        }
    }

    /// <summary>Pause a running timeline.</summary>
    public void Pause(string id)
    {
        TimelineManager.Instance.Pause(id);
    }

    /// <summary>Resume a paused timeline.</summary>
    public void Resume(string id)
    {
        TimelineManager.Instance.Resume(id);
    }

    /// <summary>Check if a timeline is currently playing.</summary>
    public bool IsPlaying(string id)
    {
        return TimelineManager.Instance.IsPlaying(id);
    }

    /// <summary>Get statistics string for debugging.</summary>
    public string GetStatistics()
    {
        return TimelineManager.Instance.GetStatistics();
    }
    #endregion

    #region Internal
    private void OnPlayerCompletedInternal(string playerId)
    {
        if (playerIdToTimelineId.TryGetValue(playerId, out var timelineId))
        {
            timelineIdToPlayerId.Remove(timelineId);
            playerIdToTimelineId.Remove(playerId);
            SendNotification(NotificationConst.TIMELINE_COMPLETED, new TimelineCompletedBody { timelineId = timelineId });
            Log.d($"TimelineProxy: completed [{timelineId}]", "TimelineProxy");
        }
    }
    #endregion
}
