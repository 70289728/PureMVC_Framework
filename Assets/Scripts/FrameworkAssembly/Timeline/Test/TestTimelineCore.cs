using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-only test to verify TimelineManager and TimelinePlayer core behavior.
/// Run by calling TestTimelineCore.RunAll() from Unity's console or a menu item.
/// </summary>
#if UNITY_EDITOR
public static class TestTimelineCore
{
    public static void RunAll()
    {
        Test_BasicPlayComplete();
        Test_PauseResume();
        Test_Seek();
        Test_Stop();
        Test_InstantClips();
        Test_MultipleClips();
        Test_UnscaledTime();
        Test_BuiltInAction_Log();
        Test_BuiltInAction_Delay();
        Test_BuiltInAction_Parallel();
        Test_Asset_FromJson();
        Test_Asset_ToJson();
        Test_Proxy_RegisterAndPlay();
        Test_Proxy_NotificationBodies();
        Test_Bridge_MarkerCreation();
        Debug.Log("<color=#00FF00>[TestTimelineCore] All tests passed!</color>");
    }

    private class TestAction : ITimelineAction
    {
        public string Id;
        public int EnterCount;
        public int ExitCount;
        public int UpdateCount;
        public float LastElapsed;

        public TestAction(string id) { Id = id; }

        public void OnEnter(TimelineContext ctx)
        {
            EnterCount++;
            Debug.Log($"  [{Id}] OnEnter at t={ctx.clipStartTime:F2}");
        }

        public void OnUpdate(TimelineContext ctx, float elapsed)
        {
            UpdateCount++;
            LastElapsed = elapsed;
        }

        public void OnExit(TimelineContext ctx)
        {
            ExitCount++;
            Debug.Log($"  [{Id}] OnExit at t={ctx.clipStartTime + ctx.clipDuration:F2}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new System.Exception($"Assertion failed: {message}");
    }

    private static void Test_BasicPlayComplete()
    {
        Debug.Log("[Test] BasicPlayComplete");
        var action = new TestAction("act");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 1f, action = action }
        };

        string completedId = null;
        var player = TimelineManager.Instance.CreateTimeline(clips, onComplete: id => completedId = id);
        player.Play();

        // Tick 0.5s — clip should be active, OnUpdate called
        player.Tick(0.5f);
        Assert(action.EnterCount == 1, $"EnterCount={action.EnterCount}, expected 1");
        Assert(action.UpdateCount > 0, $"UpdateCount={action.UpdateCount}, expected >0");
        Assert(action.ExitCount == 0, $"ExitCount={action.ExitCount}, expected 0");
        Assert(player.IsPlaying && !player.IsCompleted, "Should be playing");

        // Tick another 0.5s — clip should complete
        player.Tick(0.5f);
        Assert(action.EnterCount == 1, $"EnterCount={action.EnterCount}, expected 1");
        Assert(action.ExitCount == 1, $"ExitCount={action.ExitCount}, expected 1");
        Assert(player.IsCompleted, "Should be completed");
        Assert(completedId == player.PlayerId, "OnCompleted should fire");

        Debug.Log("[Test] BasicPlayComplete — PASSED");
    }

