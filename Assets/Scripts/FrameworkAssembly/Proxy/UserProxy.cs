using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UserVO
{
    public string Account = "admin";
    public string NickName = "Player";
    public int Level = 1;
    public int Exp = 0;
    public int Gold = 1000;
    public int Diamond = 100;
    public int Hp = 100;
    public int MaxHp = 100;
    public int Atk = 10;
    public float SkillCD1 = 0;
    public float SkillCD2 = 0;
}
public class UserProxy : ProxyBase
{
    public new const string NAME = "UserProxy";
    public UserVO userData { get; private set; }
    private List<LevelConfig> _levelCfgs = new List<LevelConfig>();

    /// <summary>
    /// Extension data for hot-update modules to attach custom state without subclassing.
    /// </summary>
    public readonly Dictionary<string, object> extData = new Dictionary<string, object>();

    public T GetExt<T>(string key, T fallback = default)
    {
        if (extData.TryGetValue(key, out object v) && v is T t) return t;
        return fallback;
    }

    public void SetExt(string key, object value)
    {
        extData[key] = value;
    }

    public UserProxy() : base(NAME, null)
    {
        userData = SaveManager.Load<UserVO>(SaveManager.Keys.UserData);
        InitLevelConfigs();
    }

    public override void OnRegister()
    {
        base.OnRegister();
        NetworkManager.Instance.Dispatcher.Register(MessageConst.LOGIN_S2C, OnLoginS2C);
        NetworkManager.Instance.Dispatcher.Register(MessageConst.REGISTER_S2C, OnRegisterS2C);
        NetworkManager.Instance.Dispatcher.Register(MessageConst.CREATE_PLAYER_S2C, OnCreatePlayerS2C);
    }

    public override void OnRemove()
    {
        NetworkManager.Instance.Dispatcher.Unregister(MessageConst.LOGIN_S2C, OnLoginS2C);
        NetworkManager.Instance.Dispatcher.Unregister(MessageConst.REGISTER_S2C, OnRegisterS2C);
        NetworkManager.Instance.Dispatcher.Unregister(MessageConst.CREATE_PLAYER_S2C, OnCreatePlayerS2C);
        base.OnRemove();
    }

