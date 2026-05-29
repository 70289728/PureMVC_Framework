/// <summary>
/// Body sent with TIMELINE_STARTED notification.
/// </summary>
public class TimelineStartedBody
{
    public string timelineId;
    public float duration;
}

/// <summary>
/// Body sent with TIMELINE_COMPLETED notification.
/// </summary>
public class TimelineCompletedBody
{
    public string timelineId;
}

/// <summary>
/// Body sent with TIMELINE_STOPPED notification.
/// </summary>
public class TimelineStoppedBody
{
    public string timelineId;
}

/// <summary>
/// Body sent with TIMELINE_EVENT notification.
/// </summary>
public class TimelineEventBody
{
    public string timelineId;
    public int clipIndex;
    public string actionType;
}
