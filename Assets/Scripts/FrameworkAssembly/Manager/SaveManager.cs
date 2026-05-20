using System;
using UnityEngine;

/// <summary>
/// Manages persistent data storage using PlayerPrefs + JsonUtility.
/// For small data sets. Replace with file-based or binary serialization for large data.
/// </summary>
public static class SaveManager
{
    #region Save / Load
    /// <summary>
    /// Save a serializable object as JSON
    /// </summary>
    public static void Save<T>(string key, T data)
    {
        try
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
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
            if (!PlayerPrefs.HasKey(key))
            {
                Log.d($"No save data found for key: {key}, returning default", "SaveManager");
                return new T();
            }
            string json = PlayerPrefs.GetString(key);
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
            if (!PlayerPrefs.HasKey(key)) return;
            string json = PlayerPrefs.GetString(key);
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
        return PlayerPrefs.HasKey(key);
    }

    /// <summary>
    /// Delete a specific save key
    /// </summary>
    public static void Delete(string key)
    {
        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
            Log.d($"Deleted key: {key}", "SaveManager");
        }
    }

    /// <summary>
    /// Delete all save data (use with caution)
    /// </summary>
    public static void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
        Log.w("All save data deleted", "SaveManager");
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
