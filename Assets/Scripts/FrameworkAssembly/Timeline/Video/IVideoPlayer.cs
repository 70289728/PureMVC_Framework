using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Abstract interface for video playback. Decouples video implementation from
/// the rest of the system — switch between Unity VideoPlayer and AVPro Video
/// by swapping the implementation without touching any call site.
///
/// For displaying video in UI, use VideoAction (Unity backend) or AVProVideoAction
/// (AVPro backend — uses AVPro's DisplayUGUI for correct rendering).
/// </summary>
public interface IVideoPlayer : IDisposable
{
    /// <summary>Current playback position in seconds.</summary>
    float CurrentTime { get; }

    /// <summary>Total video duration in seconds. 0 if not loaded.</summary>
    float Duration { get; }

    /// <summary>Whether the video is currently playing (not paused, not stopped).</summary>
    bool IsPlaying { get; }

    /// <summary>Whether the video loops on completion.</summary>
    bool IsLooping { get; set; }

    /// <summary>Output texture for rendering (assigned to RawImage or Material).</summary>
    Texture OutputTexture { get; }

    /// <summary>
    /// Load a video from the given path.
    /// Accepts local file path, streaming URL, or Resources path (with "file://" prefix).
    /// Call before Play().
    /// </summary>
    void Load(string path);

    /// <summary>Start or resume playback.</summary>
    void Play();

    /// <summary>Pause playback. Resume with Play().</summary>
    void Pause();

    /// <summary>Stop playback and release decoder resources.</summary>
    void Stop();

    /// <summary>Jump to a specific time (seconds). May not be frame-accurate on all platforms.</summary>
    void Seek(float time);

    /// <summary>
    /// Load a VideoClip (e.g., loaded from AssetBundle via AssetBundleManager).
    /// Preferred over Load(string) when video is in AssetBundle — no file path needed.
    /// For Unity backend: sets VideoPlayer.clip directly.
    /// For AVPro backend: NOT supported — use Load(string path) instead.
    /// Call before Play().
    /// </summary>
    void LoadClip(VideoClip clip);

    /// <summary>Fired when the video reaches its end (not fired for looped playback).</summary>
    event Action OnCompleted;

    /// <summary>Fired when an error occurs during loading or playback.</summary>
    event Action<string> OnError;
}
