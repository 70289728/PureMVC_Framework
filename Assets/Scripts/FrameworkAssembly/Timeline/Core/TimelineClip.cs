/// <summary>
/// A single clip on a timeline. Contains timing info and the action to execute.
/// </summary>
public class TimelineClip
{
    /// <summary>Absolute start time on the timeline (seconds).</summary>
    public float startTime;

    /// <summary>Duration of this clip (seconds). 0 = instant event (OnEnter+OnExit in same frame).</summary>
    public float duration;

    /// <summary>The action to execute for this clip.</summary>
    public ITimelineAction action;
}
