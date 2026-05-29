using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Factory for deserializing ITimelineAction from actionType + actionData strings.
/// Register custom action types before loading assets.
/// </summary>
public static class TimelineActionFactory
{
    private static Dictionary<string, Func<string, ITimelineAction>> creators
        = new Dictionary<string, Func<string, ITimelineAction>>();

    /// <summary>Register a built-in or custom action type.</summary>
    public static void Register(string actionType, Func<string, ITimelineAction> creator)
    {
        if (creators.ContainsKey(actionType))
        {
            Log.w($"TimelineActionFactory: actionType [{actionType}] already registered, overwriting", "TimelineActionFactory");
        }
        creators[actionType] = creator;
    }

    /// <summary>Create an ITimelineAction from type string and JSON data.</summary>
    public static ITimelineAction Create(string actionType, string actionData)
    {
        if (string.IsNullOrEmpty(actionType))
        {
            Log.w("TimelineActionFactory.Create: actionType is null or empty", "TimelineActionFactory");
            return null;
        }

        if (creators.TryGetValue(actionType, out var creator))
        {
            try
            {
                return creator(actionData);
            }
            catch (Exception ex)
            {
                Log.e($"TimelineActionFactory.Create: failed to create [{actionType}]: {ex.Message}", "TimelineActionFactory");
                return null;
            }
        }

        Log.w($"TimelineActionFactory.Create: unknown actionType [{actionType}]", "TimelineActionFactory");
        return null;
    }

    /// <summary>Clear all registered action types.</summary>
    public static void Clear()
    {
        creators.Clear();
    }

    #region Static Constructor — register built-in actions
    static TimelineActionFactory()
    {
        Register("Delay", data => new DelayAction());
        Register("Notification", data =>
        {
            var p = JsonConvert.DeserializeObject<NotificationActionParams>(data ?? "{}");
            return new NotificationAction(p.notificationName, p.body);
        });
        Register("Log", data =>
        {
            var p = JsonConvert.DeserializeObject<LogActionParams>(data ?? "{}");
            return new LogAction(p.message);
        });
        Register("LuaHook", data =>
        {
            var p = JsonConvert.DeserializeObject<LuaHookActionParams>(data ?? "{}");
            return new LuaHookAction(p.hookCategory, p.typeName, p.hookName);
        });
        Register("Parallel", data =>
        {
            var p = JsonConvert.DeserializeObject<ParallelActionParams>(data ?? "{}");
            var subClips = new List<TimelineClip>();
            if (p.subClips != null)
            {
                foreach (var cd in p.subClips)
                {
                    var action = Create(cd.actionType, cd.actionData);
                    if (action != null)
                    {
                        subClips.Add(new TimelineClip
                        {
                            startTime = cd.startTime,
                            duration = cd.duration,
                            action = action,
                        });
                    }
                }
            }
            return new ParallelAction(subClips);
        });
    }
    #endregion

    /// <summary>
    /// Convenience: register Video action with a player factory.
    /// Each timeline clip that uses Video action gets its own IVideoPlayer instance
    /// to avoid shared-state conflicts when multiple clips play simultaneously.
    ///
    /// Example:
    ///   TimelineActionFactory.RegisterVideo(() => new AVProVideoPlayerImpl());
    /// </summary>
    public static void RegisterVideo(Func<IVideoPlayer> playerFactory)
    {
        Register("Video", data =>
        {
            var p = JsonConvert.DeserializeObject<VideoActionParams>(data ?? "{}");
            var player = playerFactory();
            return new VideoAction(player, p.videoPath, ownsPlayer: true);
        });
    }
}

// --- Action param classes for JSON deserialization ---

[Serializable]
internal class NotificationActionParams
{
    public string notificationName;
    public object body;
}

[Serializable]
internal class LogActionParams
{
    public string message;
}

[Serializable]
internal class LuaHookActionParams
{
    public string hookCategory;
    public string typeName;
    public string hookName;
}

[Serializable]
internal class ParallelActionParams
{
    public List<TimelineClipData> subClips;
}

[Serializable]
internal class VideoActionParams
{
    public string videoPath;
}
