using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Manages persistent data storage using JSON files.
/// Files stored in Application.persistentDataPath/Saves/.
/// </summary>
public static class SaveManager
{
    private static string SaveDir
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string GetPath(string key) => Path.Combine(SaveDir, $"{key}.json");

    #region Save / Load
    /// <summary>
    /// Save a serializable object as JSON file
    /// </summary>
    public static void Save<T>(string key, T data)
    {
        try
        {
            string path = GetPath(key);
            string json = JsonUtility.ToJson(data);
            File.WriteAllText(path, json);
            Log.d($"Saved key: {key}", "SaveManager");
        }
        catch (Exception e)
        {
            Log.e($"Save failed for key [{key}]: {e.Message}", "SaveManager");
        }
    }

    /// <summary>
    /// Load and deserialize a JSON object. Returns default(T) if key not found.
    /// </summary>
    public static T Load<T>(string key) where T : new()
    {
        try
        {
            string path = GetPath(key);
            if (!File.Exists(path))
            {
                Log.d($"No save data found for key: {key}, returning default", "SaveManager");
                return new T();
            }
            string json = File.ReadAllText(path);
            T data = JsonUtility.FromJson<T>(json);
            Log.d($"Loaded key: {key}", "SaveManager");
            return data;
        }
        catch (Exception e)
        {
            Log.e($"Load failed for key [{key}]: {e.Message}", "SaveManager");
            return new T();
        }
    }

    /// <summary>
    /// Load into an existing object instance (avoids allocation)
    /// </summary>
    public static void LoadInto<T>(string key, T target)
    {
        try
        {
            string path = GetPath(key);
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite(json, target);
            Log.d($"LoadInto key: {key}", "SaveManager");
        }
        catch (Exception e)
        {
            Log.e($"LoadInto failed for key [{key}]: {e.Message}", "SaveManager");
        }
    }
    #endregion

    #region Key Management
    /// <summary>
    /// Check if a save key exists
    /// </summary>
    public static bool HasKey(string key)
    {
        return File.Exists(GetPath(key));
    }

    /// <summary>
    /// Delete a specific save key
    /// </summary>
    public static void Delete(string key)
    {
        string path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
            Log.d($"Deleted key: {key}", "SaveManager");
        }
    }

    /// <summary>
    /// Delete all save data (use with caution)
    /// </summary>
    public static void DeleteAll()
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                Log.w("All save data deleted", "SaveManager");
            }
        }
        catch (Exception e)
        {
            Log.e($"DeleteAll failed: {e.Message}", "SaveManager");
        }
    }
    #endregion

    #region Convenience Keys
    public static class Keys
    {
        public const string UserData = "save_user";
        public const string BagData  = "save_bag";
        public const string Settings = "save_settings";
    }
    #endregion
}
