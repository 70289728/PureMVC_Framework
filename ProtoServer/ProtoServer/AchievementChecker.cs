using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Server-side achievement progress checker.
/// Loads achievement config from JSON, checks progress on trigger events,
/// and updates PlayerData accordingly.
/// </summary>
public static class AchievementChecker
{
    private static List<AchievementServerConfig> _configs;
    private static readonly object _lock = new object();

    public static List<AchievementServerConfig> Configs
    {
        get
        {
            if (_configs == null)
                LoadConfig();
            return _configs;
        }
    }

    private static void LoadConfig()
    {
        lock (_lock)
        {
            if (_configs != null) return;
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
                string configPath = Path.Combine(projectRoot, "DesignConfig", "Json", "Achievement.json");
                string json = File.ReadAllText(configPath);
                var wrapper = JsonConvert.DeserializeObject<AchievementConfigWrapper>(json);
                _configs = wrapper?.items ?? new List<AchievementServerConfig>();
                Console.WriteLine($"[AchievementChecker] Loaded {_configs.Count} achievement configs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AchievementChecker] Failed to load config: {ex.Message}");
                _configs = new List<AchievementServerConfig>();
            }
        }
    }

    /// <summary>
    /// Reload config (useful for hot-reload during development).
    /// </summary>
    public static void ReloadConfig()
    {
        lock (_lock) { _configs = null; }
    }

    /// <summary>
    /// Initialize progress for all achievements for a new/returning player.
    /// Ensures AchievementProgress has entries for every config achievement.
    /// </summary>
    public static void InitProgress(PlayerData pd)
    {
        foreach (var cfg in Configs)
        {
            if (!pd.AchievementProgress.ContainsKey(cfg.id))
                pd.AchievementProgress[cfg.id] = 0;
        }
    }

    /// <summary>
    /// Check achievements triggered by a specific event.
    /// Returns list of (id, newProgress, newStatus) for achievements that changed.
    /// Caller should push updates to client.
    /// </summary>
    public static List<(int id, int progress, int target, int status)> Check(PlayerData pd, string triggerEvent, int value)
    {
        var changed = new List<(int, int, int, int)>();

        var matchingConfigs = Configs.Where(c => c.triggerEvent == triggerEvent).ToList();
        foreach (var cfg in matchingConfigs)
        {
            // Skip already claimed
            if (pd.ClaimedAchievements.Contains(cfg.id))
                continue;

            int oldProgress = pd.AchievementProgress.TryGetValue(cfg.id, out int p) ? p : 0;

            // For Threshold type, the value is the current state; for Cumulative, add to progress
            int newProgress;
            if (cfg.type == 2) // Threshold
                newProgress = value;
            else
                newProgress = oldProgress + value;

            // Skip if no change
            if (newProgress <= oldProgress) continue;

            if (newProgress > cfg.targetNum)
                newProgress = cfg.targetNum;

            pd.AchievementProgress[cfg.id] = newProgress;

            // Check if just completed
            if (newProgress >= cfg.targetNum && oldProgress < cfg.targetNum)
            {
                if (!pd.UnlockedAchievements.Contains(cfg.id))
                    pd.UnlockedAchievements.Add(cfg.id);
                changed.Add((cfg.id, newProgress, cfg.targetNum, 1)); // status=1: completed (unclaimed)
            }
            else
            {
                changed.Add((cfg.id, newProgress, cfg.targetNum, 0)); // status=0: in progress
            }
        }

        return changed;
    }

    /// <summary>
    /// Get full achievement info list for pushing to client.
    /// </summary>
    public static List<AchievementInfo> BuildInfoList(PlayerData pd)
    {
        var list = new List<AchievementInfo>();
        foreach (var cfg in Configs)
        {
            int progress = pd.AchievementProgress.TryGetValue(cfg.id, out int p) ? p : 0;
            int status = 0;
            if (pd.ClaimedAchievements.Contains(cfg.id))
                status = 2;
            else if (pd.UnlockedAchievements.Contains(cfg.id))
                status = 1;

            list.Add(new AchievementInfo
            {
                Id = cfg.id,
                Progress = progress,
                Target = cfg.targetNum,
                Status = status
            });
        }
        return list;
    }
}

/// <summary>
/// Server-side achievement config model (mirrors client AchievementConfig).
/// Loaded from DesignConfig/Json/Achievement.json.
/// </summary>
[Serializable]
public class AchievementServerConfig
{
    public int id;
    public string name;
    public string desc;
    public int category;
    public int type;
    public string triggerEvent;
    public int targetNum;
    public int rewardType;
    public int rewardNum;
    public int nextId;
}

[Serializable]
internal class AchievementConfigWrapper
{
    public List<AchievementServerConfig> items;
}