    private static void Test_PauseResume()
    {
        Debug.Log("[Test] PauseResume");
        var action = new TestAction("act");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 2f, action = action }
        };

        var player = TimelineManager.Instance.CreateTimeline(clips);
        player.Play();

        player.Tick(0.5f);
        Assert(player.IsPlaying && !player.IsPaused, "Should be playing");

        player.Pause();
        Assert(player.IsPaused, "Should be paused");

        player.Tick(0.5f); // paused, should not advance
        Assert(player.CurrentTime == 0.5f, $"CurrentTime={player.CurrentTime}, expected 0.5");

        player.Resume();
        Assert(!player.IsPaused, "Should be resumed");

        player.Tick(1f);
        Assert(player.CurrentTime == 1.5f, $"CurrentTime={player.CurrentTime}, expected 1.5");

        player.Tick(1f);
        Assert(player.IsCompleted, "Should complete after full duration");

        Debug.Log("[Test] PauseResume — PASSED");
    }

    private static void Test_Seek()
    {
        Debug.Log("[Test] Seek");
        var action = new TestAction("act");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 3f, action = action }
        };

        var player = TimelineManager.Instance.CreateTimeline(clips);
        player.Play();
        player.Tick(1f);
        Assert(player.CurrentTime == 1f, $"CurrentTime={player.CurrentTime}");

        player.Seek(2f);
        Assert(player.CurrentTime == 2f, $"After seek CurrentTime={player.CurrentTime}");
        Assert(action.EnterCount == 1, "After seek: OnEnter should be called once");
        Assert(action.ExitCount == 0, "After seek within clip: OnExit should NOT be called");

        // Seek past end
        player.Seek(4f);
        Assert(action.ExitCount == 1, "After seek past end: OnExit should be called");

        Debug.Log("[Test] Seek — PASSED");
    }

    private static void Test_Stop()
    {
        Debug.Log("[Test] Stop");
        var action = new TestAction("act");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 5f, action = action }
        };

        bool completedFired = false;
        var player = TimelineManager.Instance.CreateTimeline(clips, onComplete: id => completedFired = true);
        player.Play();
        player.Tick(1f);
        Assert(action.EnterCount == 1, "After tick: OnEnter should be called");

        player.Stop();
        Assert(action.ExitCount == 1, "After stop: OnExit should be called");
        Assert(player.IsCompleted, "Should be completed after stop");
        Assert(completedFired, "OnCompleted should fire on stop");

        Debug.Log("[Test] Stop — PASSED");
    }

    private static void Test_InstantClips()
    {
        Debug.Log("[Test] InstantClips");
        var a1 = new TestAction("a1");
        var a2 = new TestAction("a2");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 0f, action = a1 },
            new TimelineClip { startTime = 0f, duration = 0f, action = a2 },
        };

        var player = TimelineManager.Instance.CreateTimeline(clips);
        player.Play();
        // Tick a small delta — both instant clips should be entered and exited
        player.Tick(0.001f);

        Assert(a1.EnterCount == 1 && a1.ExitCount == 1, $"a1: Enter={a1.EnterCount} Exit={a1.ExitCount}");
        Assert(a2.EnterCount == 1 && a2.ExitCount == 1, $"a2: Enter={a2.EnterCount} Exit={a2.ExitCount}");
        Assert(player.IsCompleted, "Should be completed after all instant clips");

        Debug.Log("[Test] InstantClips — PASSED");
    }

    private static void Test_MultipleClips()
    {
        Debug.Log("[Test] MultipleClips");
        var a1 = new TestAction("a1");
        var a2 = new TestAction("a2");
        var a3 = new TestAction("a3");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 1f, action = a1 },
            new TimelineClip { startTime = 1f, duration = 0f, action = a2 },  // instant at t=1s
            new TimelineClip { startTime = 1f, duration = 1f, action = a3 },  // overlaps with a2
        };

        var player = TimelineManager.Instance.CreateTimeline(clips);
        player.Play();

        // t=0 → a1 enters
        player.Tick(0.5f);
        Assert(a1.EnterCount == 1, "t=0.5: a1 should have entered");
        Assert(a1.ExitCount == 0, "t=0.5: a1 should still be active");
        Assert(a2.EnterCount == 0, "t=0.5: a2 should not have entered yet");
        Assert(a3.EnterCount == 0, "t=0.5: a3 should not have entered yet");

        // t=1s — a1 exits, a2 enters+exits (instant), a3 enters
        player.Tick(0.5f);
        Assert(a1.ExitCount == 1, "t=1: a1 should have exited");
        Assert(a2.EnterCount == 1 && a2.ExitCount == 1, "t=1: a2 should have entered and exited (instant)");
        Assert(a3.EnterCount == 1 && a3.ExitCount == 0, "t=1: a3 should have entered, not exited yet");

        // t=2s — a3 exits, timeline completes
        player.Tick(1f);
        Assert(a3.ExitCount == 1, "t=2: a3 should have exited");
        Assert(player.IsCompleted, "t=2: timeline should be completed");

        Debug.Log("[Test] MultipleClips — PASSED");
    }

    private static void Test_UnscaledTime()
    {
        Debug.Log("[Test] UnscaledTime");
        var action = new TestAction("act");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 1f, action = action }
        };

        var player = TimelineManager.Instance.CreateTimeline(clips, TimelineTimeMode.Unscaled);
        player.Play();

        // This test only validates that the mode is stored correctly.
        // Actual unscaled behavior depends on Time.unscaledDeltaTime which is
        // only meaningful in Play mode.
        Assert(player.TimeMode == TimelineTimeMode.Unscaled, "TimeMode should be Unscaled");

        player.Tick(0.5f);
        Assert(player.CurrentTime == 0.5f, $"CurrentTime={player.CurrentTime}");

        Debug.Log("[Test] UnscaledTime — PASSED");
    }

    #region Phase 2 Tests — Built-in Actions + JSON Serialization
    private static void Test_BuiltInAction_Log()
    {
        Debug.Log("[Test] BuiltInAction_Log");
        var logAction = new LogAction("test message", "TestTag");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 0f, action = logAction },
        };

        var player = TimelineManager.Instance.CreateTimeline(clips);
        player.Play();
        player.Tick(0.001f);

        Assert(player.IsCompleted, "Log action clip should complete");
        Debug.Log("[Test] BuiltInAction_Log — PASSED");
    }

    private static void Test_BuiltInAction_Delay()
    {
        Debug.Log("[Test] BuiltInAction_Delay");
        var action = new TestAction("marker");
        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 1f, action = new DelayAction() },
            new TimelineClip { startTime = 1f, duration = 0f, action = action },
        };

        var player = TimelineManager.Instance.CreateTimeline(clips);
        player.Play();

        // After delay, marker should not have fired yet
        player.Tick(0.5f);
        Assert(action.EnterCount == 0, "t=0.5: marker should NOT have fired yet");

        // After delay completes, marker fires
        player.Tick(0.6f);
        Assert(action.EnterCount == 1 && action.ExitCount == 1, "t=1.1: marker should fire and exit");
        Assert(player.IsCompleted, "Should be completed");

        Debug.Log("[Test] BuiltInAction_Delay — PASSED");
    }

    private static void Test_BuiltInAction_Parallel()
    {
        Debug.Log("[Test] BuiltInAction_Parallel");
        var subA = new TestAction("subA");
        var subB = new TestAction("subB");
        var subClips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 1f, action = subA },
            new TimelineClip { startTime = 0.5f, duration = 0f, action = subB },
        };
        var parallel = new ParallelAction(subClips);

        var clips = new List<TimelineClip>
        {
            new TimelineClip { startTime = 0f, duration = 1f, action = parallel },
        };

        var player = TimelineManager.Instance.CreateTimeline(clips);
        player.Play();

        // t=0.3: subA started, subB not yet
        player.Tick(0.3f);
        Assert(subA.EnterCount == 1 && subA.ExitCount == 0, "t=0.3: subA should be active");
        Assert(subB.EnterCount == 0, "t=0.3: subB should NOT have fired");

        // t=0.6: subB should have fired (instant at t=0.5)
        player.Tick(0.3f);
        Assert(subB.EnterCount == 1 && subB.ExitCount == 1, "t=0.6: subB should fire and exit");

        // t=1.1: subA completes, whole clip completes
        player.Tick(0.5f);
        Assert(subA.ExitCount == 1, "t=1.1: subA should exit");
        Assert(player.IsCompleted, "t=1.1: timeline should complete");

        Debug.Log("[Test] BuiltInAction_Parallel — PASSED");
    }

    private static void Test_Asset_FromJson()
    {
        Debug.Log("[Test] Asset_FromJson");
        string json = @"{
            ""id"": ""test_timeline"",
            ""loop"": false,
            ""autoDestroy"": true,
            ""timeMode"": ""Scaled"",
            ""clips"": [
                { ""startTime"": 0.0, ""duration"": 1.0, ""actionType"": ""Log"", ""actionData"": ""{\""message\"": \""hello\""}"" },
                { ""startTime"": 1.0, ""duration"": 0.0, ""actionType"": ""Notification"", ""actionData"": ""{\""notificationName\"": \""TEST_NOTIFY\"", \""body\"": null}"" }
            ]
        }";

        var asset = TimelineAsset.FromJson(json);
        Assert(asset != null, "Asset should not be null");
        Assert(asset.id == "test_timeline", $"id={asset.id}");
        Assert(asset.clips.Count == 2, $"clips.Count={asset.clips.Count}");
        Assert(asset.clips[0].actionType == "Log", $"clip[0].actionType={asset.clips[0].actionType}");
        Assert(asset.clips[1].actionType == "Notification", $"clip[1].actionType={asset.clips[1].actionType}");

        Debug.Log("[Test] Asset_FromJson — PASSED");
    }

    private static void Test_Asset_ToJson()
    {
        Debug.Log("[Test] Asset_ToJson");
        var asset = new TimelineAsset
        {
            id = "roundtrip_test",
            clips = new List<TimelineClipData>
            {
                new TimelineClipData { startTime = 0f, duration = 1f, actionType = "Delay", actionData = "{}" },
            },
        };

        string json = asset.ToJson();
        Assert(!string.IsNullOrEmpty(json), "JSON should not be empty");
        Assert(json.Contains("roundtrip_test"), "JSON should contain id");

        // Round-trip
        var restored = TimelineAsset.FromJson(json);
        Assert(restored.id == asset.id, $"Round-trip: id mismatch ({restored.id} vs {asset.id})");
        Assert(restored.clips.Count == 1, $"Round-trip: clips.Count={restored.clips.Count}");
        Assert(restored.clips[0].actionType == "Delay", $"Round-trip: actionType={restored.clips[0].actionType}");

    }
    #endregion

    #region Phase 3 Tests — PureMVC Integration
    private static void Test_Proxy_RegisterAndPlay()
    {
        Debug.Log("[Test] Proxy_RegisterAndPlay");

        var proxy = new TimelineProxy();

        var asset = new TimelineAsset { id = "test_proxy_play" };
        asset.clips.Add(new TimelineClipData
        {
            startTime = 0f,
            duration = 0f,
            actionType = "Log",
            actionData = "{\"message\":\"proxy test\"}",
        });

        proxy.RegisterTimeline("test_proxy_play", asset);
        Assert(proxy.HasTimeline("test_proxy_play"), "Should be registered");

        string playerId = proxy.Play("test_proxy_play");
        Assert(!string.IsNullOrEmpty(playerId), "Play should return playerId");

        // Tick to complete the instant clip
        var player = TimelineManager.Instance.GetPlayer(playerId);
        Assert(player != null, "Player should exist");
        player.Tick(0.001f);
        Assert(player.IsCompleted, "Should complete after instant clip");

        Debug.Log("[Test] Proxy_RegisterAndPlay — PASSED");
    }

    private static void Test_Proxy_NotificationBodies()
    {
        Debug.Log("[Test] Proxy_NotificationBodies");

        var body1 = new TimelineStartedBody { timelineId = "tl1", duration = 5.0f };
        Assert(body1.timelineId == "tl1", "StartedBody timelineId");
        Assert(body1.duration == 5.0f, "StartedBody duration");

        var body2 = new TimelineCompletedBody { timelineId = "tl1" };
        Assert(body2.timelineId == "tl1", "CompletedBody timelineId");

        var body3 = new TimelineStoppedBody { timelineId = "tl1" };
        Assert(body3.timelineId == "tl1", "StoppedBody timelineId");

        var body4 = new TimelineEventBody { timelineId = "tl1", clipIndex = 2, actionType = "Log" };
        Assert(body4.timelineId == "tl1", "EventBody timelineId");
        Assert(body4.clipIndex == 2, "EventBody clipIndex");
        Assert(body4.actionType == "Log", "EventBody actionType");

        Debug.Log("[Test] Proxy_NotificationBodies — PASSED");
    }
    #endregion

    #region Phase 4 Tests — Unity Timeline Bridge
    private static void Test_Bridge_MarkerCreation()
    {
        Debug.Log("[Test] Bridge_MarkerCreation");

        // Test that TimelineNotificationMarker can be instantiated and properties set.
        // We cannot test the full PlayableDirector loop without a real scene,
        // but we can verify the marker data is correct.
        var marker = new TimelineNotificationMarker();
        marker.notificationName = "TEST_NOTIFY";
        marker.body = "test_body";

        Assert(marker.notificationName == "TEST_NOTIFY", "Marker notificationName");
        Assert(marker.body == "test_body", "Marker body");

        Debug.Log("[Test] Bridge_MarkerCreation — PASSED");
    }
    #endregion
}
#endif
