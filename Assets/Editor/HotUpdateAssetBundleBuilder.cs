using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool for building AssetBundles using the three-tier packaging rules.
/// Supports multi-platform builds with LZ4 compression and enhanced manifest.
/// </summary>
public class HotUpdateAssetBundleBuilder
{
    #region AssetBundle Build

    [MenuItem("HotUpdate/Build AssetBundles (Windows)", false, 100)]
    public static void BuildAssetBundlesWindows()
    {
        BuildAssetBundlesForPlatform(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("HotUpdate/Build AssetBundles (Android)", false, 101)]
    public static void BuildAssetBundlesAndroid()
    {
        BuildAssetBundlesForPlatform(BuildTarget.Android);
    }

    [MenuItem("HotUpdate/Build AssetBundles (All Platforms)", false, 102)]
    public static void BuildAssetBundlesAll()
    {
        BuildAssetBundlesForPlatform(BuildTarget.StandaloneWindows64);
        BuildAssetBundlesForPlatform(BuildTarget.Android);
        Log.d("AssetBundles built for all platforms", "HotUpdateAssetBundleBuilder");
    }

    /// <summary>
    /// Build AssetBundles using the three-tier rules (Base/Module/Hotfix).
    /// Output goes to StreamingAssets for local testing.
    /// </summary>
    public static void BuildAssetBundlesForPlatform(BuildTarget target)
    {
        string platformName = target.ToString();
        string outputPath = Path.Combine(Application.streamingAssetsPath, "HotUpdate", "assetbundles");

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        string version = ProjectBuilder.ReadVersion();

        // Use the rules-based builder with LZ4 compression and enhanced manifest
        AssetBundleBuildRules.BuildWithRules(target, outputPath, version);

        Log.d($"AssetBundles ({platformName}) built to: {outputPath}", "HotUpdateAssetBundleBuilder");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Hot Update",
                $"AssetBundles ({platformName}) built to:\n{outputPath}\n\n" +
                $"Compression: LZ4\n" +
                $"Layers: Base / Module / Hotfix\n" +
                $"Manifest: manifest.json (enhanced)",
                "OK");
        }
    }

    #endregion

    #region Hot Update Server

    [MenuItem("HotUpdate/Start Hot Update Server", false, 200)]
    public static void StartHotUpdateServer()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string batPath = Path.Combine(projectRoot, "local_server", "start.bat");

        if (!File.Exists(batPath))
        {
            Log.e($"Server script not found: {batPath}", "HotUpdateAssetBundleBuilder");
            EditorUtility.DisplayDialog("Error", $"Server script not found:\n{batPath}", "OK");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = batPath,
                WorkingDirectory = Path.Combine(projectRoot, "local_server"),
                UseShellExecute = true,
                CreateNoWindow = false
            });
            Log.d("Hot update server started", "HotUpdateAssetBundleBuilder");
        }
        catch (Exception e)
        {
            Log.e($"Failed to start hot update server: {e.Message}", "HotUpdateAssetBundleBuilder");
            EditorUtility.DisplayDialog("Error", $"Failed to start server:\n{e.Message}", "OK");
        }
    }

    #endregion

    #region HybridCLR Shortcut

    /// <summary>
    /// One-click HybridCLR compile + generate all (same as HybridCLR/Generate/All).
    /// Convenience shortcut so you don't need to find it in the HybridCLR submenu.
    /// </summary>
    [MenuItem("HybridCLR/Compile And Generate", false, 100)]
    public static void HybridCLRCompileAndGenerate()
    {
        // Re-generate xLua code to clean up stale type references
        XLuaClearAndGenerate();

        Log.d("Running HybridCLR Compile And Generate...", "HotUpdateAssetBundleBuilder");
        HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll();
        Log.d("HybridCLR Compile And Generate complete", "HotUpdateAssetBundleBuilder");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("HybridCLR",
                "Compile And Generate complete.\n\n" +
                "Hot update DLLs are in HybridCLRData/HotUpdateDlls/",
                "OK");
        }
    }

    #endregion

    #region Hot Update Package (AB + DLLs + Manifest → Builds/HotUpdate/)

    /// <summary>
    /// One-click: build AssetBundles + copy HybridCLR DLLs + generate manifest
    /// into Builds/HotUpdate/v{version}/{platform}/ for the local_server.
    /// Does NOT rebuild the APK — only updates the hot update package.
    /// </summary>
    [MenuItem("HotUpdate/Build HotUpdate Package (Android)", false, 50)]
    public static void BuildHotUpdatePackageAndroid()
    {
        BuildHotUpdatePackage(BuildTarget.Android);
    }

    [MenuItem("HotUpdate/Build HotUpdate Package (Windows)", false, 51)]
    public static void BuildHotUpdatePackageWindows()
    {
        BuildHotUpdatePackage(BuildTarget.StandaloneWindows64);
    }

    public static void BuildHotUpdatePackage(BuildTarget target)
    {
        string platformName = GetPlatformName(target);
        string version = ProjectBuilder.ReadVersion();
        string hotUpdateOutputDir = Path.Combine("Builds", "HotUpdate", $"v{version}", platformName);

        Log.d($"=== Build HotUpdate Package: {platformName} v{version} ===", "HotUpdateAssetBundleBuilder");

        // Step -1: Re-generate xLua code to clean up stale type references
        XLuaClearAndGenerate();

        // Step 0: Run HybridCLR Generate/All (compile DLLs + generate all)
        Log.d("Running HybridCLR Generate/All...", "HotUpdateAssetBundleBuilder");
        HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll();
        Log.d("HybridCLR Generate/All complete", "HotUpdateAssetBundleBuilder");

        // Step 1: Build AssetBundles
        if (!Directory.Exists(hotUpdateOutputDir))
            Directory.CreateDirectory(hotUpdateOutputDir);

        AssetBundleBuildRules.BuildWithRules(target, hotUpdateOutputDir, version);
        Log.d($"AssetBundles built to: {hotUpdateOutputDir}", "HotUpdateAssetBundleBuilder");

        // Step 2: Copy HybridCLR hot update DLLs
        CopyHotUpdateDlls(target, hotUpdateOutputDir);

        // Step 3: Append DLL entries to manifest.json
        AppendDllsToManifest(hotUpdateOutputDir);

        // Step 3.5: Encrypt Lua scripts and copy to hot update output
        EncryptAndCopyLuaScripts(hotUpdateOutputDir);

        // Step 3.6: Append Lua .lua.enc entries to manifest.json
        AppendLuaFilesToManifest(hotUpdateOutputDir);

        Log.d($"=== HotUpdate Package Complete: {hotUpdateOutputDir} ===", "HotUpdateAssetBundleBuilder");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Hot Update Package",
                $"Hot update package built:\n{hotUpdateOutputDir}\n\n" +
                $"Version: v{version}\n" +
                $"Platform: {platformName}\n\n" +
                $"Restart local_server\\start.bat to serve the new package.",
                "OK");
        }
    }

    /// <summary>
    /// Copy HybridCLR compiled hot update DLLs from HybridCLRData/HotUpdateDlls/{platform}/
    /// to the hot update output directory.
    /// </summary>
    private static void CopyHotUpdateDlls(BuildTarget target, string hotUpdateOutputDir)
    {
        string platformDir = GetPlatformName(target);
        string dllSourceDir = Path.Combine("HybridCLRData", "HotUpdateDlls", platformDir);

        if (!Directory.Exists(dllSourceDir))
        {
            Log.w($"No HybridCLR DLL source dir: {dllSourceDir}, skipping DLL copy", "HotUpdateAssetBundleBuilder");
            return;
        }

        string[] dllFiles = Directory.GetFiles(dllSourceDir, "*.dll", SearchOption.TopDirectoryOnly);
        if (dllFiles.Length == 0)
        {
            Log.w($"No DLLs found in {dllSourceDir}, skipping DLL copy", "HotUpdateAssetBundleBuilder");
            return;
        }

        // Only copy the hot-update assembly. AOT assemblies (AOTAssembly, FrameworkAssembly,
        // Assembly-CSharp) and Unity packages (TextMeshPro, Timeline, etc.) are embedded in
        // the APK by IL2CPP and should NOT be re-downloaded at runtime.
        string[] hotUpdateOnlyDlls = { "HotUpdateAssembly.dll" };

        string dllDestDir = Path.Combine(hotUpdateOutputDir, "dll");
        if (!Directory.Exists(dllDestDir))
            Directory.CreateDirectory(dllDestDir);

        int copied = 0;
        foreach (string dllPath in dllFiles)
        {
            string fileName = Path.GetFileName(dllPath);
            if (!hotUpdateOnlyDlls.Contains(fileName))
            {
                Log.d($"Skipped AOT DLL: {fileName}", "HotUpdateAssetBundleBuilder");
                continue;
            }
            string destPath = Path.Combine(dllDestDir, fileName);
            File.Copy(dllPath, destPath, true);
            Log.d($"Copied hot-update DLL: {fileName}", "HotUpdateAssetBundleBuilder");
            copied++;
        }

        Log.d($"Copied {copied} DLL(s) to {dllDestDir}", "HotUpdateAssetBundleBuilder");
    }

    /// <summary>
    /// Append DLL file entries to the existing manifest.json so they are included
    /// in the hot update download list with MD5 hashes.
    /// </summary>
    private static void AppendDllsToManifest(string hotUpdateOutputDir)
    {
        string manifestPath = Path.Combine(hotUpdateOutputDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Log.w("manifest.json not found, cannot append DLL entries", "HotUpdateAssetBundleBuilder");
            return;
        }

        string dllDir = Path.Combine(hotUpdateOutputDir, "dll");
        if (!Directory.Exists(dllDir))
            return;

        string[] dllFiles = Directory.GetFiles(dllDir, "*.dll", SearchOption.TopDirectoryOnly);
        if (dllFiles.Length == 0)
            return;

        string json = File.ReadAllText(manifestPath);
        AssetBundleManifest manifest = JsonUtility.FromJson<AssetBundleManifest>(json);
        if (manifest == null || manifest.bundles == null)
        {
            Log.w("Failed to parse manifest.json for DLL append", "HotUpdateAssetBundleBuilder");
            return;
        }

        foreach (string dllPath in dllFiles)
        {
            string fileName = Path.GetFileName(dllPath);
            string relativePath = "dll/" + fileName;
            FileInfo fi = new FileInfo(dllPath);
            string md5 = ComputeFileMD5(dllPath);

            manifest.bundles.Add(new AssetBundleEntry
            {
                name = relativePath,
                md5 = md5,
                size = fi.Length
            });
        }

        string updatedJson = JsonUtility.ToJson(manifest, true);
        File.WriteAllText(manifestPath, updatedJson);
        Log.d($"Appended {dllFiles.Length} DLL(s) to manifest.json", "HotUpdateAssetBundleBuilder");
    }

    #endregion

    #region Manifest Generation

    [MenuItem("HotUpdate/Generate Manifest", false, 200)]
    public static void GenerateManifest()
    {
        string hotUpdateDir = Path.Combine(Application.streamingAssetsPath, "HotUpdate");
        if (!Directory.Exists(hotUpdateDir))
        {
            EditorUtility.DisplayDialog("Error", $"HotUpdate directory not found:\n{hotUpdateDir}\n\nBuild AssetBundles first.", "OK");
            return;
        }

        string version = ProjectBuilder.ReadVersion();

        // Generate legacy manifest for backward compatibility
        HotUpdateManifest manifest = new HotUpdateManifest
        {
            version = version,
            files = new System.Collections.Generic.List<HotUpdateFileEntry>()
        };

        string[] allFiles = Directory.GetFiles(hotUpdateDir, "*.*", SearchOption.AllDirectories);
        foreach (string filePath in allFiles)
        {
            string fileName = Path.GetFileName(filePath);
            if (fileName == "manifest.json" || fileName.EndsWith(".meta"))
            {
                continue;
            }

            string relativePath = filePath.Substring(hotUpdateDir.Length + 1).Replace("\\", "/");
            FileInfo fi = new FileInfo(filePath);
            string md5 = ComputeFileMD5(filePath);

            manifest.files.Add(new HotUpdateFileEntry
            {
                name = relativePath,
                md5 = md5,
                size = fi.Length
            });
        }

        string manifestPath = Path.Combine(hotUpdateDir, "manifest.json");
        string json = JsonUtility.ToJson(manifest, true);
        File.WriteAllText(manifestPath, json);

        Log.d($"Manifest generated: {manifestPath} ({manifest.files.Count} files)", "HotUpdateAssetBundleBuilder");

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Hot Update", $"Manifest generated:\n{manifestPath}\n\nVersion: {manifest.version}\nFiles: {manifest.files.Count}", "OK");
        }
    }

    #endregion

    #region Combined Build

    [MenuItem("HotUpdate/Build All (Bundles + Manifest)", false, 300)]
    public static void BuildAll()
    {
        BuildAssetBundlesAll();
        GenerateManifest();
        Log.d("Hot update build complete", "HotUpdateAssetBundleBuilder");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Encrypt all .lua files in Assets/LuaScripts/ and copy to hot update output
    /// as .lua.enc files. These are served by local_server for hot update.
    /// </summary>
    private static void EncryptAndCopyLuaScripts(string hotUpdateOutputDir)
    {
        string luaSrcRoot = Path.Combine(Application.dataPath, "LuaScripts");
        if (!Directory.Exists(luaSrcRoot))
        {
            Log.d("No LuaScripts directory, skipping Lua encryption", "HotUpdateAssetBundleBuilder");
            return;
        }

        string luaDestRoot = Path.Combine(hotUpdateOutputDir, "LuaScripts");
        if (!Directory.Exists(luaDestRoot))
            Directory.CreateDirectory(luaDestRoot);

        // Walk all .lua files recursively
        string[] luaFiles = Directory.GetFiles(luaSrcRoot, "*.lua", SearchOption.AllDirectories);
        int count = 0;
        foreach (string srcPath in luaFiles)
        {
            string relativePath = srcPath.Substring(luaSrcRoot.Length + 1);
            // Replace .lua with .lua.enc for encrypted output
            string encFileName = Path.ChangeExtension(relativePath, null) + ".lua.enc";
            string destPath = Path.Combine(luaDestRoot, encFileName);

            string destDir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            try
            {
                byte[] plain = File.ReadAllBytes(srcPath);
                byte[] encrypted = AesHelper.Encrypt(plain);
                File.WriteAllBytes(destPath, encrypted);
                count++;
            }
            catch (Exception e)
            {
                Log.e($"Failed to encrypt {relativePath}: {e.Message}", "HotUpdateAssetBundleBuilder");
            }
        }

        Log.d($"Encrypted {count} Lua scripts to {luaDestRoot}", "HotUpdateAssetBundleBuilder");
    }

    /// <summary>
    /// Append .lua.enc file entries to manifest.json so clients download them.
    /// </summary>
    private static void AppendLuaFilesToManifest(string hotUpdateOutputDir)
    {
        string luaDir = Path.Combine(hotUpdateOutputDir, "LuaScripts");
        if (!Directory.Exists(luaDir))
            return;

        string manifestPath = Path.Combine(hotUpdateOutputDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Log.w("manifest.json not found, cannot append Lua entries", "HotUpdateAssetBundleBuilder");
            return;
        }

        string[] encFiles = Directory.GetFiles(luaDir, "*.lua.enc", SearchOption.AllDirectories);
        if (encFiles.Length == 0)
            return;

        string json = File.ReadAllText(manifestPath);
        AssetBundleManifest manifest = JsonUtility.FromJson<AssetBundleManifest>(json);
        if (manifest == null || manifest.bundles == null)
            return;

        int added = 0;
        foreach (string filePath in encFiles)
        {
            string relativePath = filePath.Substring(hotUpdateOutputDir.Length + 1).Replace("\\", "/");
            FileInfo fi = new FileInfo(filePath);
            string md5 = ComputeFileMD5(filePath);
            manifest.bundles.Add(new AssetBundleEntry
            {
                name = relativePath,
                md5 = md5,
                size = fi.Length
            });
            added++;
        }

        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        Log.d($"Appended {added} Lua files to manifest.json", "HotUpdateAssetBundleBuilder");
    }

    private static string ComputeFileMD5(string filePath)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }

    private static string GetPlatformName(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64:
            case BuildTarget.StandaloneWindows:
                return "Windows";
            case BuildTarget.Android:
                return "Android";
            case BuildTarget.iOS:
                return "iOS";
            default:
                return target.ToString();
        }
    }

    /// <summary>
    /// Clear and regenerate xLua wrapper code to avoid stale assembly references
    /// ("Assembly HotUpdateAssembly is referenced by Assembly-CSharp" error).
    /// </summary>
    private static void XLuaClearAndGenerate()
    {
        CSObjectWrapEditor.Generator.ClearAll();
        CSObjectWrapEditor.Generator.GenAll();
        Log.d("XLua generated code cleared and regenerated", "HotUpdateAssetBundleBuilder");
    }

    #endregion
}
