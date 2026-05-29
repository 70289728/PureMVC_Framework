using PureMVC.Patterns.Facade;

/// <summary>
/// Sends a PureMVC notification when the clip starts.
/// The notification is sent via Facade.SendNotification().
/// </summary>
public class NotificationAction : ITimelineAction
{
    private string notificationName;
    private object body;

    /// <summary>
    /// Create a notification action.
    /// </summary>
    /// <param name="notificationName">PureMVC notification name. See NotificationConst.</param>
    /// <param name="body">Optional notification body.</param>
    public NotificationAction(string notificationName, object body = null)
    {
        this.notificationName = notificationName;
        this.body = body;
    }

    public void OnEnter(TimelineContext ctx)
    {
        if (string.IsNullOrEmpty(notificationName))
        {
            Log.w($"NotificationAction: notificationName is empty, clipIndex={ctx.clipIndex}", "NotificationAction");
            return;
        }

        Facade.Instance.SendNotification(notificationName, body);
        Log.d($"NotificationAction sent: [{notificationName}] at t={ctx.clipStartTime:F2}", "NotificationAction");
    }

    public void OnUpdate(TimelineContext ctx, float elapsed) { }
    public void OnExit(TimelineContext ctx) { }
}
