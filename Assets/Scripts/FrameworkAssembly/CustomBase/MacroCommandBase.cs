using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using PureMVC.Patterns.Facade;

/// <summary>
/// Base class for all MacroCommand classes in the project.
/// Provides convenient proxy retrieval and unified exception handling.
/// </summary>
public abstract class MacroCommandBase : MacroCommand
{
    public override void Execute(INotification notification)
    {
        try
        {
            base.Execute(notification);
        }
        catch (System.Exception e)
        {
            Log.e($"MacroCommand {GetType().Name} execute error: {e.Message}", "MacroCommandBase");
        }
    }

    /// <summary>
    /// Retrieve a registered proxy by name
    /// </summary>
    protected T GetProxy<T>(string proxyName) where T : class, IProxy
    {
        return Facade.RetrieveProxy(proxyName) as T;
    }
}
