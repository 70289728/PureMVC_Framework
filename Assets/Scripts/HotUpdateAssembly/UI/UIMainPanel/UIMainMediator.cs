using UnityEngine;
using UnityEngine.UI;
using PureMVC.Interfaces;

public class UIMainMediator : UIMediatorBase
{
    public new const string NAME = HotUpdateUIConst.UIMain;

    #region UI Components
    [SerializeField] private Image headImg;
    [SerializeField] private Text nameTxt;
    [SerializeField] private Text levelTxt;
    [SerializeField] private Slider expSlider;
    [SerializeField] private Button chatBtn;
    [SerializeField] private Text chatContentTxt;
    [SerializeField] private Button shopBtn;
    #endregion

    public UIMainMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    #region PureMVC Lifecycle
    public override string[] ListNotificationInterests()
    {
        return new string[]
        {
            NotificationConst.UPDATE_USER_INFO,
        };
    }

    public override void HandleNotification(INotification notification)
    {
        if (notification.Name == NotificationConst.UPDATE_USER_INFO)
            RefreshUserInfo(notification.Body as UserVO);
    }
    #endregion

    protected override void InitUIComponents()
    {
        headImg = viewTrans.GetComponentInChildren<ImageBind>(true).Component;
        var allTextBinds = viewTrans.GetComponentsInChildren<TextBind>(true);
        foreach (var bind in allTextBinds)
        {
            switch (bind.gameObject.name)
            {
                case "NameTxt": nameTxt = bind.Component; break;
                case "LevelTxt": levelTxt = bind.Component; break;
                case "ChatContentTxt": chatContentTxt = bind.Component; break;
            }
        }
        expSlider = viewTrans.GetComponentInChildren<SliderBind>(true).Component;
        var allButtonBinds = viewTrans.GetComponentsInChildren<ButtonBind>(true);
        foreach (var bind in allButtonBinds)
        {
            switch (bind.gameObject.name)
            {
                case "ChatBtn": chatBtn = bind.Component; break;
                case "ShopBtn": shopBtn = bind.Component; break;
            }
        }
        InitClickEvents();
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
    }

    protected override void UnRegisterUIEvents()
    {
        base.UnRegisterUIEvents();
    }

    private void InitClickEvents()
    {
        if (chatBtn != null)
        {
            chatBtn.onClick.AddListener(OnChatBtnClick);
        }
        if (shopBtn != null)
        {
            shopBtn.onClick.AddListener(OnShopBtnClick);
        }
    }

    public override void OnShow()
    {
        base.OnShow();
        // Refresh user info when panel opens
        var proxy = GetProxy<UserProxy>(UserProxy.NAME);
        if (proxy != null)
        {
            RefreshUserInfo(proxy.userData);
        }
    }

    private void OnChatBtnClick()
    {
    }

    private void RefreshUserInfo(UserVO data)
    {
        if (data == null) return;
        if (nameTxt != null) nameTxt.text = data.NickName;
        if (levelTxt != null) levelTxt.text = $"Lv.{data.Level}";
        if (expSlider != null)
        {
            var proxy = GetProxy<UserProxy>(UserProxy.NAME);
            if (proxy != null)
            {
                var cfg = proxy.GetLevelConfig(data.Level);
                expSlider.maxValue = cfg != null ? cfg.NeedExp : 1;
                expSlider.value = data.Exp;
            }
        }

        // Print current level max exp from real config table
        ConfigManager.Load<Level>();
        var levelCfg = ConfigManager.Get<Level>(c => c.level == data.Level);
        if (levelCfg != null)
        {
            Log.d($"Current level {data.Level} max exp: {levelCfg.expValue}", NAME);
        }
        else
        {
            Log.w($"Level config not found for level {data.Level}", NAME);
        }
    }

    private void OnShopBtnClick()
    {
        UIManager.Instance.OpenUI<UIMediatorBase>(UIConst.UIShop);
    }

    #region Update (Optional)
    // Uncomment if this UI needs Update functionality
    // protected override bool NeedsUpdate() => true;
    // protected override UpdateFrequency GetUpdateFrequency() => UpdateFrequency.Low;
    // protected override UpdateType[] GetUpdateTypes() => new UpdateType[] { UpdateType.Update };
    // protected override void OnUpdate(float deltaTime) { }
    // protected override void OnFixedUpdate(float fixedDeltaTime) { }
    // protected override void OnLateUpdate(float deltaTime) { }
    #endregion

}
