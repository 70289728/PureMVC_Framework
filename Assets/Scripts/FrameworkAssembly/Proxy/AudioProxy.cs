using System.Collections.Generic;

/// <summary>
/// PureMVC Proxy for audio system. Wraps AudioManager and provides notification-based control.
///
/// Usage:
///   Facade.RegisterProxy(new AudioProxy());
///   AudioProxy proxy = Facade.RetrieveProxy(AudioProxy.NAME) as AudioProxy;
///   proxy.PlayBGM(1); // or proxy.PlayBGM("bgm_main");
///   proxy.PlaySFX("sfx_click");
///   proxy.SetVolume(AudioChannel.BGM, 0.8f);
///
/// Notifications sent:
///   AUDIO_BGM_CHANGED    — body=AudioBGMChangedBody { configId, name }
///   AUDIO_VOLUME_CHANGED — body=AudioVolumeChangedBody { channel, volume }
///   AUDIO_MUTE_CHANGED   — body=AudioMuteChangedBody { channel, muted }
///   AUDIO_CONFIG_LOADED  — body=null (signal that config is ready)
/// </summary>
public class AudioProxy : ProxyBase
{
    public new const string NAME = "AudioProxy";

    private AudioManager manager;
    private bool configLoaded = false;

    public AudioProxy() : base(NAME)
    {
    }

    #region Registration & Config

    public override void OnRegister()
    {
        base.OnRegister();
        manager = AudioManager.Instance;

        // Hook AudioManager events → PureMVC notifications
        manager.OnBGMChanged += HandleBGMChanged;
        manager.OnVolumeChanged += HandleVolumeChanged;
        manager.OnMuteChanged += HandleMuteChanged;
    }

    public override void OnRemove()
    {
        if (manager != null)
        {
            manager.OnBGMChanged -= HandleBGMChanged;
            manager.OnVolumeChanged -= HandleVolumeChanged;
            manager.OnMuteChanged -= HandleMuteChanged;
        }
        base.OnRemove();
    }

    /// <summary>
    /// Load audio config table via ConfigManager and register with AudioManager.
    /// Call once at startup, after ConfigManager is ready.
    /// </summary>
    public void LoadConfig()
    {
        ConfigManager.Load<AudioConfig>();
        var configs = ConfigManager.GetAll<AudioConfig>();
        if (configs == null || configs.Count == 0)
        {
            Log.w("AudioProxy: no audio configs loaded", "AudioProxy");
            return;
        }

        var configMap = new Dictionary<int, AudioConfig>();
        var nameMap = new Dictionary<string, int>();

        foreach (var cfg in configs)
        {
            if (configMap.ContainsKey(cfg.id))
            {
                Log.w($"AudioProxy: duplicate config id {cfg.id}, skipping", "AudioProxy");
                continue;
            }
            configMap[cfg.id] = cfg;

            if (!string.IsNullOrEmpty(cfg.name))
            {
                if (nameMap.ContainsKey(cfg.name))
                    Log.w($"AudioProxy: duplicate config name '{cfg.name}', overwriting", "AudioProxy");
                nameMap[cfg.name] = cfg.id;
            }
        }

        manager.ConfigMap = configMap;
        manager.NameToIdMap = nameMap;
        configLoaded = true;

        SendNotification(NotificationConst.AUDIO_CONFIG_LOADED);
        Log.d($"AudioProxy: loaded {configs.Count} audio configs", "AudioProxy");
    }

    #endregion

    #region Playback — Config-driven

    /// <summary>Play BGM by config ID.</summary>
    public void PlayBGM(int configId, float fadeDuration = -1f)
    {
        EnsureConfigLoaded();
        manager.PlayBGM(configId, fadeDuration);
    }

    /// <summary>Play BGM by config name.</summary>
    public void PlayBGM(string name, float fadeDuration = -1f)
    {
        EnsureConfigLoaded();
        manager.PlayBGMByName(name, fadeDuration);
    }

    /// <summary>Play SFX by config ID.</summary>
    public void PlaySFX(int configId)
    {
        EnsureConfigLoaded();
        manager.PlaySFX(configId);
    }

    /// <summary>Play SFX by config name.</summary>
    public void PlaySFX(string name)
    {
        EnsureConfigLoaded();
        manager.PlaySFXByName(name);
    }

