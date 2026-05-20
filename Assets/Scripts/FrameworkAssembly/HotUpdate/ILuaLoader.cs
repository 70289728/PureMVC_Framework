/// <summary>
/// Interface for Lua script loading. Reserved for future xLua integration.
/// </summary>
public interface ILuaLoader
{
    /// <summary>
    /// Load a Lua script from the specified path without executing it.
    /// </summary>
    /// <param name="path">Relative path to the Lua script (e.g. "luascripts/game_logic.lua")</param>
    /// <returns>The loaded script text, or null if not found</returns>
    string LoadScript(string path);

    /// <summary>
    /// Load and execute a Lua script from the specified path.
    /// </summary>
    /// <param name="path">Relative path to the Lua script</param>
    /// <returns>True if execution succeeded</returns>
    bool ExecuteScript(string path);

    /// <summary>
    /// Call a specific function in a loaded Lua script.
    /// </summary>
    /// <param name="path">Relative path to the Lua script</param>
    /// <param name="funcName">Name of the function to call</param>
    /// <param name="args">Arguments to pass to the function</param>
    /// <returns>Return value from the Lua function, or null</returns>
    object CallFunction(string path, string funcName, params object[] args);
}
