using PureMVC.Interfaces;
using PureMVC.Patterns.Command;

/// <summary>
/// Hot-update startup macro command.
/// Registers all hot-update commands and proxies.
/// Called by GameMain via reflection at startup, after Framework's StartupMacroCommand.
/// 
/// Add new hot-update SubCommands here.
/// </summary>
public class HotUpdateStartupMacroCommand : MacroCommandBase, IHotUpdateStartup
{
    protected override void InitializeMacroCommand()
    {
        AddSubCommand(() => new RegisterHotUpdateCommandsCommand());
        AddSubCommand(() => new RegisterHotUpdateProxiesCommand());
    }
}

public class RegisterHotUpdateCommandsCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        // Register hot-update commands here, e.g.:
        // Facade.RegisterCommand(NotificationConst.SOME_NOTIF, () => new SomeCommand());

        Log.d("Hot-update commands registered", "RegisterHotUpdateCommandsCommand");
    }
}

public class RegisterHotUpdateProxiesCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        Facade.RegisterProxy(new PlayerExtProxy());
        Facade.RegisterProxy(new AchievementProxy());

        Log.d("Hot-update proxies registered", "RegisterHotUpdateProxiesCommand");
    }
}