    /// <summary>Play Voice by config ID.</summary>
    public void PlayVoice(int configId)
    {
        EnsureConfigLoaded();
        manager.PlayVoice(configId);
    }

    /// <summary>Play Voice by config name.</summary>
    public void PlayVoice(string name)
    {
        EnsureConfigLoaded();
        manager.PlayVoiceByName(name);
    }

    #endregion

    #region Playback Control

    /// <summary>Stop current BGM.</summary>
    public void StopBGM() => manager.StopBGM();

    /// <summary>Pause BGM.</summary>
    public void PauseBGM() => manager.PauseBGM();

    /// <summary>Resume BGM.</summary>
    public void ResumeBGM() => manager.ResumeBGM();

    /// <summary>Stop all active SFX.</summary>
    public void StopAllSFX() => manager.StopAllSFX();

    /// <summary>Stop current voice.</summary>
    public void StopVoice() => manager.StopVoice();

    /// <summary>Get current BGM config ID. -1 if none.</summary>
    public int CurrentBGMId => manager.CurrentBGMId;

    /// <summary>Whether BGM is playing.</summary>
    public bool IsBGMPlaying => manager.IsBGMPlaying;

    #endregion

    #region Volume Control

    /// <summary>Set volume for a channel (0~1).</summary>
    public void SetVolume(AudioManager.AudioChannel channel, float volume)
    {
        manager.SetVolume(channel, volume);
    }

    /// <summary>Get current volume for a channel.</summary>
    public float GetVolume(AudioManager.AudioChannel channel)
    {
        return manager.GetVolume(channel);
    }

    /// <summary>Mute/unmute a channel.</summary>
    public void Mute(AudioManager.AudioChannel channel, bool mute)
    {
        manager.Mute(channel, mute);
    }

    /// <summary>Check if a channel is muted.</summary>
    public bool IsMuted(AudioManager.AudioChannel channel)
    {
        return manager.IsMuted(channel);
    }

    #endregion

    #region Config Queries

    /// <summary>Get audio config by ID.</summary>
    public AudioConfig GetConfig(int configId)
    {
        EnsureConfigLoaded();
        if (manager.ConfigMap != null && manager.ConfigMap.TryGetValue(configId, out var cfg))
            return cfg;
        return null;
    }

    /// <summary>Get audio config by name.</summary>
    public AudioConfig GetConfig(string name)
    {
        EnsureConfigLoaded();
        if (manager.NameToIdMap != null && manager.NameToIdMap.TryGetValue(name, out int id))
            return GetConfig(id);
        return null;
    }

    /// <summary>Whether config has been loaded.</summary>
    public bool IsConfigLoaded => configLoaded;

    #endregion

    #region Preload

    /// <summary>Preload audio clips for a set of config IDs.</summary>
    public void Preload(params int[] configIds)
    {
        EnsureConfigLoaded();
        manager.PreloadConfigs(configIds);
    }

    #endregion

    #region Internal — Event Handlers

    private void HandleBGMChanged(int configId)
    {
        string name = null;
        if (configId >= 0)
        {
            var cfg = GetConfig(configId);
            if (cfg != null) name = cfg.name;
        }
        SendNotification(NotificationConst.AUDIO_BGM_CHANGED, new AudioBGMChangedBody
        {
            configId = configId,
            name = name ?? ""
        });
    }

    private void HandleVolumeChanged(AudioManager.AudioChannel channel, float volume)
    {
        SendNotification(NotificationConst.AUDIO_VOLUME_CHANGED, new AudioVolumeChangedBody
        {
            channel = (int)channel,
            volume = volume
        });
    }

    private void HandleMuteChanged(AudioManager.AudioChannel channel, bool muted)
    {
        SendNotification(NotificationConst.AUDIO_MUTE_CHANGED, new AudioMuteChangedBody
        {
            channel = (int)channel,
            muted = muted
        });
    }

    #endregion

    #region Internal — Helpers

    private void EnsureConfigLoaded()
    {
        if (!configLoaded)
        {
            Log.w("AudioProxy: config not loaded. Call LoadConfig() first.", "AudioProxy");
        }
    }

    #endregion
}
