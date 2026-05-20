using System.Collections.Generic;

/// <summary>
/// Proxy name constants for Facade.RegisterProxy / RemoveProxy.
/// Hot-update proxy names registered via HotUpdateProxyConst.RegisterTo().
/// </summary>
public static class ProxyConst
{
    private static readonly Dictionary<string, string> registry = new Dictionary<string, string>();

    public static string USER_PROXY => Get("USER_PROXY");
    public static string SHOP_PROXY => Get("SHOP_PROXY");
    public static string BAG_PROXY => Get("BAG_PROXY");

    #region Registration

    static ProxyConst()
    {
        Register("USER_PROXY", "UserProxy");
        Register("SHOP_PROXY", "ShopProxy");
        Register("BAG_PROXY", "BagProxy");
    }

    /// <summary>
    /// Register a proxy name mapping.
    /// </summary>
    public static void Register(string key, string proxyName)
    {
        if (!registry.ContainsKey(key))
            registry[key] = proxyName;
    }

    /// <summary>
    /// Get proxy name by key. Returns the key itself if not found.
    /// </summary>
    public static string Get(string key)
    {
        if (registry.TryGetValue(key, out string value))
            return value;
        return key;
    }

    #endregion
}
