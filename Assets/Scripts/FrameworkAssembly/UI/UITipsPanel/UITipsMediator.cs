using System.Collections.Generic;
using PureMVC.Interfaces;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// General dialog & tip panel mediator.
/// 
/// Listens to SHOW_TIP (Tip-type DialogData) and SHOW_DIALOG (Confirm/ServerPush DialogData).
/// Renders floating tips via TipItem instances and modal dialogs via the DialogView subtree.
/// Communicates back to DialogManager for queue management via onConfirm/onCancel/onClose.
/// </summary>
public class UITipsMediator : UIMediatorBase
{
    public new const string NAME = UIConst.UITips;

    #region UI Components
    [SerializeField] private Transform mask;
    [SerializeField] private Transform tipContainer;
    [SerializeField] private Transform dialogView;
    [SerializeField] private Text titleTxt;
    [SerializeField] private Text contentTxt;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Text confirmTxt;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Text cancelTxt;
    [SerializeField] private Button closeBtn;
    [SerializeField] private Transform tipItemPrefab;
    [SerializeField] private Text tipsContentTxt;
    #endregion

    private DialogData _currentDialogData;
    private readonly List<UITipsTipItemMediator> _activeTips = new List<UITipsTipItemMediator>();
    private int _maxTips = 3;

    public UITipsMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    #region PureMVC Lifecycle

    public override string[] ListNotificationInterests()
    {
        return new string[]
        {
            NotificationConst.SHOW_TIP,
            NotificationConst.SHOW_DIALOG,
        };
    }

    public override void HandleNotification(INotification notification)
    {
        string name = notification.Name;
        if (name == NotificationConst.SHOW_TIP)
            HandleShowTip(notification.Body);
        else if (name == NotificationConst.SHOW_DIALOG)
            HandleShowDialog(notification.Body);
    }

    #endregion

    #region Init

