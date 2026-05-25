using PureMVC.Patterns.Proxy;
using PureMVC.Patterns.Facade;

/// <summary>
/// Base class for all Proxy classes in the project.
/// Provides convenient access to Facade, common proxy operations,
/// and optional Lua hook support for hot-updatable data logic.
/// </summary>
public abstract class ProxyBase : Proxy
{
    protected ProxyBase(string proxyName, object data = null) : base(proxyName, data)
    {
    }

    /// <summary>
    /// Retrieve a registered proxy by name
    /// </summary>
    protected T GetProxy<T>(string proxyName) where T : ProxyBase
    {
        return Facade.RetrieveProxy(proxyName) as T;
    }

    /// <summary>
    /// Try invoke a Lua hook for the given hook name.
    /// Lua file path: LuaScripts/ProxyHook/{proxyTypeName}.lua
    /// Lua function name matches hookName.
    /// Returns true if Lua handled it.
    /// 
    /// Usage in subclass:
    ///   public override void OnRegister() { base.OnRegister(); TryLuaHook("OnRegister"); ... }
    ///   or call TryLuaHook("OnDataChanged", "items", newValue) on data change.
    /// </summary>
    protected bool TryLuaHook(string hookName, params object[] args)
    {
        return LuaHookHelper.TryLuaHook("ProxyHook", GetType().Name, hookName, args);
    }
}