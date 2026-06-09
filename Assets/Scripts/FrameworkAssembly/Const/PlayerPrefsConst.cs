/// <summary>
/// PlayerPrefs key constants — Base (FrameworkAssembly).
/// Hot-update keys are added at runtime via HotUpdatePlayerPrefsConst.RegisterTo().
/// </summary>
public static class PlayerPrefsConst
{
    #region Base Keys

    public const string LastAccount = "pf_last_account";
    public const string LastPassword = "pf_last_password";

    #endregion

    #region Audio Volume Keys

    public const string AudioMasterVolume = "audio_master_vol";
    public const string AudioBGMVolume    = "audio_bgm_vol";
    public const string AudioSFXVolume    = "audio_sfx_vol";
    public const string AudioVoiceVolume  = "audio_voice_vol";
    public const string AudioMasterMute   = "audio_master_mute";
    public const string AudioBGMMute      = "audio_bgm_mute";
    public const string AudioSFXMute      = "audio_sfx_mute";
    public const string AudioVoiceMute    = "audio_voice_mute";

    #endregion
}
