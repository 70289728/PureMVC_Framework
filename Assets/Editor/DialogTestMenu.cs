using UnityEditor;

/// <summary>
/// Editor-only test menu for the dialog system.
/// Click "Tools -> Test Dialog System" to open the test panel.
/// </summary>
public static class DialogTestMenu
{
    [MenuItem("Tools/Test Dialog System/1. Show Simple Tip")]
    public static void TestTip()
    {
        DialogManager.Instance.ShowTip("This is a simple tip message!");
    }

    [MenuItem("Tools/Test Dialog System/2. Show Tip With Custom Delay (5s)")]
    public static void TestTipLong()
    {
        DialogManager.Instance.ShowTip("This tip stays for 5 seconds", 5f);
    }

    [MenuItem("Tools/Test Dialog System/3. Show Info Dialog")]
    public static void TestInfo()
    {
        DialogManager.Instance.ShowInfo("Welcome", "Welcome to the game!");
    }

    [MenuItem("Tools/Test Dialog System/4. Show Confirm Dialog")]
    public static void TestConfirm()
    {
        DialogManager.Instance.ShowConfirm(
            "Delete Item",
            "Are you sure you want to delete this item?",
            onConfirm: () => { Log.d("User confirmed deletion!", "DialogTest"); },
            onCancel: () => { Log.d("User cancelled deletion", "DialogTest"); }
        );
    }

    [MenuItem("Tools/Test Dialog System/5. Show 3 Tips (overflow test)")]
    public static void TestTipOverflow()
    {
        DialogManager.Instance.ShowTip("First tip message");
        DialogManager.Instance.ShowTip("Second tip message");
        DialogManager.Instance.ShowTip("Third tip message");
        DialogManager.Instance.ShowTip("Fourth tip (should replace first)");
    }

    [MenuItem("Tools/Test Dialog System/6. Show Queued Dialogs")]
    public static void TestDialogQueue()
    {
        DialogManager.Instance.ShowConfirm("Dialog 1", "First dialog in queue");
        DialogManager.Instance.ShowConfirm("Dialog 2", "Second dialog — appears after you close 1");
        DialogManager.Instance.ShowConfirm("Dialog 3", "Third dialog — appears after you close 2");
    }

    [MenuItem("Tools/Test Dialog System/7. Confirm + Callback + Tip")]
    public static void TestConfirmWithTip()
    {
        DialogManager.Instance.ShowConfirm(
            "Open Chest",
            "Open the treasure chest?",
            onConfirm: () =>
            {
                DialogManager.Instance.ShowTip("You got 100 gold!");
                Log.d("Chest opened!", "DialogTest");
            }
        );
    }

    [MenuItem("Tools/Test Dialog System/8. Backward Compat: SendNotification string")]
    public static void TestLegacyStringTip()
    {
        // Old code sends string body — should still work
        PureMVC.Patterns.Facade.Facade.Instance.SendNotification(
            NotificationConst.SHOW_TIP,
            "Legacy string tip — still works!"
        );
    }
}