    protected override void InitUIComponents()
    {
        var allTransformBinds = viewTrans.GetComponentsInChildren<TransformBind>(true);
        foreach (var bind in allTransformBinds)
        {
            switch (bind.gameObject.name)
            {
                case "Mask": mask = bind.Component; break;
                case "TipContainer": tipContainer = bind.Component; break;
                case "DialogView": dialogView = bind.Component; break;
                case "TipItem": tipItemPrefab = bind.Component; break;
            }
        }
        var allTextBinds = viewTrans.GetComponentsInChildren<TextBind>(true);
        foreach (var bind in allTextBinds)
        {
            switch (bind.gameObject.name)
            {
                case "TitleTxt": titleTxt = bind.Component; break;
                case "ContentTxt": contentTxt = bind.Component; break;
                case "ConfirmTxt": confirmTxt = bind.Component; break;
                case "CancelTxt": cancelTxt = bind.Component; break;
                case "TipsContentTxt": tipsContentTxt = bind.Component; break;
            }
        }
        var allButtonBinds = viewTrans.GetComponentsInChildren<ButtonBind>(true);
        foreach (var bind in allButtonBinds)
        {
            switch (bind.gameObject.name)
            {
                case "ConfirmBtn": confirmBtn = bind.Component; break;
                case "CancelButton": cancelBtn = bind.Component; break;
                case "CloseBtn": closeBtn = bind.Component; break;
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

    public override void OnShow()
    {
        base.OnShow();

        // Default state: only tip container visible, mask and dialog hidden
        if (mask != null)
            mask.gameObject.SetActive(false);
        if (dialogView != null)
            dialogView.gameObject.SetActive(false);
        if (closeBtn != null)
            closeBtn.gameObject.SetActive(false);
        // TipItem is a template — always hidden, only instantiated copies are shown
        if (tipItemPrefab != null)
            tipItemPrefab.gameObject.SetActive(false);
    }

    private void InitClickEvents()
    {
        if (confirmBtn != null)
            confirmBtn.onClick.AddListener(OnConfirmBtnClick);
        if (cancelBtn != null)
            cancelBtn.onClick.AddListener(OnCancelButtonClick);
        if (closeBtn != null)
            closeBtn.onClick.AddListener(OnCloseBtnClick);
    }

    #endregion

    #region Dialog

    private void HandleShowDialog(object body)
    {
        var data = body as DialogData;
        if (data == null) return;

        // Dismiss any lingering UI for the old dialog
        if (_currentDialogData != null)
            CloseDialogUI();

        _currentDialogData = data;
        RefreshDialogUI(data);
        DialogManager.Instance.OnDialogShown(data);
    }

    private void RefreshDialogUI(DialogData data)
    {
        if (mask != null)
            mask.gameObject.SetActive(true);

        if (dialogView != null)
            dialogView.gameObject.SetActive(true);

        if (titleTxt != null)
            titleTxt.text = data.title ?? "";

        if (contentTxt != null)
            contentTxt.text = data.message ?? "";

        // Confirm button
        if (confirmBtn != null)
            confirmBtn.gameObject.SetActive(true);
        if (data.confirmBtn != null && confirmTxt != null)
            confirmTxt.text = data.confirmBtn.text;

        // Cancel button
        bool hasCancel = data.cancelBtn != null;
        if (cancelBtn != null)
            cancelBtn.gameObject.SetActive(hasCancel);
        if (hasCancel && cancelTxt != null)
            cancelTxt.text = data.cancelBtn.text;

        // Close button (X) — only for ServerPush or when no cancel button exists
        if (closeBtn != null)
            closeBtn.gameObject.SetActive(!hasCancel);
    }

    private void CloseDialogUI()
    {
        if (mask != null)
            mask.gameObject.SetActive(false);
        if (dialogView != null)
            dialogView.gameObject.SetActive(false);

        _currentDialogData = null;
    }

    private void OnConfirmBtnClick()
    {
        var data = _currentDialogData;
        CloseDialogUI();
        DialogManager.Instance.OnDialogConfirm();
    }

    private void OnCancelButtonClick()
    {
        var data = _currentDialogData;
        CloseDialogUI();
        DialogManager.Instance.OnDialogCancel();
    }

    private void OnCloseBtnClick()
    {
        var data = _currentDialogData;
        CloseDialogUI();
        DialogManager.Instance.OnDialogCancel();
    }

    #endregion

    #region Tip

    private void HandleShowTip(object body)
    {
        DialogData data = null;

        // Backward compatibility: string body → wrap as DialogData.Tip
        if (body is string s)
            data = DialogData.Tip(s);
        else if (body is DialogData d)
            data = d;

        if (data == null || string.IsNullOrEmpty(data.message)) return;

        // Cap active tips
        while (_activeTips.Count >= _maxTips)
        {
            var oldest = _activeTips[0];
            RemoveTip(oldest);
        }

        var tipMediator = CreateTipItem(data);
        if (tipMediator != null)
        {
            _activeTips.Add(tipMediator);
            tipMediator.Show();
            DialogManager.Instance.OnTipShown(data);
        }
    }

    private UITipsTipItemMediator CreateTipItem(DialogData data)
    {
        if (tipItemPrefab == null || tipContainer == null)
        {
            Log.e("TipItem prefab or TipContainer not bound", "UITipsMediator");
            return null;
        }

        var go = GameObject.Instantiate(tipItemPrefab.gameObject, tipContainer);
        go.name = $"TipItem_{_activeTips.Count}";

        var mediator = new UITipsTipItemMediator(go, (int)EUILayer.GuideLayer);
        mediator.SetData(data, OnTipItemDismissed);

        return mediator;
    }

    private void OnTipItemDismissed(UITipsTipItemMediator mediator)
    {
        DialogManager.Instance.OnTipClosed(mediator.CurrentData);
        RemoveTip(mediator);
    }

    private void RemoveTip(UITipsTipItemMediator mediator)
    {
        _activeTips.Remove(mediator);
        mediator.DestroyView();
    }

    #endregion

    #region Cleanup

    public override void OnClose()
    {
        // Dismiss all active tips
        foreach (var tip in _activeTips)
        {
            tip.DestroyView();
        }
        _activeTips.Clear();

        // Close dialog if any
        CloseDialogUI();

        base.OnClose();
    }

    #endregion

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
