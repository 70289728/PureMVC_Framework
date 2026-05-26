using PureMVC.Interfaces;

/// <summary>
/// Interface for hot-update startup macro commands.
/// Implemented in HotUpdateAssembly so GameMain can invoke
/// via Activator + interface cast instead of full reflection.
/// </summary>
public interface IHotUpdateStartup
{
    void Execute(INotification notification);
}
