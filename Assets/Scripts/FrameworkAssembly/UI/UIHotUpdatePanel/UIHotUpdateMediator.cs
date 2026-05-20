using PureMVC.Interfaces;
using UnityEngine;
using UnityEngine.UI;

public class UIHotUpdateMediator : UIMediatorBase
{
    public new const string NAME = UIConst.UIHotUpdate;

    #region UI Components
    [SerializeField] private Text statusTxt;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text progressTxt;
    #endregion

    private HotUpdateManager hotUpdateMgr;

    public UIHotUpdateMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    #region PureMVC Lifecycle

    public override string[] ListNotificationInterests()
    {
        return new string[]
        {
            NotificationConst.HOT_UPDATE_PROGRESS,
            NotificationConst.HOT_UPDATE_STATE_CHANGED,
        };
    }

    public override void HandleNotification(INotification notification)
    {
        string name = notification.Name;
        if (name == NotificationConst.HOT_UPDATE_PROGRESS)
            HandleHotUpdateProgress(notification);
        else if (name == NotificationConst.HOT_UPDATE_STATE_CHANGED)
            HandleHotUpdateStateChanged(notification);
    }

    #endregion

    #region UI Init

    protected override void InitUIComponents()
    {
        var allTextBinds = viewTrans.GetComponentsInChildren<TextBind>(true);
        foreach (var bind in allTextBinds)
        {
            switch (bind.gameObject.name)
            {
                case "StatusTxt": statusTxt = bind.Component; break;
                case "ProgressTxt": progressTxt = bind.Component; break;
            }
        }
        var allButtonBinds = viewTrans.GetComponentsInChildren<ButtonBind>(true);
        foreach (var bind in allButtonBinds)
        {
            switch (bind.gameObject.name)
            {
                case "ConfirmBtn": confirmBtn = bind.Component; break;
                case "CancelBtn": cancelBtn = bind.Component; break;
            }
        }
        progressSlider = viewTrans.GetComponentInChildren<SliderBind>(true).Component;
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
        if (confirmBtn != null)
            confirmBtn.onClick.AddListener(OnConfirmBtnClick);
        if (cancelBtn != null)
            cancelBtn.onClick.AddListener(OnCancelBtnClick);
    }

    #endregion

    #region Button Handlers

    private void OnConfirmBtnClick()
    {
        Log.d("User confirmed — starting download", "UIHotUpdateMediator");
        hotUpdateMgr?.StartDownload();
        SetButtonEnabled(false);
    }

    private void OnCancelBtnClick()
    {
        Log.d("User cancelled — quitting app", "UIHotUpdateMediator");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Notification Handlers

    private void HandleHotUpdateStateChanged(INotification notification)
    {
        if (notification.Body == null) return;
        var state = (HotUpdateState)notification.Body;

        hotUpdateMgr = HotUpdateManager.Instance;

        switch (state)
        {
            case HotUpdateState.Idle:
            case HotUpdateState.Checking:
                SetStatus($"v{hotUpdateMgr.CurrentManifest?.version}\n{hotUpdateMgr.StatusMessage}");
                SetButtonEnabled(true);
                if (progressSlider != null) progressSlider.value = 0f;
                if (progressTxt != null) progressTxt.text = "0%";
                break;

            case HotUpdateState.Downloading:
                SetStatus("Downloading...");
                SetButtonEnabled(false);
                break;

            case HotUpdateState.Verifying:
                SetStatus("Verifying files...");
                break;

            case HotUpdateState.Applying:
                SetStatus("Applying updates...");
                break;

            case HotUpdateState.Success:
                SetStatus("Update complete");
                SetButtonEnabled(false);
                break;

            case HotUpdateState.Failed:
                SetStatus("Download failed\nPlease check network");
                SetButtonEnabled(true);
                break;
        }
    }

    private void HandleHotUpdateProgress(INotification notification)
    {
        if (notification.Body == null) return;
        var data = (HotUpdateProgressData)notification.Body;

        if (progressSlider != null) progressSlider.value = data.progress;
        if (progressTxt != null) progressTxt.text = $"{data.progress * 100f:F0}%";
        SetStatus($"Downloading...\n{data.currentFile}/{data.totalFiles} files");
    }

    #endregion

    #region Helpers

    private void SetStatus(string msg)
    {
        if (statusTxt != null) statusTxt.text = msg;
    }

    private void SetButtonEnabled(bool enabled)
    {
        if (confirmBtn != null) confirmBtn.gameObject.SetActive(enabled);
        if (cancelBtn != null) cancelBtn.gameObject.SetActive(enabled);
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
