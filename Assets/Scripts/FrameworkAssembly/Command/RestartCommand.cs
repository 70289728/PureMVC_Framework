using PureMVC.Interfaces;

/// <summary>
/// Listens for HOT_UPDATE_NEED_RESTART.
/// Only fired after a real download finishes — opens the restart UI.
/// </summary>
public class RestartCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        Log.d("Hot update download complete — opening restart UI", "RestartCommand");
        UIManager.Instance.CloseUI(UIConst.UIHotUpdate, false);
        UIManager.Instance.OpenUI<UIReStartMediator>(UIConst.UIReStart, EUILayer.SecondLayer);
    }
}
