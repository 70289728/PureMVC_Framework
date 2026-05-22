using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single floating tip item mediator.
/// Created by UITipsMediator when a Tip-type DialogData arrives.
/// Auto-dismisses after data.autoCloseDelay seconds.
/// </summary>
public class UITipsTipItemMediator : UIMediatorBase
{
    public const string NAME_PREFIX = "UITipsTipItemMediator_";

    #region UI Components
    [SerializeField] private Text tipsContentTxt;
    #endregion

    private DialogData _data;
    private Action<UITipsTipItemMediator> _onDismiss;
    private Coroutine _autoCloseCoroutine;
    private bool _dismissed;

    /// <summary>Current DialogData bound to this tip.</summary>
    public DialogData CurrentData => _data;

    /// <summary>Destroy the view GameObject. Called by UITipsMediator when removing this tip.</summary>
    public void DestroyView()
    {
        if (viewRootGo != null)
            GameObject.Destroy(viewRootGo);
    }

    public UITipsTipItemMediator(GameObject viewComponent, int layer)
        : base(NAME_PREFIX + viewComponent.GetInstanceID(), viewComponent, layer, false)
    {
    }

    protected override void InitUIComponents()
    {
        var textBind = viewTrans.GetComponentInChildren<TextBind>(true);
        if (textBind != null) tipsContentTxt = textBind.Component;
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
    }

    protected override void UnRegisterUIEvents()
    {
        base.UnRegisterUIEvents();
    }

    // ── Public API ──────────────────────────────────────

    /// <summary>
    /// Bind data and start auto-dismiss timer. Called by UITipsMediator after creation and Show().
    /// </summary>
    public void SetData(DialogData data, Action<UITipsTipItemMediator> onDismiss)
    {
        _data = data;
        _onDismiss = onDismiss;
        _dismissed = false;
        RefreshView();

        if (data.autoCloseDelay > 0)
        {
            Log.d($"TipItem auto-close timer started: {data.autoCloseDelay}s", "UITipsTipItemMediator");
            _autoCloseCoroutine = CoroutineRunner.Instance.StartCoroutine(AutoCloseCoroutine(data.autoCloseDelay));
        }

        data.onShow?.Invoke();
    }

    public override void OnShow()
    {
        base.OnShow();
        // Play slide-in animation here if Animator is attached
    }

    // ── View ────────────────────────────────────────────

    private void RefreshView()
    {
        if (tipsContentTxt != null && _data != null)
            tipsContentTxt.text = _data.message;
    }

    // ── Auto Dismiss ────────────────────────────────────

    private IEnumerator AutoCloseCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        Dismiss();
    }

    private void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;

        Log.d($"TipItem dismissed: {_data?.message}", "UITipsTipItemMediator");

        if (_autoCloseCoroutine != null)
        {
            CoroutineRunner.Instance.StopCoroutine(_autoCloseCoroutine);
            _autoCloseCoroutine = null;
        }

        _data?.onClose?.Invoke();
        _onDismiss?.Invoke(this);
    }

    // ── Cleanup ─────────────────────────────────────────

    public override void OnHide()
    {
        base.OnHide();
        // Don't auto-dismiss on Hide — lifecycle managed by UITipsMediator.RemoveTip
    }
}
