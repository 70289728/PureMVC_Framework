using PureMVC.Interfaces;

public class RegisterCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var body = notification.Body as RegisterBody;
        if (body == null)
        {
            Log.e("RegisterCommand: invalid body", "RegisterCommand");
            return;
        }

        NetworkMessageHelper.SendRegister(body.AccountId, body.Password);
    }
}

public class RegisterBody
{
    public long AccountId;
    public string Password;
}
