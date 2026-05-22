using System.Collections.Generic;
using PureMVC.Patterns.Facade;
using UnityEngine;

/// <summary>
/// Dialog queue manager — priority-based sequencing for tips and confirm dialogs.
/// 
/// Tips and dialogs use separate queues:
///   - Tip queue: non-blocking floating toasts, capped at maxVisible
///   - Dialog queue: modal blocking dialogs, shown one at a time
/// 
/// Lifecycle:
///   1. DialogManager.Instance.Initialize() — called by GameMain
///   2. Business logic: ShowTip("msg") / ShowConfirm("title","msg", onOk, onCancel)
///   3. Manager enqueues → sends SHOW_TIP / SHOW_DIALOG notification
///   4. UITipsMediator receives → instantiates UI → calls Manager.OnDialogShown/OnDialogClosed
/// </summary>
public class DialogManager : MonoBehaviour
{
    #region Singleton

    private static DialogManager instance;
    public static DialogManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("DialogManager");
                instance = go.AddComponent<DialogManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    #endregion

    #region Fields

    /// <summary>Modal confirm dialogs queue (shown one at a time).</summary>
    private readonly Queue<DialogData> dialogQueue = new Queue<DialogData>();

    /// <summary>Tip queue (non-blocking, multiple can show).</summary>
    private readonly Queue<DialogData> tipQueue = new Queue<DialogData>();

    /// <summary>Currently displayed modal dialog. Null = no dialog showing.</summary>
    private DialogData currentDialog;

    /// <summary>Currently displayed tips.</summary>
    private readonly List<DialogData> activeTips = new List<DialogData>();

    /// <summary>Max simultaneous tips.</summary>
    [SerializeField] private int maxVisibleTips = 3;

    /// <summary>True after Initialize().</summary>
    private bool initialized = false;

    /// <summary>Ensure UITipsMediator is open when a tip/dialog is requested.</summary>
    private void EnsurePanelOpen()
    {
        var uiMgr = UIManager.Instance;
        var existing = uiMgr.GetUIMediator<UITipsMediator>(UIConst.UITips);
        if (existing == null)
        {
            uiMgr.OpenUI<UITipsMediator>(UIConst.UITips, EUILayer.GuideLayer, false, false);
        }
    }

    #endregion

    #region Initialize

    /// <summary>Initialize the dialog system. Safe to call multiple times.</summary>
    public void Initialize()
    {
        if (initialized)
        {
            Log.d("DialogManager already initialized, skipping", "DialogManager");
            return;
        }

        initialized = true;
        Log.d("DialogManager initialized", "DialogManager");
    }

    #endregion

    #region Public API — Shortcuts

    /// <summary>Show a floating tip (3s auto-dismiss).</summary>
    public void ShowTip(string message)
    {
        ShowTip(message, 3f);
    }

    /// <summary>Show a floating tip with custom delay.</summary>
    public void ShowTip(string message, float autoCloseDelay)
    {
        var data = DialogData.Tip(message, autoCloseDelay);
        ShowTip(data);
    }

    /// <summary>Show a tip with full DialogData control.</summary>
    public void ShowTip(DialogData data)
    {
        if (!initialized) return;

        EnsurePanelOpen();

        // Cap active tips — dequeue oldest if full
        if (activeTips.Count >= maxVisibleTips)
        {
            var oldest = activeTips[0];
            oldest.onClose?.Invoke();
            activeTips.RemoveAt(0);
            Log.d($"Tip queue full, dropped oldest: {oldest.message}", "DialogManager");
        }

        activeTips.Add(data);
        Facade.Instance.SendNotification(NotificationConst.SHOW_TIP, data);
        Log.d($"Tip shown: {data.message}", "DialogManager");
    }

    /// <summary>Show a confirm dialog.</summary>
    public void ShowConfirm(string title, string message,
        System.Action onConfirm = null, System.Action onCancel = null,
        string confirmText = "Confirm", string cancelText = "Cancel")
    {
        var data = DialogData.Confirm(title, message, onConfirm, onCancel, confirmText, cancelText);
        ShowDialog(data);
    }

    /// <summary>Show a single-button info dialog.</summary>
    public void ShowInfo(string title, string message, System.Action onConfirm = null)
    {
        var data = DialogData.Info(title, message, onConfirm);
        ShowDialog(data);
    }

    /// <summary>Show a modal dialog.</summary>
    public void ShowDialog(DialogData data)
    {
        if (!initialized) return;

        EnsurePanelOpen();

        dialogQueue.Enqueue(data);
        Log.d($"Dialog enqueued: {data.title} ({dialogQueue.Count} in queue)", "DialogManager");

        // If no dialog is currently showing, process immediately
        if (currentDialog == null)
            ProcessNextDialog();
    }

    #endregion

    #region Queue Processing

    /// <summary>Called by UITipsMediator when a tip view is fully shown.</summary>
    public void OnTipShown(DialogData data)
    {
        // No-op for now — tips are fire-and-forget
    }

    /// <summary>Called by UITipsMediator when a tip is dismissed (auto or manual).</summary>
    public void OnTipClosed(DialogData data)
    {
        // onClose already invoked by UITipsTipItemMediator.Dismiss()
        activeTips.Remove(data);
    }

    /// <summary>Called by UITipsMediator when a dialog is fully shown.</summary>
    public void OnDialogShown(DialogData data)
    {
        currentDialog = data;
        data.onShow?.Invoke();
    }

    /// <summary>Called by UITipsMediator when user clicks confirm on current dialog.</summary>
    public void OnDialogConfirm()
    {
        if (currentDialog == null) return;

        var dialog = currentDialog;
        var callback = dialog.onConfirm;

        CloseCurrentDialog();

        callback?.Invoke();
    }

    /// <summary>Called by UITipsMediator when user clicks cancel on current dialog.</summary>
    public void OnDialogCancel()
    {
        if (currentDialog == null) return;

        var dialog = currentDialog;
        var callback = dialog.onCancel;

        CloseCurrentDialog();

        callback?.Invoke();
    }

    /// <summary>Close the current dialog and process the next in queue.</summary>
    private void CloseCurrentDialog()
    {
        if (currentDialog == null) return;

        var dialog = currentDialog;
        currentDialog = null;

        dialog.onClose?.Invoke();
        Log.d($"Dialog closed: {dialog.title}", "DialogManager");

        ProcessNextDialog();
    }

    /// <summary>Dequeue next dialog and show it.</summary>
    private void ProcessNextDialog()
    {
        while (dialogQueue.Count > 0)
        {
            var next = dialogQueue.Dequeue();

            // Skip null or empty data
            if (next == null || string.IsNullOrEmpty(next.message))
                continue;

            Facade.Instance.SendNotification(NotificationConst.SHOW_DIALOG, next);
            return;
        }
    }

    #endregion

    #region Cleanup

    /// <summary>Clear all queues and close current dialog.</summary>
    public void ClearAll()
    {
        // Close current dialog
        if (currentDialog != null)
        {
            currentDialog.onClose?.Invoke();
            currentDialog = null;
        }

        // Clear queues
        dialogQueue.Clear();
        tipQueue.Clear();

        // Clear active tips
        foreach (var tip in activeTips)
        {
            tip.onClose?.Invoke();
        }
        activeTips.Clear();

        Log.d("DialogManager cleared all dialogs and tips", "DialogManager");
    }

    #endregion
}
