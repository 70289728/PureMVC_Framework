using PureMVC.Interfaces;

/// <summary>
/// Opens the hot update UI when an update is available, so the user can
/// review update info and click Confirm/Cancel.
/// Fired by HotUpdateManager.CheckCoroutine via HOT_UPDATE_AVAILABLE.
/// </summary>
public class OpenHotUpdateUICommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        Log.d("Update available — opening hot update UI", "OpenHotUpdateUICommand");
        UIManager.Instance.OpenUI<UIHotUpdateMediator>(UIConst.UIHotUpdate);
    }
}
