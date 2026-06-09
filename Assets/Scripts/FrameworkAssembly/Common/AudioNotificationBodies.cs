/// <summary>
/// Notification body for AudioManager BGM change events.
/// </summary>
public class AudioBGMChangedBody
{
    public int configId;
    public string name;
}

/// <summary>
/// Notification body for AudioManager volume change events.
/// </summary>
public class AudioVolumeChangedBody
{
    public int channel;   // 0=Master, 1=BGM, 2=SFX, 3=Voice
    public float volume;  // 0~1
}

/// <summary>
/// Notification body for AudioManager mute change events.
/// </summary>
public class AudioMuteChangedBody
{
    public int channel;   // 0=Master, 1=BGM, 2=SFX, 3=Voice
    public bool muted;
}
