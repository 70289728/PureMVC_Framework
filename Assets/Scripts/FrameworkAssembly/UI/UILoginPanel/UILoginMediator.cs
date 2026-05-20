using UnityEngine;
using UnityEngine.UI;
using PureMVC.Interfaces;

public class UILoginMediator : UIMediatorBase
{
    public new const string NAME = UIConst.UILogin;

    #region UI Components
    [SerializeField] private InputField accountInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button loginBtn;
    [SerializeField] private Button registerBtn;
    #endregion

    public UILoginMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    #region PureMVC Lifecycle
    public override string[] ListNotificationInterests()
    {
        return new string[]
        {
            NotificationConst.LOGIN_SUCCESS,
            NotificationConst.LOGIN_FAILED,
            NotificationConst.REGISTER_SUCCESS,
            NotificationConst.REGISTER_FAILED,
        };
    }

    public override void HandleNotification(INotification notification)
    {
        string name = notification.Name;
        if (name == NotificationConst.LOGIN_SUCCESS)
            OnLoginSuccess(notification.Body as UserVO);
        else if (name == NotificationConst.LOGIN_FAILED)
            OnLoginFailed(notification.Body as string);
        else if (name == NotificationConst.REGISTER_SUCCESS)
            OnRegisterSuccess();
        else if (name == NotificationConst.REGISTER_FAILED)
            OnRegisterFailed(notification.Body as string);
    }
    #endregion

    #region UI Init
    public override void OnShow()
    {
        base.OnShow();
        if (accountInput != null) accountInput.text = PlayerPrefsManager.GetString(PlayerPrefsConst.LastAccount);
        if (passwordInput != null) passwordInput.text = PlayerPrefsManager.GetString(PlayerPrefsConst.LastPassword);
    }

    protected override void InitUIComponents()
    {
        var allInputFieldBinds = viewTrans.GetComponentsInChildren<InputFieldBind>(true);
        foreach (var bind in allInputFieldBinds)
        {
            switch (bind.gameObject.name)
            {
                case "AccountInput": accountInput = bind.Component; break;
                case "PasswordInput": passwordInput = bind.Component; break;
            }
        }

        var allButtonBinds = viewTrans.GetComponentsInChildren<ButtonBind>(true);
        foreach (var bind in allButtonBinds)
        {
            switch (bind.gameObject.name)
            {
                case "LoginBtn": loginBtn = bind.Component; break;
                case "RegisterBtn": registerBtn = bind.Component; break;
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
        if (loginBtn != null)
        {
            loginBtn.onClick.AddListener(OnLoginBtnClick);
        }
        if (registerBtn != null)
        {
            registerBtn.onClick.AddListener(OnRegisterBtnClick);
        }
    }
    #endregion

    #region UI Events
    private void OnLoginBtnClick()
    {
        string account = accountInput != null ? accountInput.text : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        if (string.IsNullOrEmpty(account))
        {
            SendNotification(NotificationConst.SHOW_TIP, "Account cannot be empty.");
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            SendNotification(NotificationConst.SHOW_TIP, "Password cannot be empty.");
            return;
        }
        if (!int.TryParse(account, out int accountId))
        {
            SendNotification(NotificationConst.SHOW_TIP, "Account must be a number.");
            return;
        }
        SetButtonsInteractable(false);
        var body = new LoginBody { AccountId = accountId, Password = password };
        SendNotification(NotificationConst.LOGIN, body);
    }

    private void OnRegisterBtnClick()
    {
        string accountText = accountInput != null ? accountInput.text : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;
        if (string.IsNullOrEmpty(accountText))
        {
            SendNotification(NotificationConst.SHOW_TIP, "Account cannot be empty.");
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            SendNotification(NotificationConst.SHOW_TIP, "Password cannot be empty.");
            return;
        }
        if (!int.TryParse(accountText, out int accountId))
        {
            SendNotification(NotificationConst.SHOW_TIP, "Account must be a number.");
            return;
        }
        SetButtonsInteractable(false);
        var body = new RegisterBody { AccountId = accountId, Password = password };
        SendNotification(NotificationConst.REGISTER, body);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginBtn != null) loginBtn.interactable = interactable;
        if (registerBtn != null) registerBtn.interactable = interactable;
    }
    #endregion

    #region Notification Handlers
    private void OnLoginSuccess(UserVO userData)
    {
        Log.d("Login success.", NAME);
        SaveCredentials();
    }

    private void OnLoginFailed(string reason)
    {
        Log.w($"Login failed: {reason}", NAME);
        SetButtonsInteractable(true);
        SendNotification(NotificationConst.SHOW_TIP, reason);
    }

    private void OnRegisterSuccess()
    {
        Log.d("Register success.", NAME);
        SetButtonsInteractable(true);
        SaveCredentials();
        SendNotification(NotificationConst.SHOW_TIP, "Register success! Please login.");
    }

    private void OnRegisterFailed(string reason)
    {
        Log.w($"Register failed: {reason}", NAME);
        SendNotification(NotificationConst.SHOW_TIP, reason);
    }

    private void SaveCredentials()
    {
        if (accountInput != null) PlayerPrefsManager.SetString(PlayerPrefsConst.LastAccount, accountInput.text);
        if (passwordInput != null) PlayerPrefsManager.SetString(PlayerPrefsConst.LastPassword, passwordInput.text);
    }
    #endregion

    #region Update (Optional)
    // protected override bool NeedsUpdate() => true;
    // protected override UpdateFrequency GetUpdateFrequency() => UpdateFrequency.Low;
    // protected override UpdateType[] GetUpdateTypes() => new UpdateType[] { UpdateType.Update };
    // protected override void OnUpdate(float deltaTime) { }
    // protected override void OnFixedUpdate(float fixedDeltaTime) { }
    // protected override void OnLateUpdate(float deltaTime) { }
    #endregion
}