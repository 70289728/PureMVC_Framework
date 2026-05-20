using PureMVC.Interfaces;
using PureMVC.Patterns.Command;

public class StartupMacroCommand : MacroCommandBase
{
    protected override void InitializeMacroCommand()
    {
        AddSubCommand(() => new RegisterFrameworkCommandsCommand());
        AddSubCommand(() => new HotUpdateCommand());
        AddSubCommand(() => new RegisterProxyCommand());
        AddSubCommand(() => new RegisterMediatorsCommand());
    }
}

public class RegisterFrameworkCommandsCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        Facade.RegisterCommand(NotificationConst.HOT_UPDATE_NEED_RESTART, () => new RestartCommand());
        Facade.RegisterCommand(NotificationConst.HOT_UPDATE_AVAILABLE, () => new OpenHotUpdateUICommand());

        // Register more Framework commands here...

        Log.d("Framework commands registered", "RegisterFrameworkCommandsCommand");
    }
}

public class RegisterProxyCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        Facade.RegisterProxy(new UserProxy());
        Facade.RegisterProxy(new NetworkProxy());
        Facade.RegisterProxy(new FriendProxy());
        Facade.RegisterProxy(new MailProxy());
        Facade.RegisterProxy(new AnnounceProxy());
        Facade.RegisterProxy(new SignInProxy());
        Facade.RegisterProxy(new BagProxy());
        Facade.RegisterProxy(new ShopProxy());
    }
}

public class RegisterMediatorsCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        // Mediators are registered dynamically via UIManager.OpenUI, no need to register here
    }
}