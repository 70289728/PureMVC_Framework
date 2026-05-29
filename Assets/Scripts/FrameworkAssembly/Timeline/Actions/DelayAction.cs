/// <summary>
/// A no-op action that simply waits for its duration to pass.
/// All logic is handled by the TimelinePlayer's timing — this action's methods are empty.
/// Use clip.duration to control how long to wait.
/// </summary>
public class DelayAction : ITimelineAction
{
    public void OnEnter(TimelineContext ctx) { }
    public void OnUpdate(TimelineContext ctx, float elapsed) { }
    public void OnExit(TimelineContext ctx) { }
}
