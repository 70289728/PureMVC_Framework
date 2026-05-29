using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using RenderHeads.Media.AVProVideo;

/// <summary>
/// Quick test scene for video playback via IVideoPlayer + TimelineManager.
/// Works on all platforms including Android (requires OpenGL ES, NOT Vulkan).
///
/// Usage:
///   Unity backend: set RawImage, backend=Unity, Play.
///   AVPro backend: add MediaPlayer+DisplayUGUI to scene, assign to avproMediaPlayer, Play.
///   Editor: defaults to file:// + Assets/ProjectAssets/Base/Video/.
///   Android: auto-loads VideoClip from AssetBundle "base_base_video".
///   testClip in Inspector overrides all auto-detection.
/// </summary>
public class VideoTestRunner : MonoBehaviour
{
    [Tooltip("The RawImage that will display the video (Unity backend only).")]
    public RawImage rawImage;

    [Tooltip("Which backend to test.")]
    public VideoTestBackend backend = VideoTestBackend.Unity;

    [Tooltip("AVPro: existing MediaPlayer in the scene.")]
    public MediaPlayer avproMediaPlayer;

    [Tooltip("Full path to video file. Leave empty to use default StreamingAssets path.\nEditor: Assets/ProjectAssets/Base/Video/xxx.mp4\nAndroid: file:// + Application.streamingAssetsPath/Video/xxx.mp4")]
    public string videoFullPath = "";

    [Tooltip("Video file name (used when videoFullPath is empty). Default: BigBuckBunny-360p30-H264.mp4")]
    public string videoFileName = "BigBuckBunny-360p30-H264.mp4";

    [Tooltip("Assign a VideoClip to test AssetBundle-based loading (Unity backend only). Takes priority over videoFullPath/videoFileName.\nOn Android: auto-loaded from AssetBundle if left null.")]
    public VideoClip testClip;

    [Tooltip("AssetBundle name for video (Android auto-load). Default: base_base_video.ab")]
    public string videoBundleName = "base_base_video.ab";

    [Tooltip("Auto-start on Start().")]
    public bool autoStart = true;

    private IVideoPlayer player;
    private TimelinePlayer timelinePlayer;

    public enum VideoTestBackend { Unity, AVPro }

    void Awake()
    {
        if (avproMediaPlayer != null)
        {
            avproMediaPlayer.AutoOpen = false;
            avproMediaPlayer.AutoStart = false;
        }
        ApplyBackendVisibility();
    }

    void Start()
    {
        if (autoStart) RunTest();
    }

    private void ApplyBackendVisibility()
    {
        bool isUnity = backend == VideoTestBackend.Unity;
        if (rawImage != null)
            rawImage.gameObject.SetActive(isUnity);
        if (avproMediaPlayer != null)
            avproMediaPlayer.gameObject.SetActive(!isUnity);
    }

    /// <summary>
    /// Resolve video path based on platform.
    /// If videoFullPath is set, use it directly.
    /// Editor: defaults to Assets/ProjectAssets/Base/Video/.
    /// Runtime: defaults to StreamingAssets/Video/.
    /// </summary>
    private string ResolveVideoPath()
    {
        if (!string.IsNullOrEmpty(videoFullPath))
            return videoFullPath;

#if UNITY_EDITOR
        // Editor: use project path directly (Assets/ProjectAssets/Base/Video/)
        string editorPath = System.IO.Path.Combine(Application.dataPath, "ProjectAssets", "Base", "Video", videoFileName);
        return "file://" + editorPath.Replace("\\", "/");
#elif UNITY_ANDROID
        // Android: Unity VideoPlayer handles jar URI directly (no file:// prefix)
        string androidPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Video", videoFileName);
        return androidPath.Replace("\\", "/");
#else
        // Standalone / iOS: file:// + StreamingAssets
        string runtimePath = System.IO.Path.Combine(Application.streamingAssetsPath, "Video", videoFileName);
        return "file://" + runtimePath.Replace("\\", "/");
#endif
    }

    [ContextMenu("Run Test")]
    public void RunTest()
    {
        Cleanup();
        ApplyBackendVisibility();

        ITimelineAction videoAction;

        // Android / non-Editor: auto-load VideoClip from AssetBundle if testClip not assigned
#if !UNITY_EDITOR
        if (testClip == null && backend == VideoTestBackend.Unity && AssetBundleManager.Instance != null)
        {
            testClip = AssetBundleManager.Instance.LoadAsset<VideoClip>(videoBundleName, videoFileName);
            if (testClip != null)
                Log.d($"Auto-loaded VideoClip from AssetBundle [{videoBundleName}]: {videoFileName}", "VideoTest");
            else
                Log.w($"Failed to load VideoClip from AssetBundle [{videoBundleName}]: {videoFileName}", "VideoTest");
        }
#endif

        // Priority 1: VideoClip from AssetBundle (Unity backend only)
        if (testClip != null && backend == VideoTestBackend.Unity)
        {
            player = new UnityVideoPlayerImpl();
            player.LoadClip(testClip);
            videoAction = new VideoAction(player, rawImage, preloaded: true);
            Log.d($"Playing [Unity/AssetBundle]: {testClip.name}", "VideoTest");
        }
        else
        {
            string path = ResolveVideoPath();

            if (backend == VideoTestBackend.Unity)
            {
                player = new UnityVideoPlayerImpl();
                videoAction = new VideoAction(player, rawImage, path, ownsPlayer: true);
            }
            else
            {
                if (avproMediaPlayer == null)
                {
                    Log.e("Assign avproMediaPlayer field!", "VideoTest");
                    return;
                }
                player = null;
                avproMediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, path, autoPlay: false);
                videoAction = new AVProControlAction(avproMediaPlayer);
            }

            Log.d($"Playing [{backend}]: {path}", "VideoTest");
        }

        var clips = new List<TimelineClip>
        {
            new TimelineClip
            {
                startTime = 0f,
                duration = 10f,
                action = videoAction,
            },
        };

        timelinePlayer = TimelineManager.Instance.PlayTimeline(clips);
    }

    [ContextMenu("Stop")]
    public void StopTest()
    {
        if (timelinePlayer != null) { timelinePlayer.Stop(); timelinePlayer = null; }
        Cleanup();
    }

    private void Cleanup()
    {
        if (player != null) { player.Dispose(); player = null; }
    }

    void OnDestroy()
    {
        StopTest();
    }
}
