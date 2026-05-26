using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages game config/table data.
/// 
/// Loading priority:
///   1. Lua config (future) — if ILuaConfigLoader is set, use it first
///   2. AssetBundle — runtime, via AssetBundleManager
///   3. AssetDatabase — Editor only, for fast iteration
/// 
/// Config files live in Assets/GameConfig/ (JSON format).
/// Bundle name: base_config.ab (Base layer, Config resource type).
/// 
/// Usage:
///   ConfigManager.Load<Item>()          // Load from GameConfig/Item.json
///   ConfigManager.Get<Item>(x => x.id == 5)
/// </summary>
public static class ConfigManager
{
    #region Fields

    private static Dictionary<Type, object> configCache = new Dictionary<Type, object>();
    private static readonly object _cacheLock = new object();

    /// <summary>
    /// Lua config loader interface. Set this to enable Lua-based config loading.
    /// When set, Lua takes priority over JSON/AssetBundle.
    /// </summary>
    public static ILuaConfigLoader LuaConfigLoader { get; set; }

    /// <summary>
    /// Config directory path relative to Assets.
    /// </summary>
    public const string CONFIG_DIR = "Assets/GameConfig/";

    /// <summary>
    /// AssetBundle name for config files.
    /// </summary>
    public const string CONFIG_BUNDLE_NAME = "base_config_config.ab";

    #endregion

    #region Load

    /// <summary>
    /// Load a config by type name from GameConfig/.
    /// e.g. Load<Item>() loads GameConfig/Item.json
    /// </summary>
    public static void Load<T>() where T : class
    {
        Type type = typeof(T);
        lock (_cacheLock)
        {
            if (configCache.ContainsKey(type))
            {
                Log.d($"Config already loaded: {type.Name}", "ConfigManager");
                return;
            }
        }

        // Priority 1: Lua config loader (future)
        if (LuaConfigLoader != null)
        {
            List<T> luaData = LuaConfigLoader.LoadConfig<T>();
            if (luaData != null && luaData.Count > 0)
            {
                lock (_cacheLock) { configCache[type] = luaData; }
                Log.d($"Loaded config via Lua: {type.Name} ({luaData.Count} items)", "ConfigManager");
                return;
            }
        }

        // Priority 2: AssetBundle (runtime)
        List<T> items = LoadFromAssetBundle<T>();
        if (items != null)
        {
            lock (_cacheLock) { configCache[type] = items; }
            Log.d($"Loaded config via AssetBundle: {type.Name} ({items.Count} items)", "ConfigManager");
            return;
        }

#if UNITY_EDITOR
        // Priority 3: AssetDatabase (Editor only)
        items = LoadFromAssetDatabase<T>();
        if (items != null)
        {
            lock (_cacheLock) { configCache[type] = items; }
            Log.d($"Loaded config via AssetDatabase: {type.Name} ({items.Count} items)", "ConfigManager");
            return;
        }
#endif

        Log.e($"Config not found: {type.Name}", "ConfigManager");
    }

    /// <summary>
    /// Load config by explicit resource path (legacy support).
    /// </summary>
    public static void Load<T>(string resourcePath) where T : class
    {
        // Try Resources as last fallback
        TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
        if (textAsset != null)
        {
            ParseAndCache<T>(textAsset.text);
            return;
        }

        // Fall through to standard loading
        Load<T>();
    }

    /// <summary>
    /// Load multiple configs by type.
    /// </summary>
    public static void LoadAll(params Type[] types)
    {
        foreach (Type type in types)
        {
            System.Reflection.MethodInfo method = typeof(ConfigManager).GetMethod(
                "Load", Type.EmptyTypes);
            System.Reflection.MethodInfo generic = method.MakeGenericMethod(type);
            generic.Invoke(null, null);
        }
    }

    /// <summary>
    /// Register a config list directly (e.g. from hardcoded data or ScriptableObject).
    /// </summary>
    public static void Register<T>(List<T> dataList) where T : class
    {
        lock (_cacheLock) { configCache[typeof(T)] = dataList; }
        Log.d($"Registered config: {typeof(T).Name} ({dataList.Count} items)", "ConfigManager");
    }

