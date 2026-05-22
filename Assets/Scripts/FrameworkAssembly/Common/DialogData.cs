using System;
using UnityEngine;

/// <summary>
/// Data payload for SHOW_TIP / SHOW_DIALOG notifications.
/// Created by business logic, consumed by UITipsMediator.
/// </summary>
[System.Serializable]
public class DialogData
{
    // ── Content ──────────────────────────────────────────

    /// <summary>Dialog title. Empty for Tip type.</summary>
    public string title;

    /// <summary>Message body text.</summary>
    public string message;

    /// <summary>Dialog type: Tip / Confirm / ServerPush.</summary>
    public DialogType type;

    // ── Buttons ──────────────────────────────────────────

    /// <summary>Confirm button config. Null = use default "OK".</summary>
    public DialogButton confirmBtn;

    /// <summary>Cancel button config. Null = no cancel button.</summary>
    public DialogButton cancelBtn;

    // ── Timing ───────────────────────────────────────────

    /// <summary>Auto-close delay in seconds. 0 = no auto-close.</summary>
    public float autoCloseDelay;

    // ── Priority ─────────────────────────────────────────

    /// <summary>Higher = shown first. Same priority = FIFO.</summary>
    public int priority;

    // ── Callbacks ────────────────────────────────────────

    /// <summary>Called when dialog is fully shown.</summary>
    [NonSerialized] public Action onShow;

    /// <summary>Called when user clicks confirm.</summary>
    [NonSerialized] public Action onConfirm;

    /// <summary>Called when user clicks cancel or dismisses.</summary>
    [NonSerialized] public Action onCancel;

    /// <summary>Called when dialog is closed (any reason).</summary>
    [NonSerialized] public Action onClose;

    // ── Factory Methods ──────────────────────────────────

    /// <summary>Create a simple tip.</summary>
    public static DialogData Tip(string message, float autoClose = 3f)
    {
        return new DialogData
        {
            message = message,
            type = DialogType.Tip,
            autoCloseDelay = autoClose,
            priority = 0,
        };
    }

    /// <summary>Create a confirm dialog.</summary>
    public static DialogData Confirm(string title, string message,
        Action onConfirm = null, Action onCancel = null,
        string confirmText = "Confirm", string cancelText = "Cancel")
    {
        return new DialogData
        {
            title = title,
            message = message,
            type = DialogType.Confirm,
            confirmBtn = new DialogButton { text = confirmText },
            cancelBtn = new DialogButton { text = cancelText },
            onConfirm = onConfirm,
            onCancel = onCancel,
            priority = 0,
        };
    }

    /// <summary>Create a single-OK-button info dialog.</summary>
    public static DialogData Info(string title, string message, Action onConfirm = null)
    {
        return new DialogData
        {
            title = title,
            message = message,
            type = DialogType.Confirm,
            confirmBtn = new DialogButton { text = "OK" },
            onConfirm = onConfirm,
            priority = 0,
        };
    }
}

/// <summary>
/// Button configuration for a dialog.
/// </summary>
[System.Serializable]
public class DialogButton
{
    public string text = "OK";
    public Color color = Color.white;
}
