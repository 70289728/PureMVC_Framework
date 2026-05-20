using PureMVC.Patterns.Proxy;
using PureMVC.Patterns.Facade;

/// <summary>
/// Base class for all Proxy classes in the project.
/// Provides convenient access to Facade and common proxy operations.
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
}