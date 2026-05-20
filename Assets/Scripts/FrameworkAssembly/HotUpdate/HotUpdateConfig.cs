using UnityEngine;

/// <summary>
/// ScriptableObject configuration for the hot update system.
/// Create via: Assets → Create → HotUpdate → Config
/// </summary>
[CreateAssetMenu(fileName = "HotUpdateConfig", menuName = "HotUpdate/Config", order = 1)]
public class HotUpdateConfig : ScriptableObject
{
    [Header("Server")]
    [Tooltip("Base URL of the update server (e.g. http://localhost:8080)")]
    public string serverBaseUrl = "http://localhost:8080";

    [Tooltip("Path to the version manifest relative to server base URL")]
    public string manifestPath = "manifest.json";

    [Header("Download")]
    [Tooltip("Maximum number of download retry attempts per file")]
    public int maxRetryCount = 3;

    [Tooltip("Download timeout in seconds")]
    public int downloadTimeoutSeconds = 30;

    [Header("Local Storage")]
    [Tooltip("Subdirectory under persistentDataPath for hot update files")]
    public string localHotUpdateDir = "HotUpdate";

    [Header("Version")]
    [Tooltip("PlayerPrefs key for storing the current hot update version")]
    public string versionKey = "hot_update_version";
}
