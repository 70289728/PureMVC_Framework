using PureMVC.Interfaces;

public class LoginCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var body = notification.Body as LoginBody;
        if (body == null)
        {
            Log.e("LoginCommand: invalid body", "LoginCommand");
            return;
        }

        NetworkMessageHelper.SendLogin(body.AccountId, body.Password);
    }
}

public class LoginBody
{
    public int AccountId;
    public string Password;
}
