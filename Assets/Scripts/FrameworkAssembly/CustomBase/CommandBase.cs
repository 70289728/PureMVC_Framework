using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using PureMVC.Patterns.Facade;

/// <summary>
/// Base class for all SimpleCommand classes in the project.
/// Provides convenient proxy retrieval and unified exception handling.
/// </summary>
public abstract class CommandBase : SimpleCommand
{
    public override void Execute(INotification notification)
    {
        try
        {
            OnExecute(notification);
        }
        catch (System.Exception e)
        {
            Log.e($"Command {GetType().Name} execute error: {e.Message}", "CommandBase");
        }
    }

    /// <summary>
    /// Override this instead of Execute to get built-in exception handling
    /// </summary>
    protected abstract void OnExecute(INotification notification);

    /// <summary>
    /// Retrieve a registered proxy by name
    /// </summary>
    protected T GetProxy<T>(string proxyName) where T : class, IProxy
    {
        return Facade.RetrieveProxy(proxyName) as T;
    }
}