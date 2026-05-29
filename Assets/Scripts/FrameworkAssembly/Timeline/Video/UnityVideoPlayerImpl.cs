using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// IVideoPlayer implementation using Unity's built-in VideoPlayer.
/// Works on all platforms with platform-specific encoding requirements:
///   - Android: H.264 Baseline Profile, no B-frames, resolution aligned to 16x.
///   - iOS: H.264 or H.265.
///   - Windows: most formats via DirectShow/MediaFoundation.
///
/// Usage:
///   var player = new UnityVideoPlayerImpl();
///   player.Load("file://" + Application.streamingAssetsPath + "/intro.mp4");
///   player.Play();
///   rawImage.texture = player.OutputTexture;
/// </summary>
public class UnityVideoPlayerImpl : IVideoPlayer
{
    #region Fields
    private VideoPlayer videoPlayer;
    private GameObject ownerGo;
    private string pendingPath;
    private bool isPrepared;
    private float pendingSeekTime = -1f;
    private float loadTimeout = 10f;
    private float loadTimer;
    private bool isLoading;

    // Loop workaround: Unity's isLooping causes a brief black flash on Android.
    // We manually reset time on loop point instead.
    private bool useManualLoop;

    public float CurrentTime
    {
        get
        {
            if (videoPlayer == null || !isPrepared) return 0f;
            return (float)videoPlayer.time;
        }
    }

    public float Duration
    {
        get
        {
            if (videoPlayer == null || !isPrepared) return 0f;
            return (float)videoPlayer.length;
        }
    }

    public bool IsPlaying
    {
        get
        {
            if (videoPlayer == null || !isPrepared) return false;
            return videoPlayer.isPlaying;
        }
    }

    public bool IsLooping
    {
        get => useManualLoop;
        set
        {
            useManualLoop = value;
            // Never set videoPlayer.isLooping — causes dual-loop conflict with manual loop.
        }
    }

    public Texture OutputTexture
    {
        get
        {
            if (videoPlayer == null) return null;
            return videoPlayer.texture;
        }
    }

    public event Action OnCompleted;
    public event Action<string> OnError;
    #endregion

    #region Constructor & Lifecycle
    public UnityVideoPlayerImpl()
    {
        CreateVideoPlayer();
    }

    private void CreateVideoPlayer()
    {
        ownerGo = new GameObject("UnityVideoPlayerImpl");
        UnityEngine.Object.DontDestroyOnLoad(ownerGo);
        ownerGo.hideFlags = HideFlags.HideAndDontSave;

        videoPlayer = ownerGo.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = new RenderTexture(1920, 1080, 0);
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        // Subscribe to events
        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.loopPointReached += OnLoopPointReached;
        videoPlayer.errorReceived += OnVideoError;
    }

    public void Dispose()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.loopPointReached -= OnLoopPointReached;
            videoPlayer.errorReceived -= OnVideoError;

            if (videoPlayer.isPlaying)
                videoPlayer.Stop();

            if (videoPlayer.targetTexture != null)
            {
                videoPlayer.targetTexture.Release();
                UnityEngine.Object.Destroy(videoPlayer.targetTexture);
            }

            UnityEngine.Object.Destroy(videoPlayer);
            videoPlayer = null;
        }

        if (ownerGo != null)
        {
            UnityEngine.Object.Destroy(ownerGo);
            ownerGo = null;
        }

        isPrepared = false;
        isLoading = false;
    }
    #endregion

    #region IVideoPlayer Implementation
    public void Load(string path)
    {
        if (videoPlayer == null)
        {
            Log.w("UnityVideoPlayerImpl.Load: player is disposed", "VideoPlayer");
            return;
        }

        // If already loading the same path, skip
        if (isLoading && pendingPath == path) return;

        // If currently playing something else, stop first (avoids Android memory leak)
        if (videoPlayer.isPlaying || isPrepared)
        {
            videoPlayer.Stop();
            isPrepared = false;
        }

        pendingPath = path;
        pendingSeekTime = -1f; // reset deferred seek on new load
        videoPlayer.clip = null; // clear clip mode, use url mode
        videoPlayer.url = path;
        videoPlayer.Prepare();

        isLoading = true;
        loadTimer = 0f;
        Log.d($"Video loading: {path}", "VideoPlayer");
    }

    public void LoadClip(VideoClip clip)
    {
        if (videoPlayer == null)
        {
            Log.w("UnityVideoPlayerImpl.LoadClip: player is disposed", "VideoPlayer");
            return;
        }

        if (clip == null)
        {
            Log.w("UnityVideoPlayerImpl.LoadClip: clip is null", "VideoPlayer");
            return;
        }

        // If currently playing something else, stop first
        if (videoPlayer.isPlaying || isPrepared)
        {
            videoPlayer.Stop();
            isPrepared = false;
        }

        pendingPath = clip.name;
        pendingSeekTime = -1f;
        videoPlayer.clip = clip;
        videoPlayer.Prepare();

        isLoading = true;
        loadTimer = 0f;
        Log.d($"Video loaded from clip: {clip.name}", "VideoPlayer");
    }

    public void Play()
    {
        if (videoPlayer == null) return;

        if (!isPrepared)
        {
            Log.w("VideoPlayer.Play(): video not prepared yet", "VideoPlayer");
            return;
        }

        if (videoPlayer.isPlaying) return;

        videoPlayer.Play();
        Log.d("Video playing", "VideoPlayer");
    }

    public void Pause()
    {
        if (videoPlayer == null || !videoPlayer.isPlaying) return;
        videoPlayer.Pause();
        Log.d("Video paused", "VideoPlayer");
    }

    public void Stop()
    {
        if (videoPlayer == null) return;
        videoPlayer.Stop();
        isPrepared = false;
        isLoading = false;
        Log.d("Video stopped", "VideoPlayer");
    }

    public void Seek(float time)
    {
        if (videoPlayer == null) return;

        if (isPrepared)
        {
            videoPlayer.time = time;
        }
        else if (isLoading)
        {
            // Defer seek until prepared
            pendingSeekTime = time;
        }
    }
    #endregion

    #region Timeout Check
    /// <summary>
    /// Call this from Update/MonoBehaviour to detect hung loads.
    /// </summary>
    public void UpdateTimeout(float dt)
    {
        if (!isLoading) return;

        loadTimer += dt;
        if (loadTimer >= loadTimeout)
        {
            Log.e($"Video load timed out after {loadTimeout}s: {pendingPath}", "VideoPlayer");
            isLoading = false;
            videoPlayer?.Stop();
            OnError?.Invoke($"Video load timed out: {pendingPath}");
        }
    }
    #endregion

    #region Event Handlers
    private void OnPrepareCompleted(VideoPlayer source)
    {
        isLoading = false;
        isPrepared = true;
        Log.d($"Video prepared: {pendingPath}, duration={source.length:F2}s", "VideoPlayer");

        // Apply deferred seek
        if (pendingSeekTime >= 0f)
        {
            source.time = pendingSeekTime;
            pendingSeekTime = -1f;
        }
    }

    private void OnLoopPointReached(VideoPlayer source)
    {
        if (useManualLoop)
        {
            // Manual loop: reset and play — avoids black flash on Android
            source.time = 0;
            source.Play();
            Log.d("Video loop restarted (manual)", "VideoPlayer");
        }
        else
        {
            OnCompleted?.Invoke();
        }
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Log.e($"Video error: {message}", "VideoPlayer");
        isLoading = false;
        OnError?.Invoke(message);
    }
    #endregion
}
