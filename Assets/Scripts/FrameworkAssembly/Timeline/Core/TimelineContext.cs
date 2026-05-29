/// <summary>
/// Context passed to ITimelineAction methods.
/// Provides info about the current clip and its parent player.
/// </summary>
public struct TimelineContext
{
    /// <summary>ID of the TimelinePlayer that owns this clip.</summary>
    public string playerId;

    /// <summary>Zero-based index of this clip in the timeline's clip list.</summary>
    public int clipIndex;

    /// <summary>Absolute start time of this clip on the timeline (seconds).</summary>
    public float clipStartTime;

    /// <summary>Duration of this clip (seconds). 0 = instant.</summary>
    public float clipDuration;

    /// <summary>User-defined data attached to this clip.</summary>
    public object userData;
}
