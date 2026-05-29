/// <summary>
/// Calls a Lua hook function when the clip starts.
/// Delegates to LuaHookHelper.TryLuaHook.
/// </summary>
public class LuaHookAction : ITimelineAction
{
    private string hookCategory;
    private string typeName;
    private string hookName;

    /// <summary>
    /// Create a Lua hook action.
    /// </summary>
    /// <param name="hookCategory">"ProxyHook", "CommandHook", or "MediatorHook".</param>
    /// <param name="typeName">Caller's Type.Name.</param>
    /// <param name="hookName">Lua function name to call.</param>
    public LuaHookAction(string hookCategory, string typeName, string hookName)
    {
        this.hookCategory = hookCategory;
        this.typeName = typeName;
        this.hookName = hookName;
    }

    public void OnEnter(TimelineContext ctx)
    {
        if (string.IsNullOrEmpty(hookCategory) || string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(hookName))
        {
            Log.w($"LuaHookAction: missing parameters (category={hookCategory}, type={typeName}, hook={hookName})", "LuaHookAction");
            return;
        }

        bool handled = LuaHookHelper.TryLuaHook(hookCategory, typeName, hookName, ctx);
        Log.d($"LuaHookAction: [{hookCategory}.{typeName}.{hookName}] handled={handled}", "LuaHookAction");
    }

    public void OnUpdate(TimelineContext ctx, float elapsed) { }
    public void OnExit(TimelineContext ctx) { }
}
