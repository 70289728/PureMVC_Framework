/// <summary>
/// Interface for a timeline clip action.
/// Implement this to create custom behaviors executed at specific points on a timeline.
/// </summary>
public interface ITimelineAction
{
    /// <summary>Called when the clip's startTime is reached.</summary>
    void OnEnter(TimelineContext ctx);

    /// <summary>
    /// Called each frame while the clip is active.
    /// elapsed = time passed since clip startTime (0 to duration).
    /// Not called for duration=0 clips.
    /// </summary>
    void OnUpdate(TimelineContext ctx, float elapsed);

    /// <summary>
    /// Called when the clip's duration expires, or when the timeline is stopped.
    /// Always called after OnEnter, even for duration=0 clips (same frame).
    /// </summary>
    void OnExit(TimelineContext ctx);
}
