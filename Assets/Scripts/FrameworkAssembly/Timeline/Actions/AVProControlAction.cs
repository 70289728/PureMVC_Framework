using System;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

/// <summary>
/// Timeline clip action that controls an existing AVPro MediaPlayer.
/// Does NOT create or manage MediaPlayer or DisplayUGUI — those are set up in the scene/editor.
/// Only sends play/pause/stop/seek commands via the existing AVPro control interface.
///
/// Usage:
///   1. Add MediaPlayer + DisplayUGUI to scene via Inspector (standard AVPro workflow).
///   2. Reference the MediaPlayer in this action.
///   3. Timeline clip drives playback.
///
///   var action = new AVProControlAction(mediaPlayer);
/// </summary>
public class AVProControlAction : ITimelineAction
{
    #region Fields
    private MediaPlayer mediaPlayer;
    private bool started;
    #endregion

    #region Constructors
    /// <summary>Control an existing MediaPlayer from a Timeline.</summary>
    public AVProControlAction(MediaPlayer mediaPlayer)
    {
        this.mediaPlayer = mediaPlayer ?? throw new ArgumentNullException(nameof(mediaPlayer));
    }
    #endregion

    #region ITimelineAction
    public void OnEnter(TimelineContext ctx)
    {
        if (mediaPlayer == null || mediaPlayer.Control == null) return;

        if (!mediaPlayer.Control.IsPlaying())
        {
            mediaPlayer.Control.Play();
            started = true;
            Log.d($"AVProControlAction: playing", "AVProControlAction");
        }
    }

    public void OnUpdate(TimelineContext ctx, float elapsed)
    {
        // No continuous work needed — AVPro handles playback internally
    }

    public void OnExit(TimelineContext ctx)
    {
        if (mediaPlayer == null || mediaPlayer.Control == null) return;

        if (started)
        {
            mediaPlayer.Control.Pause();
            started = false;
            Log.d($"AVProControlAction: paused", "AVProControlAction");
        }
    }
    #endregion
}
