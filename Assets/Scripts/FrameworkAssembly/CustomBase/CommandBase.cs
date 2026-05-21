using PureMVC.Interfaces;
using PureMVC.Patterns.Command;
using PureMVC.Patterns.Facade;

/// <summary>
/// Base class for all SimpleCommand classes in the project.
/// Provides convenient proxy retrieval, unified exception handling,
/// and optional Lua hook support for hot-updatable command logic.
/// </summary>
public abstract class CommandBase : SimpleCommand
{
    public override void Execute(INotification notification)
    {
        try
        {
            // Try Lua hook first; if Lua handles it, skip C# OnExecute
            if (!TryLuaHook("OnExecute", notification))
                OnExecute(notification);
        }
        catch (System.Exception e)
        {
            Log.e($"Command {GetType().Name} execute error: {e.Message}", "CommandBase");
        }
    }

    /// <summary>
    /// Override this instead of Execute to get built-in exception handling and Lua hook support.
    /// This is called only when no Lua hook handles the execution.
    /// </summary>
    protected abstract void OnExecute(INotification notification);

    /// <summary>
    /// Retrieve a registered proxy by name
    /// </summary>
    protected T GetProxy<T>(string proxyName) where T : class, IProxy
    {
        return Facade.RetrieveProxy(proxyName) as T;
    }

    /// <summary>
    /// Try invoke a Lua hook for the given hook name.
    /// Lua file path: LuaScripts/CommandHook/{commandTypeName}.lua
    /// Lua function name matches hookName.
    /// Returns true if Lua handled it.
    /// </summary>
    protected bool TryLuaHook(string hookName, params object[] args)
    {
#if UNITY_EDITOR
        return false;
#else
        var lua = LuaBootstrap.Instance;
        if (lua == null || !lua.IsInitialized) return false;

        string path = $"LuaScripts/CommandHook/{GetType().Name}.lua";
        object result = lua.Call(path, hookName, args);
        return result is bool b && b;
#endif
    }
}