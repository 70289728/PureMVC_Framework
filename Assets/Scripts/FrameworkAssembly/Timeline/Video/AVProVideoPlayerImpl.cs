using System;
using UnityEngine;
using UnityEngine.Video;
using RenderHeads.Media.AVProVideo;

/// <summary>
/// IVideoPlayer implementation using AVPro Video.
/// Wraps AVPro's MediaPlayer component behind the IVideoPlayer interface.
///
/// AVPro advantages over Unity VideoPlayer:
///   - Consistent decoding across all platforms (FFmpeg-based)
///   - Alpha channel video support
///   - Frame-accurate seeking
///   - HLS/DASH streaming with adaptive bitrate
///   - Seamless looping
///
/// Usage:
///   var player = new AVProVideoPlayerImpl();
///   player.Load("intro.mp4");
///   player.Play();
///   rawImage.texture = player.OutputTexture;
/// </summary>
public class AVProVideoPlayerImpl : IVideoPlayer
{
    #region Fields
    private MediaPlayer mediaPlayer;
    private GameObject ownerGo;
    private string pendingPath;
    private bool isLoaded;
    private bool isLooping;
    private float pendingSeekTime = -1f;

    public float CurrentTime
    {
        get
        {
            if (mediaPlayer == null || !isLoaded) return 0f;
            return (float)mediaPlayer.Control.GetCurrentTime();
        }
    }

    public float Duration
    {
        get
        {
            if (mediaPlayer == null || !isLoaded) return 0f;
            return (float)mediaPlayer.Info.GetDuration();
        }
    }

    public bool IsPlaying
    {
        get
        {
            if (mediaPlayer == null || !isLoaded) return false;
            return mediaPlayer.Control.IsPlaying();
        }
    }

    public bool IsLooping
    {
        get => isLooping;
        set
        {
            isLooping = value;
            if (mediaPlayer != null)
                mediaPlayer.Control.SetLooping(value);
        }
    }

    public Texture OutputTexture
    {
        get
        {
            if (mediaPlayer == null) return null;
            return mediaPlayer.TextureProducer?.GetTexture();
        }
    }

    public event Action OnCompleted;
    public event Action<string> OnError;
    #endregion

    #region Constructor & Lifecycle
    public AVProVideoPlayerImpl()
    {
        CreateMediaPlayer();
    }

    private void CreateMediaPlayer()
    {
        ownerGo = new GameObject("AVProVideoPlayerImpl");
        UnityEngine.Object.DontDestroyOnLoad(ownerGo);
        ownerGo.hideFlags = HideFlags.HideAndDontSave;

        mediaPlayer = ownerGo.AddComponent<MediaPlayer>();
        mediaPlayer.AutoOpen = false;
        mediaPlayer.AutoStart = false;

        // Subscribe to events
        mediaPlayer.Events.AddListener(OnMediaPlayerEvent);
    }

    public void Dispose()
    {
        if (mediaPlayer != null)
        {
            mediaPlayer.Events.RemoveListener(OnMediaPlayerEvent);
            mediaPlayer.Control.CloseMedia();

            UnityEngine.Object.Destroy(mediaPlayer);
            mediaPlayer = null;
        }

        if (ownerGo != null)
        {
            UnityEngine.Object.Destroy(ownerGo);
            ownerGo = null;
        }

        isLoaded = false;
    }
    #endregion

    #region IVideoPlayer Implementation
    public void Load(string path)
    {
        if (mediaPlayer == null)
        {
            Log.w("AVProVideoPlayerImpl.Load: player is disposed", "VideoPlayer");
            return;
        }

        // If currently playing, close first
        if (isLoaded)
        {
            mediaPlayer.Control.CloseMedia();
            isLoaded = false;
        }

        pendingPath = path;
        pendingSeekTime = -1f;

        // AVPro: open media with path type and path string directly
        mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, path, autoPlay: false);
        isLoaded = true;

        Log.d($"AVPro video loaded: {path}", "VideoPlayer");
    }

    public void LoadClip(VideoClip clip)
    {
        Log.w("AVProVideoPlayerImpl.LoadClip: VideoClip is not supported. Use Load(string path) for AVPro backend.", "VideoPlayer");
    }

    public void Play()
    {
        if (mediaPlayer == null || !isLoaded) return;
        if (mediaPlayer.Control.IsPlaying()) return;

        mediaPlayer.Control.Play();

        // Apply deferred seek
        if (pendingSeekTime >= 0f)
        {
            mediaPlayer.Control.Seek(pendingSeekTime);
            pendingSeekTime = -1f;
        }

        Log.d("AVPro video playing", "VideoPlayer");
    }

    public void Pause()
    {
        if (mediaPlayer == null || !isLoaded) return;
        if (!mediaPlayer.Control.IsPlaying()) return;

        mediaPlayer.Control.Pause();
        Log.d("AVPro video paused", "VideoPlayer");
    }

    public void Stop()
    {
        if (mediaPlayer == null) return;

        mediaPlayer.Control.Stop();
        Log.d("AVPro video stopped", "VideoPlayer");
    }

    public void Seek(float time)
    {
        if (mediaPlayer == null) return;

        if (isLoaded)
        {
            mediaPlayer.Control.Seek(time);
        }
        else
        {
            pendingSeekTime = time;
        }
    }
    #endregion

    #region Event Handlers
    private void OnMediaPlayerEvent(MediaPlayer mp, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
    {
        switch (eventType)
        {
            case MediaPlayerEvent.EventType.FinishedPlaying:
                if (!isLooping)
                {
                    OnCompleted?.Invoke();
                }
                break;

            case MediaPlayerEvent.EventType.Error:
                string errorMsg = $"AVPro error: {errorCode}";
                Log.e(errorMsg, "VideoPlayer");
                OnError?.Invoke(errorMsg);
                break;

            case MediaPlayerEvent.EventType.Started:
                Log.d("AVPro video started", "VideoPlayer");
                break;

            case MediaPlayerEvent.EventType.ReadyToPlay:
                Log.d($"AVPro video ready: {pendingPath}, duration={Duration:F2}s", "VideoPlayer");
                break;
        }
    }
    #endregion
}
