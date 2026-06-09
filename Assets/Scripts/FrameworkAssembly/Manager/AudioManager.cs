using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central audio engine. Manages BGM, SFX pool, Voice playback with AudioMixer channel routing.
/// Audio clips are loaded via AssetBundle (priority) with Resources fallback.
/// Volume settings are persisted via PlayerPrefs.
///
/// For PureMVC integration, use AudioProxy which wraps this manager and sends notifications.
/// Direct usage is supported for simple cases but AudioProxy is recommended.
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Enums

    public enum AudioChannel
    {
        Master = 0,
        BGM = 1,
        SFX = 2,
        Voice = 3
    }

    public enum AudioPlayType
    {
        BGM = 0,
        SFX = 1,
        Voice = 2
    }

    #endregion

    #region Singleton

    private static AudioManager instance;
    private static readonly object _instanceLock = new object();
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                lock (_instanceLock)
                {
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AudioManager");
                        instance = go.AddComponent<AudioManager>();
                        DontDestroyOnLoad(go);
                    }
                }
            }
            return instance;
        }
    }

    #endregion

    #region Inspector Fields

    /// <summary>
    /// AudioMixer asset reference. If null, falls back to direct AudioSource volume control.
    /// Assign via Inspector or Editor script.
    /// </summary>
    [SerializeField] private AudioMixer mixer;

    [Header("SFX Pool")]
    [SerializeField, Range(4, 64)]  private int sfxPoolMax = 16;
    [SerializeField, Range(0, 16)]  private int sfxPoolPreWarm = 4;
    [SerializeField, Range(1, 20)]  private int sfxMaxPerFrame = 3;

    [Header("Clip Cache")]
    [SerializeField, Range(32, 512)] private int clipCacheMax = 128;

    #endregion

    #region Mixer Constants

    private const string MIXER_PATH = "Audio/AudioMixer";
    private const string MIXER_PARAM_MASTER = "MasterVolume";
    private const string MIXER_PARAM_BGM = "BGMVolume";
    private const string MIXER_PARAM_SFX = "SFXVolume";
    private const string MIXER_PARAM_VOICE = "VoiceVolume";

    #endregion

    #region AssetBundle Constants

    private const string AB_AUDIO_RES_TYPE = "audio";
    private const string AB_LAYER_PREFIX_BASE = "base_base";
    private const string AB_LAYER_PREFIX_HOTFIX = "hotfix_hotupdate";

    #endregion

    #region BGM

    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;
    private GameObject bgmFadeTempGo;         // tracked for cleanup on fade interruption
    private int currentBgmConfigId = -1;
    private const float DEFAULT_FADE_DURATION = 1.0f;
    private bool isInitializing = true;       // suppress events during Awake

    #endregion

    #region SFX Pool

    private Queue<AudioSource> sfxFreePool = new Queue<AudioSource>();
    private List<AudioSource> sfxActiveList = new List<AudioSource>();
    private GameObject sfxPoolRoot;
    private int sfxFrameCount = 0;

    #endregion

    #region Voice

    private AudioSource voiceSource;

    #endregion

    #region Clip Cache

    private Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    #endregion

    #region Config (set by AudioProxy)

    internal Dictionary<int, AudioConfig> ConfigMap { get; set; }
    internal Dictionary<string, int> NameToIdMap { get; set; }

    #endregion

    #region Volume Settings

    private float[] channelVolume = new float[4] { 1f, 1f, 1f, 1f };
    private bool[] channelMuted = new bool[4];

    #endregion

    #region Events

    /// <summary>Fired when BGM changes. Parameter: configId (-1 if stopped).</summary>
    public event Action<int> OnBGMChanged;

    /// <summary>Fired when volume changes. Parameters: channel, newVolume.</summary>
    public event Action<AudioChannel, float> OnVolumeChanged;

    /// <summary>Fired when mute state changes. Parameters: channel, muted.</summary>
    public event Action<AudioChannel, bool> OnMuteChanged;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        isInitializing = true;

        // BGM source
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        // Voice source
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.loop = false;
        voiceSource.playOnAwake = false;

        // SFX pool
        sfxPoolMax = Mathf.Max(4, sfxPoolMax);
        sfxPoolPreWarm = Mathf.Min(sfxPoolPreWarm, sfxPoolMax);
        sfxPoolRoot = new GameObject("SFXPool");
        sfxPoolRoot.transform.SetParent(transform);
        for (int i = 0; i < sfxPoolPreWarm; i++)
        {
            var src = CreateSFXSource();
            sfxFreePool.Enqueue(src);
        }

        // Load AudioMixer if not assigned in Inspector
        if (mixer == null)
        {
            mixer = Resources.Load<AudioMixer>(MIXER_PATH);
            if (mixer == null)
            {
                Log.d("AudioMixer not found — using direct volume control", "AudioManager");
            }
        }

        // Route sources to mixer groups
        if (mixer != null)
        {
            var bgmGroup = mixer.FindMatchingGroups("BGM");
            if (bgmGroup != null && bgmGroup.Length > 0)
                bgmSource.outputAudioMixerGroup = bgmGroup[0];

            var sfxGroup = mixer.FindMatchingGroups("SFX");
            if (sfxGroup != null && sfxGroup.Length > 0)
            {
                foreach (var src in sfxFreePool)
                    src.outputAudioMixerGroup = sfxGroup[0];
            }

            var voiceGroup = mixer.FindMatchingGroups("Voice");
            if (voiceGroup != null && voiceGroup.Length > 0)
                voiceSource.outputAudioMixerGroup = voiceGroup[0];
        }

        LoadVolumeSettings();
        ApplyAllVolumes();
        isInitializing = false;
    }

    void Update()
    {
        // Reset per-frame SFX counter
        sfxFrameCount = 0;

        // Check and recycle finished SFX sources
        for (int i = sfxActiveList.Count - 1; i >= 0; i--)
        {
            var src = sfxActiveList[i];
            if (!src.isPlaying)
            {
                ReturnSFXSource(src);
                sfxActiveList.RemoveAt(i);
            }
        }
    }

    #endregion

    #region Config-Driven Playback (Primary API)

    /// <summary>
    /// Play BGM by config ID. Cross-fades from current BGM if one is playing.
    /// </summary>
    /// <param name="configId">Config ID from audioConfig table.</param>
    /// <param name="fadeDuration">Cross-fade duration in seconds. Default=1.0s, 0=instant switch.</param>
    public void PlayBGM(int configId, float fadeDuration = -1f)
    {
        if (!TryGetConfig(configId, out var cfg)) return;
        if (cfg.type != (int)AudioPlayType.BGM)
        {
            Log.w($"PlayBGM: config {configId} is not BGM type (type={cfg.type})", "AudioManager");
            return;
        }

        var clip = GetOrLoadClip(cfg.path, cfg.layer);
        if (clip == null) return;

        float fd = fadeDuration < 0 ? DEFAULT_FADE_DURATION : fadeDuration;
        PlayBGMInternal(clip, cfg.volume, cfg.loop, fd, configId);
    }

    /// <summary>
    /// Play BGM by config name. Convenience overload.
    /// </summary>
    public void PlayBGMByName(string name, float fadeDuration = -1f)
    {
        if (NameToIdMap != null && NameToIdMap.TryGetValue(name, out int id))
        {
            PlayBGM(id, fadeDuration);
        }
        else
        {
            Log.w($"PlayBGMByName: config name '{name}' not found", "AudioManager");
        }
    }

    /// <summary>
    /// Play SFX by config ID. Uses pooled AudioSource for one-shot playback.
    /// </summary>
    public void PlaySFX(int configId)
    {
        if (!TryGetConfig(configId, out var cfg)) return;
        if (cfg.type != (int)AudioPlayType.SFX)
        {
            Log.w($"PlaySFX: config {configId} is not SFX type (type={cfg.type})", "AudioManager");
            return;
        }

        var clip = GetOrLoadClip(cfg.path, cfg.layer);
        if (clip == null) return;

        PlaySFXInternal(clip, cfg.volume);
    }

    /// <summary>
    /// Play SFX by config name.
    /// </summary>
    public void PlaySFXByName(string name)
    {
        if (NameToIdMap != null && NameToIdMap.TryGetValue(name, out int id))
        {
            PlaySFX(id);
        }
        else
        {
            Log.w($"PlaySFXByName: config name '{name}' not found", "AudioManager");
        }
    }

    /// <summary>
    /// Play Voice by config ID. Stops any currently playing voice first.
    /// </summary>
    public void PlayVoice(int configId)
    {
        if (!TryGetConfig(configId, out var cfg)) return;
        if (cfg.type != (int)AudioPlayType.Voice)
        {
            Log.w($"PlayVoice: config {configId} is not Voice type (type={cfg.type})", "AudioManager");
            return;
        }

        var clip = GetOrLoadClip(cfg.path, cfg.layer);
        if (clip == null) return;

        PlayVoiceInternal(clip, cfg.volume);
    }

    /// <summary>
    /// Play Voice by config name.
    /// </summary>
    public void PlayVoiceByName(string name)
    {
        if (NameToIdMap != null && NameToIdMap.TryGetValue(name, out int id))
        {
            PlayVoice(id);
        }
        else
        {
            Log.w($"PlayVoiceByName: config name '{name}' not found", "AudioManager");
        }
    }

    #endregion

    #region Direct Clip Playback (Backward Compatible)

    /// <summary>
    /// Play BGM by AudioClip directly.
    /// </summary>
    public void PlayBGMByClip(AudioClip clip, float volume = 1f, bool loop = true, float fadeDuration = -1f)
    {
        if (clip == null)
        {
            Log.w("PlayBGMByClip: clip is null", "AudioManager");
            return;
        }

        float fd = fadeDuration < 0 ? DEFAULT_FADE_DURATION : fadeDuration;
        PlayBGMInternal(clip, volume, loop, fd, -1);
    }

    /// <summary>
    /// Play SFX by AudioClip directly.
    /// </summary>
    public void PlaySFXByClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            Log.w("PlaySFXByClip: clip is null", "AudioManager");
            return;
        }

        PlaySFXInternal(clip, volume);
    }

    #endregion

    #region Legacy String-Based API (Obsolete)

    [Obsolete("Use PlayBGM(int configId) or PlayBGMByName(string name) instead.")]
    public void PlayBGM(string name)
    {
        var clip = GetOrLoadClip("Audio/BGM/" + name, 0);
        if (clip == null) return;
        PlayBGMInternal(clip, 1f, true, DEFAULT_FADE_DURATION, -1);
        Log.d($"PlayBGM: {name}", "AudioManager");
    }

    [Obsolete("Use PlaySFX(int configId) or PlaySFXByName(string name) instead.")]
    public void PlaySFX(string name)
    {
        var clip = GetOrLoadClip("Audio/SFX/" + name, 0);
        if (clip == null) return;
        PlaySFXInternal(clip, 1f);
    }

    #endregion

    #region Playback Control

    /// <summary>Stop BGM immediately. Fires OnBGMChanged(-1).</summary>
    public void StopBGM()
    {
        InterruptCrossFade();
        bgmSource.Stop();
        bgmSource.clip = null;
        int prevId = currentBgmConfigId;
        currentBgmConfigId = -1;
        if (prevId >= 0)
            OnBGMChanged?.Invoke(-1);
    }

    /// <summary>Pause BGM.</summary>
    public void PauseBGM() => bgmSource.Pause();

    /// <summary>Resume BGM.</summary>
    public void ResumeBGM() => bgmSource.UnPause();

    /// <summary>Stop all active SFX.</summary>
    public void StopAllSFX()
    {
        foreach (var src in sfxActiveList)
        {
            src.Stop();
            src.clip = null;
            ReturnSFXSource(src);
        }
        sfxActiveList.Clear();
    }

    /// <summary>Stop voice.</summary>
    public void StopVoice()
    {
        voiceSource.Stop();
        voiceSource.clip = null;
    }

    /// <summary>Get current BGM config ID. Returns -1 if no BGM playing.</summary>
    public int CurrentBGMId => currentBgmConfigId;

    /// <summary>Whether BGM is currently playing.</summary>
    public bool IsBGMPlaying => bgmSource.isPlaying;

    /// <summary>Whether voice is currently playing.</summary>
    public bool IsVoicePlaying => voiceSource.isPlaying;

    /// <summary>Number of active SFX sources.</summary>
    public int ActiveSFXCount => sfxActiveList.Count;

    #endregion

    #region Volume Control

    /// <summary>Set volume for a channel (0~1).</summary>
    public void SetVolume(AudioChannel channel, float volume)
    {
        volume = Mathf.Clamp01(volume);
        channelVolume[(int)channel] = volume;
        ApplyVolume(channel);
        SaveVolumeSettings();
        if (!isInitializing) OnVolumeChanged?.Invoke(channel, volume);
    }

    /// <summary>Get current volume for a channel.</summary>
    public float GetVolume(AudioChannel channel)
    {
        return channelVolume[(int)channel];
    }

    /// <summary>Mute or unmute a channel.</summary>
    public void Mute(AudioChannel channel, bool mute)
    {
        channelMuted[(int)channel] = mute;
        ApplyVolume(channel);
        SaveVolumeSettings();
        if (!isInitializing) OnMuteChanged?.Invoke(channel, mute);
    }

    /// <summary>Check if a channel is muted.</summary>
    public bool IsMuted(AudioChannel channel)
    {
        return channelMuted[(int)channel];
    }

    /// <summary>Set all channel volumes at once.</summary>
    public void SetAllVolumes(float master, float bgm, float sfx, float voice)
    {
        SetVolume(AudioChannel.Master, master);
        SetVolume(AudioChannel.BGM, bgm);
        SetVolume(AudioChannel.SFX, sfx);
        SetVolume(AudioChannel.Voice, voice);
    }

    #endregion

    #region Preload

    /// <summary>
    /// Pre-warm cache for a specific clip path. Useful before important SFX playback.
    /// </summary>
    public void PreloadClip(string path)
    {
        GetOrLoadClip(path, 0);
    }

    /// <summary>
    /// Preload clips for a set of config IDs.
    /// </summary>
    public void PreloadConfigs(params int[] configIds)
    {
        foreach (int id in configIds)
        {
            if (ConfigMap != null && ConfigMap.TryGetValue(id, out var cfg))
            {
                GetOrLoadClip(cfg.path, cfg.layer);
            }
        }
    }

    /// <summary>
    /// Preload a list of clip names from Resources (legacy compatibility).
    /// </summary>
    [Obsolete("Use PreloadConfigs instead.")]
    public void Preload(string[] bgmNames = null, string[] sfxNames = null)
    {
        if (bgmNames != null)
            foreach (var n in bgmNames) PreloadClip("Audio/BGM/" + n);
        if (sfxNames != null)
            foreach (var n in sfxNames) PreloadClip("Audio/SFX/" + n);
    }

    #endregion

    #region Internal — BGM

    private void PlayBGMInternal(AudioClip clip, float volume, bool loop, float fadeDuration, int configId)
    {
        // If no current BGM, play immediately
        if (bgmSource.clip == null || !bgmSource.isPlaying)
        {
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = GetEffectiveVolume(AudioChannel.BGM, volume);
            bgmSource.Play();
            currentBgmConfigId = configId;
            OnBGMChanged?.Invoke(configId);
            return;
        }

        // Same clip already playing — skip
        if (bgmSource.clip == clip)
            return;

        // Interrupt any in-progress cross-fade and clean up its temp objects
        InterruptCrossFade();

        // Instant switch — no need for coroutine temp objects
        if (fadeDuration <= 0f)
        {
            bgmSource.Stop();
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = GetEffectiveVolume(AudioChannel.BGM, volume);
            bgmSource.Play();
            currentBgmConfigId = configId;
            OnBGMChanged?.Invoke(configId);
            return;
        }

        bgmFadeCoroutine = StartCoroutine(CrossFadeBGM(clip, volume, loop, fadeDuration, configId));
    }

    private void InterruptCrossFade()
    {
        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }
        if (bgmFadeTempGo != null)
        {
            Destroy(bgmFadeTempGo);
            bgmFadeTempGo = null;
        }
    }

    private IEnumerator CrossFadeBGM(AudioClip newClip, float volume, bool loop, float duration, int configId)
    {
        // Create temporary source for new BGM
        GameObject tempGo = new GameObject("BGMFadeTemp");
        tempGo.transform.SetParent(transform);
        bgmFadeTempGo = tempGo;

        var newSrc = tempGo.AddComponent<AudioSource>();
        newSrc.clip = newClip;
        newSrc.loop = loop;
        newSrc.volume = 0f;
        newSrc.playOnAwake = false;

        if (mixer != null)
        {
            var bgmGroup = mixer.FindMatchingGroups("BGM");
            if (bgmGroup != null && bgmGroup.Length > 0)
                newSrc.outputAudioMixerGroup = bgmGroup[0];
        }

        newSrc.Play();

        // Fade: old out, new in
        float elapsed = 0f;
        float oldStartVolume = GetEffectiveVolume(AudioChannel.BGM, volume);
        float targetNewVol = GetEffectiveVolume(AudioChannel.BGM, volume);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            bgmSource.volume = Mathf.Lerp(oldStartVolume, 0f, t);
            newSrc.volume = Mathf.Lerp(0f, targetNewVol, t);
            yield return null;
        }

        // Swap
        bgmSource.Stop();
        bgmSource.clip = newClip;
        bgmSource.loop = loop;
        bgmSource.volume = targetNewVol;
        bgmSource.time = newSrc.time;
        bgmSource.Play();

        newSrc.Stop();
        if (bgmFadeTempGo == tempGo)
            bgmFadeTempGo = null;
        Destroy(tempGo);

        currentBgmConfigId = configId;
        OnBGMChanged?.Invoke(configId);
        bgmFadeCoroutine = null;
    }

    #endregion

    #region Internal — SFX

    private void PlaySFXInternal(AudioClip clip, float volume)
    {
        // Per-frame limit to prevent performance spikes
        if (sfxFrameCount >= sfxMaxPerFrame)
            return;

        var src = GetFreeSFXSource();
        if (src == null)
        {
            Log.w("PlaySFXInternal: no free SFX source available", "AudioManager");
            return;
        }

        src.clip = clip;
        src.volume = GetEffectiveVolume(AudioChannel.SFX, volume);
        src.Play();
        sfxActiveList.Add(src);
        sfxFrameCount++;
    }

    private AudioSource GetFreeSFXSource()
    {
        if (sfxFreePool.Count > 0)
            return sfxFreePool.Dequeue();

        // Create new if under max
        int totalCount = sfxFreePool.Count + sfxActiveList.Count;
        if (totalCount < sfxPoolMax)
        {
            var src = CreateSFXSource();
            return src;
        }

        // Pool exhausted — steal the oldest active source
        if (sfxActiveList.Count > 0)
        {
            var src = sfxActiveList[0];
            src.Stop();
            src.clip = null;
            sfxActiveList.RemoveAt(0);
            return src;
        }

        return null;
    }

    private void ReturnSFXSource(AudioSource src)
    {
        if (src == null) return;
        src.clip = null;
        src.Stop();
        if (!sfxFreePool.Contains(src))
            sfxFreePool.Enqueue(src);
    }

    private AudioSource CreateSFXSource()
    {
        var go = new GameObject("SFX_Source");
        go.transform.SetParent(sfxPoolRoot.transform);
        var src = go.AddComponent<AudioSource>();
        src.loop = false;
        src.playOnAwake = false;

        if (mixer != null)
        {
            var sfxGroup = mixer.FindMatchingGroups("SFX");
            if (sfxGroup != null && sfxGroup.Length > 0)
                src.outputAudioMixerGroup = sfxGroup[0];
        }

        return src;
    }

    #endregion

    #region Internal — Voice

    private void PlayVoiceInternal(AudioClip clip, float volume)
    {
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.volume = GetEffectiveVolume(AudioChannel.Voice, volume);
        voiceSource.Play();
    }

    #endregion

    #region Internal — Volume

    private float GetEffectiveVolume(AudioChannel channel, float baseVolume)
    {
        float masterVol = channelMuted[(int)AudioChannel.Master] ? 0f : channelVolume[(int)AudioChannel.Master];
        float chanVol = channelMuted[(int)channel] ? 0f : channelVolume[(int)channel];
        return masterVol * chanVol * baseVolume;
    }

    private void ApplyVolume(AudioChannel channel)
    {
        if (mixer != null)
        {
            string param = channel switch
            {
                AudioChannel.Master => MIXER_PARAM_MASTER,
                AudioChannel.BGM => MIXER_PARAM_BGM,
                AudioChannel.SFX => MIXER_PARAM_SFX,
                AudioChannel.Voice => MIXER_PARAM_VOICE,
                _ => null
            };

            if (param != null)
            {
                float db = LinearToDecibel(channelMuted[(int)channel] ? 0f : channelVolume[(int)channel]);
                mixer.SetFloat(param, db);
            }
        }
        else
        {
            // Direct volume control (no mixer)
            if (channel == AudioChannel.BGM)
                bgmSource.volume = GetEffectiveVolume(AudioChannel.BGM, 1f);
            if (channel == AudioChannel.Voice)
                voiceSource.volume = GetEffectiveVolume(AudioChannel.Voice, 1f);
        }
    }

    private void ApplyAllVolumes()
    {
        ApplyVolume(AudioChannel.Master);
        ApplyVolume(AudioChannel.BGM);
        ApplyVolume(AudioChannel.SFX);
        ApplyVolume(AudioChannel.Voice);
    }

    private static float LinearToDecibel(float linear)
    {
        return linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear);
    }

    #endregion

    #region Internal — Clip Loading

    private AudioClip GetOrLoadClip(string path, int layer)
    {
        if (string.IsNullOrEmpty(path))
        {
            Log.w("GetOrLoadClip: path is null or empty", "AudioManager");
            return null;
        }

        if (clipCache.TryGetValue(path, out var cached))
            return cached;

        AudioClip clip = LoadFromAssetBundle(path, layer);
        if (clip == null)
        {
            clip = Resources.Load<AudioClip>(path);
        }

        if (clip != null)
        {
            // Evict oldest half if cache exceeds limit
            if (clipCache.Count >= clipCacheMax)
                EvictCache();
            clipCache[path] = clip;
        }
        else
        {
            Log.w($"AudioClip not found: {path}", "AudioManager");
        }

        return clip;
    }

    private void EvictCache()
    {
        int removeCount = clipCacheMax / 2;
        int removed = 0;
        var keysToRemove = new List<string>();
        foreach (var kvp in clipCache)
        {
            if (removed >= removeCount) break;
            keysToRemove.Add(kvp.Key);
            removed++;
        }

        foreach (var key in keysToRemove)
            clipCache.Remove(key);

        Log.d($"Audio clip cache evicted {keysToRemove.Count} entries (max={clipCacheMax})", "AudioManager");
    }

    private AudioClip LoadFromAssetBundle(string path, int layer)
    {
#if UNITY_EDITOR
        // Editor: skip AssetBundle loading. ConfigManager + AssetDatabase provide assets directly.
        // Avoids repeated "AssetBundleManager not initialized" debug logs.
        return null;
#else
        try
        {
            string prefix = layer == 2 ? AB_LAYER_PREFIX_HOTFIX : AB_LAYER_PREFIX_BASE;
            string bundleName = $"{prefix}_{AB_AUDIO_RES_TYPE}.ab";
            string assetName = Path.GetFileNameWithoutExtension(path);

            var clip = AssetBundleManager.Instance.LoadAsset<AudioClip>(bundleName, assetName);
            if (clip != null)
            {
                Log.d($"Loaded audio from AB: {assetName} [{bundleName}]", "AudioManager");
                return clip;
            }
        }
        catch (Exception e)
        {
            Log.w($"Audio AB load failed for {path}: {e.Message}", "AudioManager");
        }

        return null;
#endif
    }

    #endregion

    #region Internal — Config

    private bool TryGetConfig(int configId, out AudioConfig config)
    {
        if (ConfigMap == null)
        {
            Log.w($"TryGetConfig: ConfigMap not initialized (AudioProxy not loaded?)", "AudioManager");
            config = null;
            return false;
        }

        if (ConfigMap.TryGetValue(configId, out config))
            return true;

        Log.w($"AudioConfig not found: id={configId}", "AudioManager");
        return false;
    }

    #endregion

    #region Internal — Persistence

    private void LoadVolumeSettings()
    {
        channelVolume[(int)AudioChannel.Master] = PlayerPrefs.GetFloat(PlayerPrefsConst.AudioMasterVolume, 1f);
        channelVolume[(int)AudioChannel.BGM]    = PlayerPrefs.GetFloat(PlayerPrefsConst.AudioBGMVolume, 1f);
        channelVolume[(int)AudioChannel.SFX]    = PlayerPrefs.GetFloat(PlayerPrefsConst.AudioSFXVolume, 1f);
        channelVolume[(int)AudioChannel.Voice]  = PlayerPrefs.GetFloat(PlayerPrefsConst.AudioVoiceVolume, 1f);

        channelMuted[(int)AudioChannel.Master] = PlayerPrefs.GetInt(PlayerPrefsConst.AudioMasterMute, 0) == 1;
        channelMuted[(int)AudioChannel.BGM]    = PlayerPrefs.GetInt(PlayerPrefsConst.AudioBGMMute, 0) == 1;
        channelMuted[(int)AudioChannel.SFX]    = PlayerPrefs.GetInt(PlayerPrefsConst.AudioSFXMute, 0) == 1;
        channelMuted[(int)AudioChannel.Voice]  = PlayerPrefs.GetInt(PlayerPrefsConst.AudioVoiceMute, 0) == 1;

        // Migrate from legacy keys (old AudioManager format)
        MigrateLegacyVolume();
    }

    private void MigrateLegacyVolume()
    {
        if (PlayerPrefs.HasKey("audio_bgm_vol"))
        {
            channelVolume[(int)AudioChannel.BGM] = PlayerPrefs.GetFloat("audio_bgm_vol", 1f);
            channelMuted[(int)AudioChannel.BGM]  = PlayerPrefs.GetInt("audio_bgm_mute", 0) == 1;

            SetVolume(AudioChannel.BGM, channelVolume[(int)AudioChannel.BGM]);
            Mute(AudioChannel.BGM, channelMuted[(int)AudioChannel.BGM]);

            PlayerPrefs.DeleteKey("audio_bgm_vol");
            PlayerPrefs.DeleteKey("audio_bgm_mute");
            PlayerPrefs.DeleteKey("audio_sfx_vol");
            PlayerPrefs.DeleteKey("audio_sfx_mute");
            PlayerPrefs.Save();
            Log.d("Migrated legacy audio volume settings", "AudioManager");
        }
    }

    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(PlayerPrefsConst.AudioMasterVolume, channelVolume[(int)AudioChannel.Master]);
        PlayerPrefs.SetFloat(PlayerPrefsConst.AudioBGMVolume,    channelVolume[(int)AudioChannel.BGM]);
        PlayerPrefs.SetFloat(PlayerPrefsConst.AudioSFXVolume,    channelVolume[(int)AudioChannel.SFX]);
        PlayerPrefs.SetFloat(PlayerPrefsConst.AudioVoiceVolume,  channelVolume[(int)AudioChannel.Voice]);

        PlayerPrefs.SetInt(PlayerPrefsConst.AudioMasterMute, channelMuted[(int)AudioChannel.Master] ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsConst.AudioBGMMute,    channelMuted[(int)AudioChannel.BGM] ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsConst.AudioSFXMute,    channelMuted[(int)AudioChannel.SFX] ? 1 : 0);
        PlayerPrefs.SetInt(PlayerPrefsConst.AudioVoiceMute,  channelMuted[(int)AudioChannel.Voice] ? 1 : 0);

        PlayerPrefs.Save();
    }

    #endregion
}