    #endregion

    #region Internal Loaders

    /// <summary>
    /// Load config from AssetBundle via AssetBundleManager.
    /// </summary>
    private static List<T> LoadFromAssetBundle<T>() where T : class
    {
        try
        {
            if (AssetBundleManager.Instance == null) return null;

            string assetName = typeof(T).Name;
            TextAsset textAsset = AssetBundleManager.Instance.LoadAsset<TextAsset>(CONFIG_BUNDLE_NAME, assetName);
            if (textAsset == null) return null;

            ConfigWrapper<T> wrapper = JsonUtility.FromJson<ConfigWrapper<T>>(textAsset.text);
            return wrapper?.items;
        }
        catch (Exception e)
        {
            Log.w($"AssetBundle config load failed for {typeof(T).Name}: {e.Message}", "ConfigManager");
            return null;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Load config from AssetDatabase in Editor for fast iteration.
    /// </summary>
    private static List<T> LoadFromAssetDatabase<T>() where T : class
    {
        try
        {
            string assetPath = CONFIG_DIR + typeof(T).Name + ".json";
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (textAsset == null) return null;

            ConfigWrapper<T> wrapper = JsonUtility.FromJson<ConfigWrapper<T>>(textAsset.text);
            return wrapper?.items;
        }
        catch (Exception e)
        {
            Log.w($"AssetDatabase config load failed for {typeof(T).Name}: {e.Message}", "ConfigManager");
            return null;
        }
    }
#endif

    /// <summary>
    /// Parse JSON and cache the result.
    /// </summary>
    private static void ParseAndCache<T>(string json) where T : class
    {
        try
        {
            ConfigWrapper<T> wrapper = JsonUtility.FromJson<ConfigWrapper<T>>(json);
            configCache[typeof(T)] = wrapper.items;
            Log.d($"Parsed config: {typeof(T).Name} ({wrapper.items.Count} items)", "ConfigManager");
        }
        catch (Exception e)
        {
            Log.e($"Config parse error [{typeof(T).Name}]: {e.Message}", "ConfigManager");
        }
    }

    #endregion

    #region Query

    /// <summary>
    /// Get first matching config entry.
    /// </summary>
    public static T Get<T>(Predicate<T> predicate) where T : class
    {
        var list = GetAll<T>();
        if (list == null) return null;
        return list.Find(predicate);
    }

    /// <summary>
    /// Get all config entries of type T.
    /// </summary>
    public static List<T> GetAll<T>() where T : class
    {
        Type type = typeof(T);
        lock (_cacheLock)
        {
            if (!configCache.ContainsKey(type))
            {
                Log.w($"Config not loaded: {type.Name}. Call ConfigManager.Load() first.", "ConfigManager");
                return null;
            }
            return configCache[type] as List<T>;
        }
    }

    /// <summary>
    /// Check if config type is already loaded.
    /// </summary>
    public static bool IsLoaded<T>()
    {
        lock (_cacheLock) { return configCache.ContainsKey(typeof(T)); }
    }

    #endregion

    #region Clear

    /// <summary>
    /// Clear all cached configs (e.g. on scene reload).
    /// </summary>
    public static void ClearAll()
    {
        lock (_cacheLock) { configCache.Clear(); }
        Log.d("All configs cleared", "ConfigManager");
    }

    #endregion

    #region Types

    /// <summary>
    /// Interface for Lua-based config loading.
    /// Implement this to load configs from Lua tables instead of JSON.
    /// </summary>
    public interface ILuaConfigLoader
    {
        /// <summary>
        /// Load a config list of type T from Lua.
        /// Return null if this config is not available in Lua.
        /// </summary>
        List<T> LoadConfig<T>() where T : class;
    }

    // Internal wrapper to match JSON format: { "items": [...] }
    [Serializable]
    private class ConfigWrapper<T>
    {
        public List<T> items = new List<T>();
    }

    #endregion
}