    private void OnLoginS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseLoginS2C(body);
        if (resp.Rst.Result)
        {
            NetworkManager.CurrentAccountId = resp.AccountId;

            // Complete reconnect handshake if this login was triggered by reconnect flow.
            // IMPORTANT: do NOT fire LOGIN_SUCCESS/LOGIN_FAILED for reconnects —
            // those trigger LoginSuccessCommand which opens UIMain/UILogin and would break
            // the current UI state.
            var netProxy = GetProxy<NetworkProxy>(NetworkProxy.NAME);
            if (netProxy != null && netProxy.IsReconnecting)
            {
                Log.d($"Reconnect login success for account {resp.AccountId}. Completing handshake.", NAME);
                netProxy.ResetReconnectState();
                NetworkManager.Instance.FlushPendingMessages();
                NetworkMessageHelper.SendBagList();
                Facade.SendNotification(NetworkNotificationConst.NETWORK_RECONNECTED);
                return;
            }

            // Sync player data from server
            if (resp.PlayerData != null && !string.IsNullOrEmpty(resp.PlayerData.PlayerName))
            {
                userData.NickName = resp.PlayerData.PlayerName;
                userData.Level = resp.PlayerData.Level;
                userData.Exp = resp.PlayerData.Exp;
                userData.Gold = resp.PlayerData.Gold;
                userData.Diamond = resp.PlayerData.Diamond;
                SendNotification(NotificationConst.LOGIN_SUCCESS, resp);
            }
            else
            {
                // No character yet, send login success but UI layer will open create player
                SendNotification(NotificationConst.LOGIN_SUCCESS, resp);
            }
        }
        else
        {
            // Reconnect login failed — reset state and discard pending messages
            var netProxy = GetProxy<NetworkProxy>(NetworkProxy.NAME);
            if (netProxy != null && netProxy.IsReconnecting)
            {
                Log.w($"Reconnect login failed: errCode={resp.Rst.ErrCode}. Clearing pending messages.", NAME);
                netProxy.ResetReconnectState();
                NetworkManager.Instance.ClearPendingMessages();
                return;
            }
            SendNotification(NotificationConst.LOGIN_FAILED, $"Login failed, errCode={resp.Rst.ErrCode}");
        }
    }

    private void OnRegisterS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseRegisterS2C(body);
        if (resp.Rst.Result)
        {
            SendNotification(NotificationConst.REGISTER_SUCCESS);
        }
        else
        {
            SendNotification(NotificationConst.REGISTER_FAILED, $"Register failed, errCode={resp.Rst.ErrCode}");
        }
    }

    private void OnCreatePlayerS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseCreatePlayerS2C(body);
        if (resp.Rst.Result)
        {
            // Reset to default level and exp on character creation
            userData.Level = resp.Level;
            userData.Exp = resp.Exp;
            SendNotification(NotificationConst.CREATE_PLAYER_SUCCESS, resp);
        }
        else
        {
            SendNotification(NotificationConst.CREATE_PLAYER_FAILED, $"Create player failed, errCode={resp.Rst.ErrCode}");
        }
    }

    /// <summary>
    /// Level exp thresholds loaded from level.json config.
    /// TODO: Load via ConfigManager once Level type is accessible in FrameworkAssembly.
    /// Currently Level.cs lives in HotUpdateAssembly (export tool target), so we mirror
    /// the exp values here to keep level-up logic consistent with the Excel config.
    /// Combat stats (AddAtk, AddHp) are not in the Excel config and remain formula-based.
    /// </summary>
    private static readonly Dictionary<int, int> LevelExpThresholds = new Dictionary<int, int>
    {
        {1,100},{2,150},{3,250},{4,400},{5,650},{6,900},{7,1300},{8,1800},{9,2400},{10,3100},
        {11,4000},{12,5000},{13,6200},{14,7500},{15,9000},{16,10600},{17,12400},{18,14300},{19,16400},{20,18600},
        {21,21000},{22,23500},{23,26200},{24,29000},{25,32000},{26,35400},{27,38900},{28,42600},{29,46500},{30,50600},
        {31,55000},{32,59600},{33,64500},{34,69600},{35,75000},{36,80600},{37,86500},{38,92600},{39,99000},{40,105600},
        {41,112500},{42,119600},{43,127000},{44,134600},{45,142500},{46,150600},{47,159000},{48,167600},{49,176500},{50,185600},
        {51,195000},{52,204600},{53,214500},{54,224600},{55,235000},{56,245600},{57,256500},{58,267600},{59,279000},{60,290600},
        {61,302500},{62,314600},{63,327000},{64,339600},{65,352500},{66,365600},{67,379000},{68,392600},{69,406500},{70,420600},
        {71,435000},{72,449600},{73,464500},{74,479600},{75,495000},{76,510600},{77,526500},{78,542600},{79,559000},{80,575600},
        {81,592500},{82,609600},{83,627000},{84,644600},{85,662500},{86,680600},{87,699000},{88,717600},{89,736500},{90,755600},
        {91,775000},{92,794600},{93,814500},{94,834600},{95,855000},{96,875600},{97,896500},{98,917600},{99,939000},{100,960600}
    };

    private void InitLevelConfigs()
    {
        for (int i = 1; i <= 100; i++)
        {
            int expValue = LevelExpThresholds.TryGetValue(i, out int v) ? v : i * 100;
            _levelCfgs.Add(new LevelConfig
            {
                Level = i,
                NeedExp = expValue,
                AddAtk = i + 2,
                AddHp = i * 5 + 10
            });
        }
    }

    public void AddExp(int exp)
    {
        userData.Exp += exp;
        CheckLevelUp();
        SendNotification(NotificationConst.UPDATE_USER_INFO, userData);
    }

    public LevelConfig GetLevelConfig(int level)
    {
        return _levelCfgs.Find(c => c.Level == level);
    }

    private void CheckLevelUp()
    {
        var cfg = _levelCfgs.Find(c => c.Level == userData.Level);
        if (cfg == null) return;
        if (userData.Exp >= cfg.NeedExp)
        {
            userData.Exp -= cfg.NeedExp;
            userData.Level++;
            userData.Atk += cfg.AddAtk;
            userData.MaxHp += cfg.AddHp;
            userData.Hp = userData.MaxHp;
            SendNotification(NotificationConst.LEVEL_UP, userData.Level);
            CheckLevelUp();
        }
    }

    public void AddGold(int num)
    {
        SetGold(userData.Gold + num);
    }

    /// <summary>
    /// Set gold to an absolute value. Validates non-negative. Fires UPDATE_USER_INFO.
    /// </summary>
    public void SetGold(int value)
    {
        userData.Gold = Mathf.Max(0, value);
        SendNotification(NotificationConst.UPDATE_USER_INFO, userData);
    }

    public void AddDiamond(int num)
    {
        SetDiamond(userData.Diamond + num);
    }

    /// <summary>
    /// Set diamond to an absolute value. Validates non-negative. Fires UPDATE_USER_INFO.
    /// </summary>
    public void SetDiamond(int value)
    {
        userData.Diamond = Mathf.Max(0, value);
        SendNotification(NotificationConst.UPDATE_USER_INFO, userData);
    }

    public void Hurt(int damage)
    {
        userData.Hp = Mathf.Max(0, userData.Hp - damage);
        SendNotification(NotificationConst.UPDATE_USER_INFO, userData);
    }

    public void Save()
    {
        SaveManager.Save(SaveManager.Keys.UserData, userData);
    }
}

public class LevelConfig
{
    public int Level;
    public int NeedExp;
    public int AddAtk;
    public int AddHp;
}