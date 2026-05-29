/// <summary>
/// Outputs a log message via Log.d when the clip starts.
/// </summary>
public class LogAction : ITimelineAction
{
    private string message;
    private string tag;

    /// <summary>
    /// Create a log action.
    /// </summary>
    /// <param name="message">Message to log.</param>
    /// <param name="tag">Optional tag for Log.d. Defaults to "LogAction".</param>
    public LogAction(string message, string tag = null)
    {
        this.message = message ?? string.Empty;
        this.tag = tag ?? "LogAction";
    }

    public void OnEnter(TimelineContext ctx)
    {
        Log.d($"[t={ctx.clipStartTime:F2}] {message}", tag);
    }

    public void OnUpdate(TimelineContext ctx, float elapsed) { }
    public void OnExit(TimelineContext ctx) { }
}
