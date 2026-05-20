using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Tools → Clean Build Outputs
/// Removes all generated files from the build pipeline.
/// </summary>
public class CleanBuildOutputs : EditorWindow
{
    [MenuItem("Tools/Clean Build Outputs", false, 600)]
    public static void ShowWindow()
    {
        var window = GetWindow<CleanBuildOutputs>(true, "Clean Build Outputs", true);
        window.minSize = new Vector2(420, 340);
        window.maxSize = new Vector2(500, 380);
        window.ShowModal();
    }

    private Vector2 scrollPos;
    private bool cleanBuilds = true;
    private bool cleanStreamingAssets = true;
    private bool cleanResourcesDll = true;
    private bool cleanVersionFile = true;

    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Select items to clean:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Generated build outputs will be permanently deleted.", MessageType.Warning);

        EditorGUILayout.Space(5);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        cleanBuilds               = EditorGUILayout.ToggleLeft("1. Builds/                           (APK/EXE + HotUpdate packages)", cleanBuilds);
        cleanStreamingAssets      = EditorGUILayout.ToggleLeft("2. StreamingAssets/HotUpdate/         (AssetBundles + manifest + LuaScripts)", cleanStreamingAssets);
        cleanResourcesDll         = EditorGUILayout.ToggleLeft("3. Resources/HotUpdateAssembly.bytes  (IL2CPP base DLL)", cleanResourcesDll);
        cleanVersionFile          = EditorGUILayout.ToggleLeft("4. ProjectSettings/version.txt        (version number)", cleanVersionFile);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Clean Selected", GUILayout.Width(130), GUILayout.Height(30)))
        {
            Clean();
        }

        if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(30)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void Clean()
    {
        int deletedCount = 0;

        if (cleanBuilds)
        {
            string buildsDir = Path.Combine(Application.dataPath, "../Builds");
            DeleteDir(buildsDir, ref deletedCount);
        }

        if (cleanStreamingAssets)
        {
            string saHotUpdate = Path.Combine(Application.streamingAssetsPath, "HotUpdate");
            string saLua = Path.Combine(Application.streamingAssetsPath, "LuaScripts");
            DeleteDir(saHotUpdate, ref deletedCount);
            DeleteDir(saLua, ref deletedCount);
        }

        if (cleanResourcesDll)
        {
            string resDll = Path.Combine(Application.dataPath, "Resources/HotUpdateAssembly.bytes");
            string resDllMeta = resDll + ".meta";
            DeleteFile(resDll, ref deletedCount);
            DeleteFile(resDllMeta, ref deletedCount);
        }

        if (cleanVersionFile)
        {
            string verFile = Path.Combine(Application.dataPath, "../ProjectSettings/version.txt");
            string verMeta = verFile + ".meta";
            DeleteFile(verFile, ref deletedCount);
            DeleteFile(verMeta, ref deletedCount);
        }

        AssetDatabase.Refresh();

        string msg = deletedCount > 0
            ? $"Clean complete. {deletedCount} file(s)/folder(s) removed."
            : "Nothing to clean.";

        Log.d(msg, "CleanBuildOutputs");
        EditorUtility.DisplayDialog("Done", msg, "OK");
        Close();
    }

    private static void DeleteDir(string path, ref int count)
    {
        if (!Directory.Exists(path)) return;
        Directory.Delete(path, true);
        DeleteMeta(path, ref count);
        count++;
        Log.d($"Deleted: {path}", "CleanBuildOutputs");
    }

    private static void DeleteSubDir(string parent, string subName, ref int count)
    {
        string fullPath = Path.Combine(parent, subName);
        DeleteDir(fullPath, ref count);
    }

    private static void DeleteFile(string path, ref int count)
    {
        if (!File.Exists(path)) return;
        File.Delete(path);
        count++;
        Log.d($"Deleted: {path}", "CleanBuildOutputs");
    }

    private static void DeleteMeta(string path, ref int count)
    {
        string meta = path + ".meta";
        if (!File.Exists(meta)) return;
        File.Delete(meta);
        count++;
    }
}
