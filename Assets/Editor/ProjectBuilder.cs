using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Complete project build pipeline.
/// Supports command-line batchmode builds and Editor menu builds.
/// 
/// Usage (command line):
///   Unity.exe -batchmode -quit -executeMethod ProjectBuilder.BuildWindows
///   Unity.exe -batchmode -quit -executeMethod ProjectBuilder.BuildAndroid
///   Unity.exe -batchmode -quit -executeMethod ProjectBuilder.BuildAll
/// 
/// Version file: ProjectSettings/version.txt (semantic version x.y.z)
/// Output: Builds/{Platform}/v{version}/
/// HotUpdate output: Builds/HotUpdate/v{version}/
/// </summary>
public class ProjectBuilder
{
    #region Version Management

    private static readonly string VersionFilePath = "ProjectSettings/version.txt";

    /// <summary>
    /// Read current version from version.txt.
    /// </summary>
    public static string ReadVersion()
    {
        if (!File.Exists(VersionFilePath))
        {
            Log.w($"Version file not found at {VersionFilePath}, using default 1.0.0", "ProjectBuilder");
            return "1.0.0";
        }

        string version = File.ReadAllText(VersionFilePath).Trim();
        if (string.IsNullOrEmpty(version))
        {
            Log.w("Version file is empty, using default 1.0.0", "ProjectBuilder");
            return "1.0.0";
        }

        return version;
    }

    /// <summary>
    /// Increment patch version and write back to version.txt.
    /// Returns the new version string.
    /// </summary>
    public static string IncrementVersion()
    {
        string current = ReadVersion();
        string[] parts = current.Split('.');
        if (parts.Length != 3)
        {
            Log.w($"Invalid version format: {current}, resetting to 1.0.0", "ProjectBuilder");
            File.WriteAllText(VersionFilePath, "1.0.0");
            return "1.0.0";
        }

        int major = int.Parse(parts[0]);
        int minor = int.Parse(parts[1]);
        int patch = int.Parse(parts[2]) + 1;

        string newVersion = $"{major}.{minor}.{patch}";
        File.WriteAllText(VersionFilePath, newVersion);
        Log.d($"Version incremented: {current} -> {newVersion}", "ProjectBuilder");
        return newVersion;
    }

    /// <summary>
    /// Write a specific version to version.txt.
    /// </summary>
    public static void WriteVersion(string version)
    {
        File.WriteAllText(VersionFilePath, version);
        Log.d($"Version set to: {version}", "ProjectBuilder");
    }

    #endregion

    #region Build Targets

    /// <summary>
    /// Build for Windows (StandaloneWindows64).
    /// </summary>
    [MenuItem("Build/Windows (Increment Version)", false, 200)]
    public static void BuildWindows()
    {
        string version = IncrementVersion();
        BuildForPlatform(BuildTarget.StandaloneWindows64, version);
    }

    /// <summary>
    /// Build for Windows without incrementing version.
    /// </summary>
    [MenuItem("Build/Windows (Keep Version)", false, 201)]
    public static void BuildWindowsKeepVersion()
    {
        string version = ReadVersion();
        BuildForPlatform(BuildTarget.StandaloneWindows64, version);
    }

    /// <summary>
    /// Build for Android.
    /// </summary>
    [MenuItem("Build/Android (Increment Version)", false, 210)]
    public static void BuildAndroid()
    {
        string version = IncrementVersion();
        BuildForPlatform(BuildTarget.Android, version);
    }

    /// <summary>
    /// Build for Android without incrementing version.
    /// </summary>
    [MenuItem("Build/Android (Keep Version)", false, 211)]
    public static void BuildAndroidKeepVersion()
    {
        string version = ReadVersion();
        BuildForPlatform(BuildTarget.Android, version);
    }

    /// <summary>
    /// Build all supported platforms.
    /// </summary>
    [MenuItem("Build/Build All Platforms", false, 300)]
    public static void BuildAll()
    {
        string version = IncrementVersion();

        Log.d($"=== Build All Platforms v{version} ===", "ProjectBuilder");

        BuildForPlatform(BuildTarget.StandaloneWindows64, version);
        BuildForPlatform(BuildTarget.Android, version);

        Log.d($"=== Build All Platforms Complete v{version} ===", "ProjectBuilder");
    }

