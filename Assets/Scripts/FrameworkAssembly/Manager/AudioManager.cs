using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages BGM and SFX playback.
/// Audio clips are loaded from Resources/Audio/BGM and Resources/Audio/SFX.
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("AudioManager");
                instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Member Variables
    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private bool bgmMuted = false;
    private bool sfxMuted = false;

    private const string BGM_PATH = "Audio/BGM/";
    private const string SFX_PATH = "Audio/SFX/";
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        LoadVolumeSettings();
    }
    #endregion

    #region BGM
    /// <summary>
    /// Play background music. Stops current BGM first.
    /// </summary>
    public void PlayBGM(string name)
    {
        AudioClip clip = GetClip(BGM_PATH + name);
        if (clip == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.volume = bgmMuted ? 0f : bgmVolume;
        bgmSource.Play();
        Log.d($"PlayBGM: {name}", "AudioManager");
    }

    /// <summary>
    /// Stop current background music
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    /// <summary>
    /// Pause background music
    /// </summary>
    public void PauseBGM() => bgmSource.Pause();

    /// <summary>
    /// Resume background music
    /// </summary>
    public void ResumeBGM() => bgmSource.UnPause();
    #endregion

    #region SFX
    /// <summary>
    /// Play a sound effect (one-shot)
    /// </summary>
    public void PlaySFX(string name)
    {
        AudioClip clip = GetClip(SFX_PATH + name);
        if (clip == null) return;

        float vol = sfxMuted ? 0f : sfxVolume;
        sfxSource.PlayOneShot(clip, vol);
    }

    /// <summary>
    /// Stop all currently playing SFX
    /// </summary>
    public void StopSFX() => sfxSource.Stop();
    #endregion

    #region Volume Control
    /// <summary>
    /// Set BGM and SFX volume simultaneously (0~1)
    /// </summary>
    public void SetVolume(float bgm, float sfx)
    {
        SetBGMVolume(bgm);
        SetSFXVolume(sfx);
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (!bgmMuted) bgmSource.volume = bgmVolume;
        SaveVolumeSettings();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
    }

    public void MuteBGM(bool mute)
    {
        bgmMuted = mute;
        bgmSource.volume = mute ? 0f : bgmVolume;
        SaveVolumeSettings();
    }

    public void MuteSFX(bool mute)
    {
        sfxMuted = mute;
        SaveVolumeSettings();
    }

    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;
    public bool IsBGMMuted => bgmMuted;
    public bool IsSFXMuted => sfxMuted;
    #endregion

    #region Helper Methods
    private AudioClip GetClip(string path)
    {
        if (clipCache.TryGetValue(path, out AudioClip cached))
            return cached;

        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            Log.w($"AudioClip not found: {path}", "AudioManager");
            return null;
        }

        clipCache[path] = clip;
        return clip;
    }

    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat("audio_bgm_vol", bgmVolume);
        PlayerPrefs.SetFloat("audio_sfx_vol", sfxVolume);
        PlayerPrefs.SetInt("audio_bgm_mute", bgmMuted ? 1 : 0);
        PlayerPrefs.SetInt("audio_sfx_mute", sfxMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadVolumeSettings()
    {
        bgmVolume = PlayerPrefs.GetFloat("audio_bgm_vol", 1f);
        sfxVolume = PlayerPrefs.GetFloat("audio_sfx_vol", 1f);
        bgmMuted  = PlayerPrefs.GetInt("audio_bgm_mute", 0) == 1;
        sfxMuted  = PlayerPrefs.GetInt("audio_sfx_mute", 0) == 1;
    }

    /// <summary>
    /// Pre-warm the cache for a list of clip names
    /// </summary>
    public void Preload(string[] bgmNames = null, string[] sfxNames = null)
    {
        if (bgmNames != null)
            foreach (var n in bgmNames) GetClip(BGM_PATH + n);
        if (sfxNames != null)
            foreach (var n in sfxNames) GetClip(SFX_PATH + n);
    }
    #endregion
}
