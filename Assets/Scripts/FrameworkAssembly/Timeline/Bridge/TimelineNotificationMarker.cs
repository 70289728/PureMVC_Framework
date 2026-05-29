using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Custom Timeline marker that fires a PureMVC notification when passed during playback.
/// Drop this on any Timeline track in the Unity Timeline Editor.
///
/// Works together with TimelineBridge which listens for these markers.
/// </summary>
public class TimelineNotificationMarker : Marker, INotification
{
    [Tooltip("PureMVC notification name to send. Use NotificationConst values.")]
    public string notificationName;

    [Tooltip("Optional body string. If empty, a TimelineEventBody is sent.")]
    public string body;

    #region INotification
    public PropertyName id => new PropertyName($"TimelineNotification_{notificationName}_{time:F2}");
    #endregion
}
