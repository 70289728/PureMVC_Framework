using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Timeline clip action that plays a video via Unity VideoPlayer.
/// OnEnter: loads and plays the video.
/// OnUpdate: assigns texture when ready, drives load timeout check.
/// OnExit: stops the video and optionally disposes the player.
///
/// Usage:
///   var action = new VideoAction(player, rawImage, "intro.mp4");
///
/// For AVPro video with correct rendering (no flip/brightness issues), use AVProVideoAction instead.
/// </summary>
public class VideoAction : ITimelineAction
{
    #region Fields
    private IVideoPlayer player;
    private RawImage renderTarget;
    private string videoPath;
    private bool isPlaying;
    private bool loadRequested;
    private bool ownsPlayer;
    private bool textureAssigned;
    private bool preloaded;
    #endregion

    #region Constructors
    public VideoAction(IVideoPlayer player, RawImage renderTarget, string videoPath, bool ownsPlayer = false)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.renderTarget = renderTarget;
        this.videoPath = videoPath;
        this.ownsPlayer = ownsPlayer;
    }

    public VideoAction(IVideoPlayer player, string videoPath, bool ownsPlayer = false)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.videoPath = videoPath;
        this.ownsPlayer = ownsPlayer;
    }

    /// <summary>
    /// Preloaded clip mode: video already loaded via IVideoPlayer.LoadClip().
    /// OnEnter skips Load(), OnUpdate handles texture assignment + Play() as normal.
    /// </summary>
    public VideoAction(IVideoPlayer player, RawImage renderTarget, bool preloaded)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.renderTarget = renderTarget;
        this.videoPath = "";
        this.preloaded = true;
    }
    #endregion

    #region ITimelineAction
    public void OnEnter(TimelineContext ctx)
    {
        if (preloaded)
        {
            // Clip was already loaded via IVideoPlayer.LoadClip() before entering.
            // OnUpdate will detect it's ready and trigger Play().
            textureAssigned = false;
            player.OnCompleted += OnVideoCompleted;
            loadRequested = true;
            isPlaying = false;
            return;
        }

        if (string.IsNullOrEmpty(videoPath))
        {
            Log.w($"VideoAction: videoPath is empty, clipIndex={ctx.clipIndex}", "VideoAction");
            return;
        }

        textureAssigned = false;
        player.OnCompleted += OnVideoCompleted;
        player.Load(videoPath);
        loadRequested = true;
        isPlaying = false;
    }

    public void OnUpdate(TimelineContext ctx, float elapsed)
    {
        if (player == null) return;

        // Drive timeout check for UnityVideoPlayerImpl
        if (player is UnityVideoPlayerImpl impl)
            impl.UpdateTimeout(Time.unscaledDeltaTime);

        // Assign texture once it's available (async after Load)
        if (!textureAssigned && renderTarget != null && player.OutputTexture != null)
        {
            renderTarget.texture = player.OutputTexture;
            textureAssigned = true;
        }

        // Start playback once prepared
        if (loadRequested && !isPlaying && player.IsPlaying)
        {
            isPlaying = true;
            loadRequested = false;
        }
        else if (loadRequested && !isPlaying)
        {
            if (player.Duration > 0f)
            {
                player.Play();
                isPlaying = true;
                loadRequested = false;
            }
        }
    }

    public void OnExit(TimelineContext ctx)
    {
        if (player == null) return;

        player.OnCompleted -= OnVideoCompleted;

        if (renderTarget != null)
            renderTarget.texture = null;

        player.Stop();
        isPlaying = false;
        loadRequested = false;
        textureAssigned = false;

        if (ownsPlayer)
            player.Dispose();
    }
    #endregion

    #region Callbacks
    private void OnVideoCompleted()
    {
        Log.d("VideoAction: video completed", "VideoAction");
    }
    #endregion
}
