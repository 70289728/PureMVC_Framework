/// <summary>
/// Time mode for timeline playback.
/// </summary>
public enum TimelineTimeMode
{
    /// <summary>Use Time.deltaTime, affected by Time.timeScale.</summary>
    Scaled,
    /// <summary>Use Time.unscaledDeltaTime, independent of Time.timeScale.</summary>
    Unscaled,
}
