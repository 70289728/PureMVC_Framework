using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using PureMVC.Patterns.Facade;

/// <summary>
/// Bridge between Unity Timeline (PlayableDirector) and the PureMVC notification system.
/// Attach this to a GameObject that already has a PlayableDirector.
///
/// When the PlayableDirector plays, this component listens for TimelineNotificationMarker
/// instances on the timeline and fires PureMVC notifications as they are reached.
///
/// The PlayableDirector is also wrapped as a TimelinePlayer-like control surface:
///   public methods: PlayTimeline(), StopTimeline(), PauseTimeline(), ResumeTimeline()
///   auto-sends TIMELINE_STARTED / TIMELINE_COMPLETED / TIMELINE_STOPPED notifications.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class TimelineBridge : MonoBehaviour
{
    #region Inspector Fields
    [Tooltip("Identifier for this timeline. Used as timelineId in notifications. Defaults to GameObject name.")]
    public string timelineId;

    [Tooltip("If true, auto-destroy this GameObject when the timeline completes.")]
    public bool autoDestroyOnComplete = false;
    #endregion

    #region Component References
    private PlayableDirector director;
    private double previousTime = -1.0;
    private List<MarkerEntry> markers = new List<MarkerEntry>();

    private struct MarkerEntry
    {
        public double time;
        public string notificationName;
        public string body;
        public bool fired;
    }
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        director = GetComponent<PlayableDirector>();
        if (director == null)
        {
            Log.e("[TimelineBridge] No PlayableDirector found on this GameObject", "TimelineBridge");
            enabled = false;
            return;
        }

        if (string.IsNullOrEmpty(timelineId))
            timelineId = gameObject.name;

        CollectMarkers();
    }

    void OnEnable()
    {
        if (director != null)
        {
            director.played += OnTimelinePlayed;
            director.stopped += OnTimelineStopped;
        }
    }

    void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnTimelinePlayed;
            director.stopped -= OnTimelineStopped;
        }
    }

    void Update()
    {
        if (director == null || director.state != PlayState.Playing) return;

        double currentTime = director.time;
        CheckMarkers(previousTime, currentTime);
        previousTime = currentTime;
    }
    #endregion

    #region Public API — Playback Control
    /// <summary>Start or restart the timeline. Sends TIMELINE_STARTED.</summary>
    public void PlayTimeline()
    {
        if (director == null) return;
        director.Play();
    }

    /// <summary>Stop the timeline. Sends TIMELINE_STOPPED.</summary>
    public void StopTimeline()
    {
        if (director == null) return;
        director.Stop();
    }

    /// <summary>Pause the timeline.</summary>
    public void PauseTimeline()
    {
        if (director == null) return;
        director.Pause();
    }

    /// <summary>Resume the paused timeline.</summary>
    public void ResumeTimeline()
    {
        if (director == null) return;
        director.Resume();
    }

    /// <summary>Seek to a specific time.</summary>
    public void Seek(float time)
    {
        if (director == null) return;
        director.time = time;
        director.Evaluate();
    }

    /// <summary>Current timeline duration (from the TimelineAsset).</summary>
    public double Duration => director != null ? director.duration : 0.0;

    /// <summary>Whether the timeline is currently playing.</summary>
    public bool IsPlaying => director != null && director.state == PlayState.Playing;
    #endregion

    #region Markers
    private void CollectMarkers()
    {
        markers.Clear();
        var unityTimelineAsset = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
        if (unityTimelineAsset == null) return;

        foreach (var track in unityTimelineAsset.GetOutputTracks())
        {
            foreach (var marker in track.GetMarkers())
            {
                var notifyMarker = marker as TimelineNotificationMarker;
                if (notifyMarker != null)
                {
                    markers.Add(new MarkerEntry
                    {
                        time = marker.time,
                        notificationName = notifyMarker.notificationName,
                        body = notifyMarker.body,
                        fired = false,
                    });
                }
            }
        }

        Log.d($"TimelineBridge [{timelineId}]: collected {markers.Count} notification markers", "TimelineBridge");
    }

    private void CheckMarkers(double prevTime, double currentTime)
    {
        for (int i = 0; i < markers.Count; i++)
        {
            var entry = markers[i];
            if (entry.fired) continue;

            // Fire if marker time is within (prevTime, currentTime]
            // On wrap / seek-forward: fire all markers between prevTime and currentTime
            if (entry.time > prevTime && entry.time <= currentTime)
            {
                entry.fired = true;
                markers[i] = entry;
                FireMarker(entry);
            }
        }
    }

    private void FireMarker(MarkerEntry entry)
    {
        var body = new TimelineEventBody
        {
            timelineId = timelineId,
            clipIndex = -1,
            actionType = "UnityMarker",
        };

        Facade.Instance.SendNotification(entry.notificationName, string.IsNullOrEmpty(entry.body) ? body : entry.body);
        Log.d($"TimelineBridge [{timelineId}]: fired marker [{entry.notificationName}] at t={entry.time:F2}", "TimelineBridge");
    }

    private void ResetMarkers()
    {
        for (int i = 0; i < markers.Count; i++)
        {
            var entry = markers[i];
            entry.fired = false;
            markers[i] = entry;
        }
    }
    #endregion

    #region Callbacks
    private void OnTimelinePlayed(PlayableDirector pd)
    {
        previousTime = -1.0;
        ResetMarkers();

        Facade.Instance.SendNotification(NotificationConst.TIMELINE_STARTED, new TimelineStartedBody
        {
            timelineId = timelineId,
            duration = (float)director.duration,
        });

        Log.d($"TimelineBridge [{timelineId}]: started, duration={director.duration:F2}s", "TimelineBridge");
    }

    private void OnTimelineStopped(PlayableDirector pd)
    {
        // Only send if we were actually playing (not idle stop)
        if (previousTime >= 0)
        {
            // Check if we reached the end
            if (previousTime >= director.duration - 0.001)
            {
                Facade.Instance.SendNotification(NotificationConst.TIMELINE_COMPLETED, new TimelineCompletedBody { timelineId = timelineId });
                Log.d($"TimelineBridge [{timelineId}]: completed", "TimelineBridge");
            }
            else
            {
                Facade.Instance.SendNotification(NotificationConst.TIMELINE_STOPPED, new TimelineStoppedBody { timelineId = timelineId });
                Log.d($"TimelineBridge [{timelineId}]: stopped", "TimelineBridge");
            }
        }

        previousTime = -1.0;

        if (autoDestroyOnComplete)
        {
            Destroy(gameObject);
        }
    }
    #endregion
}
