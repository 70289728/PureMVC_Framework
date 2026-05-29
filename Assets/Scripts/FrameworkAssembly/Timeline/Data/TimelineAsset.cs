using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Serializable timeline asset. Contains an ordered list of clips.
/// Can be serialized to/from JSON for hot-update delivery or Lua reading.
/// </summary>
[Serializable]
public class TimelineAsset
{
    /// <summary>Unique identifier for this timeline.</summary>
    public string id;

    /// <summary>Ordered list of timeline clips.</summary>
    public List<TimelineClipData> clips;

    /// <summary>If true, loop back to start on completion.</summary>
    public bool loop;

    /// <summary>
    /// If true, auto-destroy the player when this timeline completes.
    /// Default true. Set false if you plan to replay the player.
    /// </summary>
    public bool autoDestroy;

    /// <summary>Time mode override. If null, uses player default (Scaled).</summary>
    public string timeMode; // "Scaled" or "Unscaled"

    public TimelineAsset()
    {
        clips = new List<TimelineClipData>();
        autoDestroy = true;
    }

    /// <summary>Deserialize from JSON string.</summary>
    public static TimelineAsset FromJson(string json)
    {
        return JsonConvert.DeserializeObject<TimelineAsset>(json);
    }

    /// <summary>Serialize to JSON string.</summary>
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}

/// <summary>
/// Serializable clip data for a timeline asset.
/// Contains timing info and action type/data for deserialization.
/// </summary>
[Serializable]
public class TimelineClipData
{
    /// <summary>Absolute start time on the timeline (seconds).</summary>
    public float startTime;

    /// <summary>Duration of this clip (seconds). 0 = instant.</summary>
    public float duration;

    /// <summary>Action type identifier (e.g. "Delay", "Notification", "Log", "LuaHook", "Parallel").</summary>
    public string actionType;

    /// <summary>Action parameters as a JSON string.</summary>
    public string actionData;
}
