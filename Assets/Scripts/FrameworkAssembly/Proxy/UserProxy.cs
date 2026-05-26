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

    private void InitLevelConfigs()
    {
        for (int i = 1; i <= 100; i++)
        {
            _levelCfgs.Add(new LevelConfig
            {
                Level = i,
                NeedExp = i * 100,
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