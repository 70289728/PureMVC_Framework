using PureMVC.Interfaces;

public class LoginSuccessCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var resp = notification.Body as LoginMessageS2C;
        bool hasPlayer = resp != null && resp.PlayerData != null && !string.IsNullOrEmpty(resp.PlayerData.PlayerName);

        Log.d($"Login success, hasPlayer={hasPlayer}.", "LoginSuccessCommand");
        UIManager.Instance.CloseUI(UIConst.UILogin);

        if (hasPlayer)
        {
            UIManager.Instance.OpenUI<UIMainMediator>(HotUpdateUIConst.UIMain);
            HotNetworkMessageHelper.SendPlayerExt(resp.AccountId);
        }
        else
        {
            UIManager.Instance.OpenUI<UICreatePlayerMediator>(HotUpdateUIConst.UICreatePlayer);
        }
    }
}
