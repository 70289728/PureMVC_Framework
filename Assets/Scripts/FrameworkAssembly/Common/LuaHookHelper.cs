/// <summary>
/// Shared Lua hook utility used by ProxyBase, CommandBase, MacroCommandBase, and UIMediatorBase.
/// Extracted to avoid duplicating the same TryLuaHook logic across four CustomBase classes.
/// </summary>
public static class LuaHookHelper
{
    /// <summary>
    /// Try invoke a Lua hook for the given type and hook name.
    /// </summary>
    /// <param name="hookCategory">"ProxyHook", "CommandHook", or "MediatorHook"</param>
    /// <param name="typeName">Caller's Type.Name</param>
    /// <param name="hookName">Lua function name</param>
    /// <param name="args">Arguments to pass to Lua</param>
    /// <returns>true if Lua handled it</returns>
    public static bool TryLuaHook(string hookCategory, string typeName, string hookName, params object[] args)
    {
#if UNITY_EDITOR
        return false;
#else
        var lua = LuaBootstrap.Instance;
        if (lua == null || !lua.IsInitialized) return false;

        string path = $"LuaScripts/{hookCategory}/{typeName}.lua";
        object result = lua.Call(path, hookName, args);
        return result is bool b && b;
#endif
    }
}
