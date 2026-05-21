using System.Collections.Generic;

/// <summary>
/// PureMVC notification constants for network events.
/// </summary>
public static class NetworkNotificationConst
{
    private static readonly Dictionary<string, string> registry = new Dictionary<string, string>();

    public static string NETWORK_CONNECTED => Get("NETWORK_CONNECTED");
    public static string NETWORK_DISCONNECTED => Get("NETWORK_DISCONNECTED");
    public static string NETWORK_ERROR => Get("NETWORK_ERROR");
    public static string NETWORK_DISCONNECTED_DIALOG => Get("NETWORK_DISCONNECTED_DIALOG");
    public static string NETWORK_RECONNECTED => Get("NETWORK_RECONNECTED");

    #region Registration

    static NetworkNotificationConst()
    {
        Register("NETWORK_CONNECTED", "NETWORK_CONNECTED");
        Register("NETWORK_DISCONNECTED", "NETWORK_DISCONNECTED");
        Register("NETWORK_ERROR", "NETWORK_ERROR");
        Register("NETWORK_DISCONNECTED_DIALOG", "NETWORK_DISCONNECTED_DIALOG");
        Register("NETWORK_RECONNECTED", "NETWORK_RECONNECTED");
    }

    public static void Register(string key, string name)
    {
        if (!registry.ContainsKey(key))
            registry[key] = name;
    }

    public static string Get(string key)
    {
        if (registry.TryGetValue(key, out string value))
            return value;
        return key;
    }

    #endregion
}
