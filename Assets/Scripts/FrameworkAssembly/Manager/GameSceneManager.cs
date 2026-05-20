using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wraps Unity's SceneManager with loading progress callbacks and UI cleanup hooks.
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    #region Singleton
    private static GameSceneManager instance;
    public static GameSceneManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("GameSceneManager");
                instance = go.AddComponent<GameSceneManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Member Variables
    private bool isLoading = false;
    public bool IsLoading => isLoading;
    public string CurrentScene => SceneManager.GetActiveScene().name;
    #endregion

    #region Load Scene
    /// <summary>
    /// Load scene synchronously
    /// </summary>
    public void LoadScene(string sceneName, Action onComplete = null)
    {
        if (isLoading)
        {
            Log.w($"Scene already loading, ignoring request: {sceneName}", "GameSceneManager");
            return;
        }
        OnBeforeSceneLoad();
        SceneManager.LoadScene(sceneName);
        onComplete?.Invoke();
        Log.d($"LoadScene: {sceneName}", "GameSceneManager");
    }

    /// <summary>
    /// Load scene asynchronously with progress callback
    /// </summary>
    public void LoadSceneAsync(string sceneName, Action<float> onProgress = null, Action onComplete = null)
    {
        if (isLoading)
        {
            Log.w($"Scene already loading, ignoring request: {sceneName}", "GameSceneManager");
            return;
        }
        StartCoroutine(LoadSceneCoroutine(sceneName, onProgress, onComplete));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName, Action<float> onProgress, Action onComplete)
    {
        isLoading = true;
        OnBeforeSceneLoad();
        Log.d($"LoadSceneAsync start: {sceneName}", "GameSceneManager");

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            // Unity async load reports 0~0.9 while loading, 1.0 when activated
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            onProgress?.Invoke(progress);

            if (op.progress >= 0.9f)
            {
                onProgress?.Invoke(1f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        isLoading = false;
        Log.d($"LoadSceneAsync complete: {sceneName}", "GameSceneManager");
        onComplete?.Invoke();
    }
    #endregion

    #region Hooks
    /// <summary>
    /// Called before every scene load — close all UIs and clear caches
    /// </summary>
    private void OnBeforeSceneLoad()
    {
        UIManager.Instance.CloseAllUI();
        ConfigManager.ClearAll();
        Log.d("OnBeforeSceneLoad: UI and configs cleared", "GameSceneManager");
    }
    #endregion
}
