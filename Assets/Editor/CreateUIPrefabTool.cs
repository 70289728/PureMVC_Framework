using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor tool: create a new UI prefab folder + prefab with a black Image (60% alpha).
/// Menu: Tools → Create UI Prefab
/// 
/// Assembly target:
///   - Framework → `Assets/ProjectAssets/Base/UIAssets/Prefabs/` (Base layer, shipped with app)
///   - HotUpdate → `Assets/ProjectAssets/HotUpdate/UIAssets/Prefabs/` (Hotfix layer, downloadable)
///   Default: HotUpdate
/// 
/// AssetBundleBuildRules uses directory-based wildcard rules — no per-prefab rule needed.
/// </summary>
public class CreateUIPrefabTool : EditorWindow
{
    private string prefabName = "";

    /// <summary>
    /// Which assembly / layer this UI belongs to.
    /// </summary>
    private enum AssemblyTarget
    {
        HotUpdate,  // Hotfix layer
        Framework,  // Base layer
    }

    private AssemblyTarget targetAssembly = AssemblyTarget.HotUpdate;

    [MenuItem("Tools/Create UI Prefab", false, 500)]
    public static void ShowWindow()
    {
        var window = GetWindow<CreateUIPrefabTool>(true, "Create UI Prefab", true);
        window.minSize = new Vector2(350, 150);
        window.maxSize = new Vector2(450, 180);
        window.prefabName = "";
        window.targetAssembly = AssemblyTarget.HotUpdate;
        window.ShowModal();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Enter prefab name (also used as folder name):", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(5);
        prefabName = EditorGUILayout.TextField("Name:", prefabName);
        EditorGUILayout.Space(5);

        targetAssembly = (AssemblyTarget)EditorGUILayout.EnumPopup("Assembly:", targetAssembly);

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUI.enabled = !string.IsNullOrWhiteSpace(prefabName);
        if (GUILayout.Button("Create", GUILayout.Width(100)))
        {
            CreatePrefab();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void CreatePrefab()
    {
        string raw = prefabName.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            EditorUtility.DisplayDialog("Error", "Name cannot be empty.", "OK");
            return;
        }

        // Normalize: always produce UI-prefixed folder and prefab.
        // Chat → UIChat / UIChatPanel
        // UIChat → UIChat / UIChatPanel
        // UIChatPanel → UIChat / UIChatPanel
        // ChatPanel → UIChat / UIChatPanel
        string folderName;
        string prefabFile;

        // Strip Panel suffix if present
        string core = raw;
        if (core.EndsWith("Panel", System.StringComparison.OrdinalIgnoreCase))
            core = core.Substring(0, core.Length - "Panel".Length);

        // Strip UI prefix if present, then re-add
        if (core.StartsWith("UI", System.StringComparison.OrdinalIgnoreCase))
            core = core.Substring(2);

        folderName = "UI" + core;
        prefabFile = folderName + "Panel";

        // Choose base path by assembly target
        string basePath = targetAssembly == AssemblyTarget.Framework
            ? "Assets/ProjectAssets/Base/UIAssets/Prefabs"
            : "Assets/ProjectAssets/HotUpdate/UIAssets/Prefabs";

        string folderPath = Path.Combine(basePath, folderName);
        string prefabPath = Path.Combine(folderPath, prefabFile + ".prefab");

        if (Directory.Exists(folderPath) || File.Exists(prefabPath))
        {
            if (!EditorUtility.DisplayDialog("Overwrite?",
                $"Folder or prefab already exists:\n{folderPath}\n\nOverwrite the prefab? (Folder contents preserved.)", "Overwrite", "Cancel"))
            {
                return;
            }

            // Delete old prefab, refresh so AssetDatabase forgets it
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
                AssetDatabase.Refresh();
            }
        }

        // Create folder
        Directory.CreateDirectory(folderPath);
        AssetDatabase.Refresh();

        // Create root GameObject — stretch to fill screen
        var rootGO = new GameObject(prefabFile, typeof(RectTransform));
        var rootRT = rootGO.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // Create BG child (black, 60% alpha, fills parent)
        var imageGO = new GameObject("BG", typeof(RectTransform), typeof(Image));
        imageGO.transform.SetParent(rootGO.transform, false);

        var image = imageGO.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.6f);

        // Position Image to fill parent
        var imageRT = imageGO.GetComponent<RectTransform>();
        imageRT.anchorMin = Vector2.zero;
        imageRT.anchorMax = Vector2.one;
        imageRT.offsetMin = Vector2.zero;
        imageRT.offsetMax = Vector2.zero;

        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(rootGO, prefabPath);

        // Cleanup scene objects
        DestroyImmediate(rootGO);

        AssetDatabase.Refresh();

        Log.d($"UI prefab created: {prefabPath}", "CreateUIPrefabTool");

        AssetDatabase.Refresh();

        Close();
    }

    private void OnLostFocus()
    {
        // Don't close on lost focus
    }
}