    #endregion

    #region Core Build Logic

    private static void BuildForPlatform(BuildTarget target, string version)
    {
        string platformName = GetPlatformName(target);
        string outputDir = GetOutputDir(platformName, version);
        string buildPath = GetBuildPath(target, outputDir);

        Log.d($"Building {platformName} v{version}...", "ProjectBuilder");
        Log.d($"Output: {buildPath}", "ProjectBuilder");

        // Ensure output directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Set PlayerSettings
        PlayerSettings.bundleVersion = version;
        PlayerSettings.productName = "PureMVC_Framework";

        // Force landscape orientation
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;

        // Disable IL2CPP code stripping to prevent MonoBehaviour scripts from being removed
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Disabled);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);

        // Allow HTTP connections on Android (required for hot update server)
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

        // Check if HybridCLR is initialized
        bool hybridClrReady = IsHybridCLRInitialized();
        if (!hybridClrReady)
        {
            Log.w("HybridCLR not initialized, C# hot update will be disabled", "ProjectBuilder");
            Log.w("Run 'HybridCLR/Installer' then 'HybridCLR/Generate/All' to enable C# hot update", "ProjectBuilder");
        }

        // Copy base HotUpdateAssembly.dll to Resources for IL2CPP runtime loading.
        // IL2CPP does not expose hot update assembly types via Type.GetType
        // without an explicit Assembly.Load first.
        CopyBaseDllToResources(target);

        // Build hot update ABs + copy to StreamingAssets BEFORE BuildPlayer
        // so they are included in the APK as built-in assets.
        BuildHotUpdateForPlatform(target, version);

        // Always build Player (HybridCLR is optional for C# hot update)
        {
            // Collect scenes
            string[] scenes = GetBuildScenes();

            // Build
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Log.d($"Build succeeded: {platformName} v{version} ({summary.totalSize / 1024 / 1024} MB)", "ProjectBuilder");

                // Generate build_info.json
                GenerateBuildInfo(outputDir, platformName, version, summary);

                // Show success dialog (only in Editor interactive mode)
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Build Complete",
                        $"{platformName} v{version}\n\nOutput: {outputDir}\nSize: {summary.totalSize / 1024 / 1024} MB",
                        "OK");
                }
            }
            else
            {
                Log.e($"Build failed: {platformName} v{version} - {summary.result}", "ProjectBuilder");

                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Build Failed",
                        $"{platformName} v{version}\n\nResult: {summary.result}\nErrors: {summary.totalErrors}",
                        "OK");
                }
            }
        }
    }

    /// <summary>
    /// Check if HybridCLR is installed and initialized.
    /// Returns false if HybridCLR is not present or not initialized, so we skip Player build.
    /// </summary>
    private static bool IsHybridCLRInitialized()
    {
        // Find HybridCLRSettings type by scanning all loaded assemblies
        System.Type settingsType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            settingsType = asm.GetType("HybridCLR.Editor.Settings.HybridCLRSettings");
            if (settingsType != null) break;
        }

        if (settingsType == null)
        {
            UnityEngine.Debug.Log("[ProjectBuilder] HybridCLRSettings type not found in any loaded assembly");
            return false;
        }

        // Check if HybridCLR has been installed (hotUpdateAssemblies is configured)
        try
        {
            // hotUpdateAssemblies is an instance field, get via HybridCLRSettings.Instance
            var instanceProp = settingsType.GetProperty("Instance");
            if (instanceProp == null)
            {
                UnityEngine.Debug.Log("[ProjectBuilder] HybridCLRSettings.Instance property not found");
                return false;
            }
            var instance = instanceProp.GetValue(null);
            if (instance == null)
            {
                UnityEngine.Debug.Log("[ProjectBuilder] HybridCLRSettings.Instance is null");
                return false;
            }

            var field = settingsType.GetField("hotUpdateAssemblies");
            if (field == null)
            {
                UnityEngine.Debug.Log("[ProjectBuilder] hotUpdateAssemblies field not found");
                return false;
            }
            var value = field.GetValue(instance);
            if (value == null)
            {
                UnityEngine.Debug.Log("[ProjectBuilder] hotUpdateAssemblies value is null");
                return false;
            }
            var list = value as System.Collections.IList;
            if (list == null || list.Count == 0)
            {
                UnityEngine.Debug.Log($"[ProjectBuilder] hotUpdateAssemblies list null or empty (count={list?.Count ?? -1})");
                return false;
            }
            UnityEngine.Debug.Log($"[ProjectBuilder] HybridCLR OK, hotUpdateAssemblies count={list.Count}");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log($"[ProjectBuilder] HybridCLR check exception: {e.Message}");
            return false;
        }

        return true;
    }

    #endregion

    #region Hot Update Build

    /// <summary>
    /// Build hot update AssetBundles for the given platform using the three-tier rules.
    /// Output: Builds/HotUpdate/v{version}/{platform}/
    /// Also copies to StreamingAssets for built-in fallback.
    /// </summary>
    private static void BuildHotUpdateForPlatform(BuildTarget target, string version)
    {
        string platformName = GetPlatformName(target);
        string hotUpdateOutputDir = Path.Combine("Builds", "HotUpdate", $"v{version}", platformName);

        Log.d($"Building hot update for {platformName} v{version}...", "ProjectBuilder");

        // Build using the three-tier rules (LZ4 compression, enhanced manifest)
        AssetBundleBuildRules.BuildWithRules(target, hotUpdateOutputDir, version);

        // Copy HybridCLR hot update DLLs to hot update output
        CopyHotUpdateDlls(target, hotUpdateOutputDir);

        // Append DLL entries to manifest.json so they get downloaded by clients
        AppendDllsToManifest(hotUpdateOutputDir);

        // Also copy to StreamingAssets for built-in fallback (Base + Module layers only)
        string streamingAssetsDir = Path.Combine(Application.streamingAssetsPath, "HotUpdate", "assetbundles");
        if (!Directory.Exists(streamingAssetsDir))
        {
            Directory.CreateDirectory(streamingAssetsDir);
        }

        // Copy all bundles to StreamingAssets for built-in fallback.
        // Hotfix bundles included so first-time install has complete assets.
        foreach (string filePath in Directory.GetFiles(hotUpdateOutputDir, "*.ab", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(filePath);
            File.Copy(filePath, Path.Combine(streamingAssetsDir, fileName), true);
        }

        // Copy manifest to StreamingAssets, but set version to "0.0.0"
        // so client always sees server version as newer → triggers hot update flow.
        string srcManifest = Path.Combine(hotUpdateOutputDir, "manifest.json");
        string dstManifest = Path.Combine(streamingAssetsDir, "manifest.json");
        if (File.Exists(srcManifest))
        {
            string manifestJson = File.ReadAllText(srcManifest);
            // Replace version with "0.0.0" so built-in manifest never matches server version
            manifestJson = System.Text.RegularExpressions.Regex.Replace(manifestJson,
                @"""version""\s*:\s*""[^""]+""", "\"version\": \"0.0.0\"");
            File.WriteAllText(dstManifest, manifestJson);
        }

        // Copy encrypted Lua scripts to StreamingAssets for built-in Lua support
        CopyLuaScriptsToStreamingAssets(hotUpdateOutputDir);

        Log.d($"Hot update built: {hotUpdateOutputDir}", "ProjectBuilder");
        Log.d($"StreamingAssets updated: {streamingAssetsDir}", "ProjectBuilder");
    }

    /// <summary>
    /// Encrypt Lua scripts from Assets/LuaScripts/ and copy to StreamingAssets
    /// so they are packaged into the APK as built-in Lua.
    /// </summary>
    private static void CopyLuaScriptsToStreamingAssets(string hotUpdateOutputDir)
    {
        string luaSrcRoot = Path.Combine(Application.dataPath, "LuaScripts");
        if (!Directory.Exists(luaSrcRoot))
        {
            Log.d("No LuaScripts directory, skipping Lua encryption for APK", "ProjectBuilder");
            return;
        }

        string luaDstDir = Path.Combine(Application.streamingAssetsPath, "LuaScripts");
        if (Directory.Exists(luaDstDir))
            Directory.Delete(luaDstDir, true);
        Directory.CreateDirectory(luaDstDir);

        string[] luaFiles = Directory.GetFiles(luaSrcRoot, "*.lua", SearchOption.AllDirectories);
        int count = 0;
        foreach (string srcPath in luaFiles)
        {
            string relativePath = srcPath.Substring(luaSrcRoot.Length + 1);
            string encFileName = Path.ChangeExtension(relativePath, null) + ".lua.enc";
            string destPath = Path.Combine(luaDstDir, encFileName);

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
                Log.e($"Failed to encrypt {relativePath}: {e.Message}", "ProjectBuilder");
            }
        }

        Log.d($"Encrypted {count} Lua scripts to StreamingAssets/LuaScripts", "ProjectBuilder");
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Copy base HotUpdateAssembly.dll from HybridCLR output to Resources.
    /// IL2CPP 需要这个文件来 Assembly.Load，否则 Type.GetType 找不到 HotUpdateAssembly 的类型。
    /// </summary>
    private static void CopyBaseDllToResources(BuildTarget target)
    {
        string platformDir = GetPlatformName(target);
        string srcDll = Path.Combine("HybridCLRData", "HotUpdateDlls", platformDir, "HotUpdateAssembly.dll");
        if (!File.Exists(srcDll))
        {
            Log.w($"Base DLL not found: {srcDll}. Run HybridCLR 'Compile And Generate' first.", "ProjectBuilder");
            return;
        }

        string resourcesDir = Path.Combine(Application.dataPath, "Resources");
        if (!Directory.Exists(resourcesDir))
            Directory.CreateDirectory(resourcesDir);

        string dst = Path.Combine(resourcesDir, "HotUpdateAssembly.bytes");
        File.Copy(srcDll, dst, true);
        AssetDatabase.Refresh();
        Log.d($"Base DLL copied to Resources: {dst}", "ProjectBuilder");
    }

    /// <summary>
    /// Copy HybridCLR compiled hot update DLLs from HybridCLRData/HotUpdateDlls/{platform}/
    /// to the hot update output directory so they can be downloaded by clients.
    /// </summary>
    private static void CopyHotUpdateDlls(BuildTarget target, string hotUpdateOutputDir)
    {
        string platformDir = GetPlatformName(target);
        string dllSourceDir = Path.Combine("HybridCLRData", "HotUpdateDlls", platformDir);

        if (!Directory.Exists(dllSourceDir))
        {
            Log.d($"No HybridCLR DLL source dir: {dllSourceDir}, skipping DLL copy", "ProjectBuilder");
            return;
        }

        string[] dllFiles = Directory.GetFiles(dllSourceDir, "*.dll", SearchOption.TopDirectoryOnly);
        if (dllFiles.Length == 0)
        {
            Log.d($"No DLLs found in {dllSourceDir}, skipping DLL copy", "ProjectBuilder");
            return;
        }

        // Only copy the hot-update assembly. AOT assemblies (AOTAssembly, FrameworkAssembly,
        // Assembly-CSharp) and Unity packages are embedded in the APK by IL2CPP.
        string[] hotUpdateOnlyDlls = { "HotUpdateAssembly.dll" };

        // Create a dll subdirectory in hot update output
        string dllDestDir = Path.Combine(hotUpdateOutputDir, "dll");
        if (!Directory.Exists(dllDestDir))
        {
            Directory.CreateDirectory(dllDestDir);
        }

        int copied = 0;
        foreach (string dllPath in dllFiles)
        {
            string fileName = Path.GetFileName(dllPath);
            if (!hotUpdateOnlyDlls.Contains(fileName))
            {
                Log.d($"Skipped AOT DLL: {fileName}", "ProjectBuilder");
                continue;
            }
            string destPath = Path.Combine(dllDestDir, fileName);
            File.Copy(dllPath, destPath, true);
            Log.d($"Copied hot-update DLL: {fileName}", "ProjectBuilder");
            copied++;
        }

        Log.d($"Copied {copied} DLL(s) to {dllDestDir}", "ProjectBuilder");
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
            Log.w("manifest.json not found, cannot append DLL entries", "ProjectBuilder");
            return;
        }

        string dllDir = Path.Combine(hotUpdateOutputDir, "dll");
        if (!Directory.Exists(dllDir))
        {
            return;
        }

        string[] dllFiles = Directory.GetFiles(dllDir, "*.dll", SearchOption.TopDirectoryOnly);
        if (dllFiles.Length == 0)
        {
            return;
        }

        // Read existing manifest
        string json = File.ReadAllText(manifestPath);
        AssetBundleManifest manifest = JsonUtility.FromJson<AssetBundleManifest>(json);
        if (manifest == null || manifest.bundles == null)
        {
            Log.w("Failed to parse manifest.json for DLL append", "ProjectBuilder");
            return;
        }

        // Append DLL entries as bundles
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

        // Write updated manifest
        string updatedJson = JsonUtility.ToJson(manifest, true);
        File.WriteAllText(manifestPath, updatedJson);
        Log.d($"Appended {dllFiles.Length} DLL(s) to manifest.json", "ProjectBuilder");
    }

    private static string ComputeFileMD5(string filePath)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    #endregion

    #region Build Info

    /// <summary>
    /// Generate build_info.json with metadata about this build.
    /// </summary>
    private static void GenerateBuildInfo(string outputDir, string platform, string version, BuildSummary summary)
    {
        var buildInfo = new BuildInfo
        {
            version = version,
            platform = platform,
            buildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            unityVersion = Application.unityVersion,
            buildSizeBytes = (long)summary.totalSize,
            buildSizeMB = (long)(summary.totalSize / 1024 / 1024),
            result = summary.result.ToString()
        };

        string json = JsonUtility.ToJson(buildInfo, true);
        string path = Path.Combine(outputDir, "build_info.json");
        File.WriteAllText(path, json);

        Log.d($"Build info saved: {path}", "ProjectBuilder");
    }

    #endregion

    #region Helpers

    private static string GetPlatformName(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64:
                return "Windows";
            case BuildTarget.StandaloneWindows:
                return "Windows";
            case BuildTarget.Android:
                return "Android";
            case BuildTarget.iOS:
                return "iOS";
            case BuildTarget.StandaloneLinux64:
                return "Linux";
            case BuildTarget.StandaloneOSX:
                return "MacOS";
            default:
                return target.ToString();
        }
    }

    private static string GetOutputDir(string platform, string version)
    {
        return Path.Combine("Builds", platform, $"v{version}");
    }

    private static string GetBuildPath(BuildTarget target, string outputDir)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64:
            case BuildTarget.StandaloneWindows:
                return Path.Combine(outputDir, "PureMVC_Framework.exe");
            case BuildTarget.Android:
                return Path.Combine(outputDir, "PureMVC_Framework.apk");
            case BuildTarget.iOS:
                return Path.Combine(outputDir, "PureMVC_Framework");
            case BuildTarget.StandaloneLinux64:
                return Path.Combine(outputDir, "PureMVC_Framework.x86_64");
            case BuildTarget.StandaloneOSX:
                return Path.Combine(outputDir, "PureMVC_Framework.app");
            default:
                return Path.Combine(outputDir, "PureMVC_Framework");
        }
    }

    private static string[] GetBuildScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }

        if (scenes.Count == 0)
        {
            Log.w("No scenes found in Build Settings, using all scenes in project", "ProjectBuilder");
            // Fallback: find all scenes in project
            string[] allScenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);
            scenes.AddRange(allScenes);
        }

        return scenes.ToArray();
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
        {
            Log.w($"Source directory not found: {sourceDir}", "ProjectBuilder");
            return;
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dirs)
        {
            string newDestDir = Path.Combine(destDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestDir);
        }
    }


    #endregion

    #region Data Classes

    [Serializable]
    public class BuildInfo
    {
        public string version;
        public string platform;
        public string buildTime;
        public string unityVersion;
        public long buildSizeBytes;
        public long buildSizeMB;
        public string result;
    }

    #endregion
}
