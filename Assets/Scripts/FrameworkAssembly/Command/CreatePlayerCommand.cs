using PureMVC.Interfaces;

public class CreatePlayerCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var body = notification.Body as CreatePlayerBody;
        if (body == null)
        {
            Log.e("CreatePlayerCommand: invalid body", "CreatePlayerCommand");
            return;
        }

        NetworkMessageHelper.SendCreatePlayer(body.PlayerName, body.Gender, body.Job);
    }
}

public class CreatePlayerBody
{
    public string PlayerName;
    public int Gender;
    public int Job;
}
