using System.Collections;
using UnityEngine;

/// <summary>
/// Global coroutine runner for non-MonoBehaviour classes.
/// Created as a DontDestroyOnLoad GameObject.
/// </summary>
public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner instance;
    public static CoroutineRunner Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("CoroutineRunner");
                instance = go.AddComponent<CoroutineRunner>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
}
