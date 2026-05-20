using UnityEngine;
using UnityEngine.UI;
using PureMVC.Interfaces;

public class UICreatePlayerMediator : UIMediatorBase
{
    public new const string NAME = HotUpdateUIConst.UICreatePlayer;

    #region UI Components
    [SerializeField] private InputField nameInput;
    [SerializeField] private Button malBtn;
    [SerializeField] private Button femaleBtn;
    [SerializeField] private Button createBtn;
    #endregion

    private int selectedGender = 0; // 0 for Male, 1 for Female

    public UICreatePlayerMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    #region PureMVC Lifecycle
    public override string[] ListNotificationInterests()
    {
        return new string[]
        {
            NotificationConst.CREATE_PLAYER_SUCCESS,
            NotificationConst.CREATE_PLAYER_FAILED,
        };
    }

    public override void HandleNotification(INotification notification)
    {
        string name = notification.Name;
        if (name == NotificationConst.CREATE_PLAYER_SUCCESS)
            OnCreatePlayerSuccess();
        else if (name == NotificationConst.CREATE_PLAYER_FAILED)
            OnCreatePlayerFailed(notification.Body as string);
    }
    #endregion

    #region UI Init
    protected override void InitUIComponents()
    {
        var allInputFieldBinds = viewTrans.GetComponentsInChildren<InputFieldBind>(true);
        foreach (var bind in allInputFieldBinds)
        {
            switch (bind.gameObject.name)
            {
                case "NameInput": nameInput = bind.Component; break;
            }
        }

        var allButtonBinds = viewTrans.GetComponentsInChildren<ButtonBind>(true);
        foreach (var bind in allButtonBinds)
        {
            switch (bind.gameObject.name)
            {
                case "MalBtn": malBtn = bind.Component; break;
                case "FemaleBtn": femaleBtn = bind.Component; break;
                case "CreateBtn": createBtn = bind.Component; break;
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
        if (malBtn != null)
        {
            malBtn.onClick.AddListener(OnMalBtnClick);
        }
        if (femaleBtn != null)
        {
            femaleBtn.onClick.AddListener(OnFemaleBtnClick);
        }
        if (createBtn != null)
        {
            createBtn.onClick.AddListener(OnCreateBtnClick);
        }

        // Set default gender to Male
        if (malBtn != null)
        {
            SelectGender(0);
        }
    }
    #endregion

    #region UI Events
    private void OnMalBtnClick()
    {
        SelectGender(0);
    }

    private void OnFemaleBtnClick()
    {
        SelectGender(1);
    }

    private void SelectGender(int gender)
    {
        selectedGender = gender;
        // Update button visual states
        if (malBtn != null)
        {
            var colors = malBtn.colors;
            colors.normalColor = gender == 0 ? Color.green : Color.white;
            malBtn.colors = colors;
        }
        if (femaleBtn != null)
        {
            var colors = femaleBtn.colors;
            colors.normalColor = gender == 1 ? Color.green : Color.white;
            femaleBtn.colors = colors;
        }
    }

    private void OnCreateBtnClick()
    {
        string playerName = nameInput != null ? nameInput.text : string.Empty;
        if (string.IsNullOrEmpty(playerName))
        {
            SendNotification(NotificationConst.SHOW_TIP, "Player name cannot be empty.");
            return;
        }

        createBtn.interactable = false;
        var body = new CreatePlayerBody { PlayerName = playerName, Gender = selectedGender, Job = 0 };
        SendNotification(NotificationConst.CREATE_PLAYER, body);
    }
    #endregion

    #region Notification Handlers
    private void OnCreatePlayerSuccess()
    {
        Log.d("Create player success, opening main UI.", NAME);
        UIManager.Instance.CloseUI(NAME);
        UIManager.Instance.OpenUI<UIMainMediator>(HotUpdateUIConst.UIMain);
    }

    private void OnCreatePlayerFailed(string reason)
    {
        Log.w($"Create player failed: {reason}", NAME);
        if (createBtn != null) createBtn.interactable = true;
        SendNotification(NotificationConst.SHOW_TIP, reason);
    }
    #endregion
}