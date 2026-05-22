using System.Collections.Generic;

/// <summary>
/// Achievement system proxy (HotUpdateAssembly).
/// Achievement data is pushed by server after login, and updated via progress/unlock pushes.
/// Registered at startup by RegisterHotUpdateProxiesCommand in HotUpdateStartupMacroCommand.
/// </summary>
public class AchievementProxy : ProxyBase
{
    public new const string NAME = "AchievementProxy";

    /// <summary> achievementId → AchievementInfo (synced from server) </summary>
    public Dictionary<int, AchievementInfo> AchievementMap { get; private set; } = new Dictionary<int, AchievementInfo>();

    /// <summary> Unlocked achievement IDs (completed but not yet claimed) </summary>
    public List<int> UnlockedIds { get; private set; } = new List<int>();

    /// <summary> Cached config list for local queries </summary>
    private List<AchievementConfig> _configs;

    public AchievementProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Register(MessageConst.ACHIEVEMENT_LIST_S2C, OnAchievementListS2C);
        disp.Register(MessageConst.ACHIEVEMENT_PROGRESS_S2C, OnAchievementProgressS2C);
        disp.Register(MessageConst.ACHIEVEMENT_UNLOCK_S2C, OnAchievementUnlockS2C);
        disp.Register(MessageConst.ACHIEVEMENT_CLAIM_S2C, OnAchievementClaimS2C);

        LoadConfig();
    }

    public override void OnRemove()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Unregister(MessageConst.ACHIEVEMENT_LIST_S2C, OnAchievementListS2C);
        disp.Unregister(MessageConst.ACHIEVEMENT_PROGRESS_S2C, OnAchievementProgressS2C);
        disp.Unregister(MessageConst.ACHIEVEMENT_UNLOCK_S2C, OnAchievementUnlockS2C);
        disp.Unregister(MessageConst.ACHIEVEMENT_CLAIM_S2C, OnAchievementClaimS2C);
    }

    #region Config

    private void LoadConfig()
    {
        ConfigManager.Load<AchievementConfig>();
        _configs = ConfigManager.GetAll<AchievementConfig>();
        Log.d($"Loaded {_configs?.Count ?? 0} achievement configs", NAME);
    }

    public AchievementConfig GetConfig(int id)
    {
        if (_configs == null) return null;
        return _configs.Find(c => c.id == id);
    }

    public List<AchievementConfig> GetAllConfigs()
    {
        return _configs;
    }

    #endregion

    #region Send

    public void SendClaim(int achievementId)
    {
        NetworkMessageHelper.SendAchievementClaim(achievementId);
    }

    #endregion

    #region Callbacks

    private void OnAchievementListS2C(byte[] body)
    {
        var list = NetworkMessageHelper.ParseAchievementListS2C(body);
        AchievementMap.Clear();
        UnlockedIds.Clear();

        foreach (var info in list.Achievements)
        {
            AchievementMap[info.Id] = info;
            if (info.Status == 1)
                UnlockedIds.Add(info.Id);
        }

        Log.d($"Achievement list received: {AchievementMap.Count} total, {UnlockedIds.Count} unlocked", NAME);
        SendNotification(NotificationConst.UPDATE_ACHIEVEMENT);
    }

    private void OnAchievementProgressS2C(byte[] body)
    {
        var update = NetworkMessageHelper.ParseAchievementProgressS2C(body);
        AchievementMap[update.Id] = new AchievementInfo
        {
            Id = update.Id,
            Progress = update.Progress,
            Target = update.Target,
            Status = update.Status
        };

        if (update.Status == 1 && !UnlockedIds.Contains(update.Id))
            UnlockedIds.Add(update.Id);

        Log.d($"Achievement progress: id={update.Id}, {update.Progress}/{update.Target}, status={update.Status}", NAME);
        SendNotification(NotificationConst.UPDATE_ACHIEVEMENT);
    }

    private void OnAchievementUnlockS2C(byte[] body)
    {
        var notify = NetworkMessageHelper.ParseAchievementUnlockS2C(body);

        if (AchievementMap.TryGetValue(notify.Id, out var info))
            info.Status = 1;

        if (!UnlockedIds.Contains(notify.Id))
            UnlockedIds.Add(notify.Id);

        Log.d($"Achievement unlocked: id={notify.Id}", NAME);
        SendNotification(NotificationConst.ACHIEVEMENT_UNLOCK, notify.Id);
    }

    private void OnAchievementClaimS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseAchievementClaimS2C(body);
        Log.d($"Achievement claim result: id={resp.Id}, {(resp.Rst.Result ? "success" : "failed")}", NAME);

        if (resp.Rst.Result)
        {
            if (AchievementMap.TryGetValue(resp.Id, out var info))
                info.Status = 2;
            UnlockedIds.Remove(resp.Id);

            if (resp.RewardType == 1)
            {
                var userProxy = Facade.RetrieveProxy(UserProxy.NAME) as UserProxy;
                userProxy?.AddGold(resp.RewardCount);
            }
            else if (resp.RewardType == 2)
            {
                var userProxy = Facade.RetrieveProxy(UserProxy.NAME) as UserProxy;
                userProxy?.AddDiamond(resp.RewardCount);
            }

            SendNotification(NotificationConst.UPDATE_ACHIEVEMENT);
        }

        SendNotification(NotificationConst.SHOW_TIP, $"{(resp.Rst.Result ? "Claimed!" : "Claim failed")}");
    }

    #endregion
}
