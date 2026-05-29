using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Defines the three-tier AssetBundle packaging rules.
/// Scans project assets and assigns them to Base / Module / Hotfix layers
/// based on directory structure and resource type.
/// 
/// Directory layout (v2):
///   Assets/ProjectAssets/Base/       → AssetBundleLayer.Base   (shipped with app)
///   Assets/ProjectAssets/Module/     → AssetBundleLayer.Module  (per-feature, loaded on demand)
///   Assets/ProjectAssets/HotUpdate/  → AssetBundleLayer.Hotfix (downloaded at runtime)
/// 
/// Naming convention: {layer}_{module}_{resType}.ab
/// </summary>
public class AssetBundleBuildRules
{
    #region Build Rules Definition

    /// <summary>
    /// Mapping: directory pattern → (layer, moduleName)
    /// Order matters — first match wins.
    /// 
    /// General-purpose Base/ HotUpdate/ rules replace the old per-UI rules.
    /// </summary>
    private static readonly List<BundleRule> Rules = new List<BundleRule>
    {
        // === Base Layer (shipped with app) ===
        new BundleRule { dirPattern = "Assets/ProjectAssets/Base/",      layer = AssetBundleLayer.Base,    moduleName = "base",    resType = AssetBundleResourceType.Unknown },

        // === HotUpdate Layer (downloaded at runtime) ===
        new BundleRule { dirPattern = "Assets/ProjectAssets/HotUpdate/", layer = AssetBundleLayer.Hotfix,  moduleName = "hotupdate", resType = AssetBundleResourceType.Unknown },

        // === Lua Scripts ===
        new BundleRule { dirPattern = "Assets/LuaScripts/BuiltIn",    layer = AssetBundleLayer.Module, moduleName = "lua",     resType = AssetBundleResourceType.Lua },
        new BundleRule { dirPattern = "Assets/LuaScripts/HotUpdate",  layer = AssetBundleLayer.Hotfix,  moduleName = "lua",     resType = AssetBundleResourceType.Lua },
    };

    #endregion

    #region Public API

    /// <summary>
    /// Scan all project assets and group them into AssetBundleBuild definitions.
    /// Returns a list ready for BuildPipeline.BuildAssetBundles.
    /// </summary>
    public static List<AssetBundleBuild> GenerateBuildDefinitions()
    {
        // Group assets by bundle name
        Dictionary<string, List<string>> bundleToAssets = new Dictionary<string, List<string>>();

        // Get all assets that can be bundled
        string[] allAssets = AssetDatabase.GetAllAssetPaths()
            .Where(p => IsBundleableAsset(p))
            .ToArray();

        foreach (string assetPath in allAssets)
        {
            BundleRule rule = MatchRule(assetPath);
            if (rule == null) continue;

            // If the rule has Unknown resType, infer from the actual file extension
            AssetBundleResourceType resType = rule.resType;
            if (resType == AssetBundleResourceType.Unknown)
            {
                resType = InferResourceType(assetPath);
            }

            string bundleName = BuildBundleName(rule.layer, rule.moduleName, resType);

            if (!bundleToAssets.ContainsKey(bundleName))
            {
                bundleToAssets[bundleName] = new List<string>();
            }
            bundleToAssets[bundleName].Add(assetPath);
        }

        // Convert to AssetBundleBuild array
        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        foreach (var kv in bundleToAssets)
        {
            builds.Add(new AssetBundleBuild
            {
                assetBundleName = kv.Key,
                assetNames = kv.Value.ToArray()
            });
        }

        Log.d($"Generated {builds.Count} AssetBundle definitions from {allAssets.Length} assets", "AssetBundleBuildRules");
        return builds;
    }

    /// <summary>
    /// Build AssetBundles using the defined rules and generate enhanced manifest.
    /// </summary>
    public static void BuildWithRules(BuildTarget target, string outputDir, string version)
    {
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        List<AssetBundleBuild> buildDefs = GenerateBuildDefinitions();

        if (buildDefs.Count == 0)
        {
            Log.w("No AssetBundle definitions generated, skipping build", "AssetBundleBuildRules");
            return;
        }

        // Build with LZ4 compression
        UnityEngine.AssetBundleManifest unityManifest = BuildPipeline.BuildAssetBundles(
            outputDir,
            buildDefs.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression, // LZ4
            target
        );

        if (unityManifest == null)
        {
            Log.e("AssetBundle build failed", "AssetBundleBuildRules");
            return;
        }

        // Generate enhanced manifest
        AssetBundleManifest enhancedManifest = GenerateEnhancedManifest(
            outputDir, version, target, buildDefs, unityManifest
        );

        string manifestPath = Path.Combine(outputDir, "manifest.json");
        string json = JsonUtility.ToJson(enhancedManifest, true);
        File.WriteAllText(manifestPath, json);

        Log.d($"AssetBundles built: {outputDir} ({buildDefs.Count} bundles)", "AssetBundleBuildRules");
    }

    #endregion

    #region Rule Matching

    private static BundleRule MatchRule(string assetPath)
    {
        foreach (var rule in Rules)
        {
            if (assetPath.StartsWith(rule.dirPattern, StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }
        return null;
    }

    /// <summary>
    /// Infer AssetBundleResourceType from file extension.
    /// Used when a BundleRule does not specify an explicit resType.
    /// </summary>
    private static AssetBundleResourceType InferResourceType(string assetPath)
    {
        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        return ext switch
        {
            ".prefab" => AssetBundleResourceType.Prefab,
            ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" => AssetBundleResourceType.Texture,
            ".mat" => AssetBundleResourceType.Material,
            ".shader" => AssetBundleResourceType.Shader,
            ".ogg" or ".wav" or ".mp3" => AssetBundleResourceType.Audio,
            ".anim" or ".controller" => AssetBundleResourceType.Animation,
            ".json" or ".asset" or ".bytes" => AssetBundleResourceType.Config,
            ".lua" => AssetBundleResourceType.Lua,
            ".ttf" or ".otf" => AssetBundleResourceType.Font,
            ".mp4" or ".mov" or ".webm" or ".avi" => AssetBundleResourceType.Video,
            _ => AssetBundleResourceType.Unknown,
        };
    }

    private static string BuildBundleName(AssetBundleLayer layer, string moduleName, AssetBundleResourceType resType)
    {
        string layerPrefix = layer.ToString().ToLowerInvariant();
        string resSuffix = resType.ToString().ToLowerInvariant();
        return $"{layerPrefix}_{moduleName}_{resSuffix}.ab";
    }

    private static bool IsBundleableAsset(string path)
    {
        // Skip directories, .meta, .cs scripts, and Editor-only assets
        if (Directory.Exists(path)) return false;
        if (path.EndsWith(".meta")) return false;
        if (path.EndsWith(".cs")) return false;
        if (path.Contains("/Editor/")) return false;
        if (path.StartsWith("Packages/")) return false;

        // Only include known asset types
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".prefab" => true,
            ".png" => true,
            ".jpg" => true,
            ".jpeg" => true,
            ".tga" => true,
            ".psd" => true,
            ".json" => true,
            ".asset" => true,
            ".mat" => true,
            ".shader" => true,
            ".ogg" => true,
            ".wav" => true,
            ".mp3" => true,
            ".anim" => true,
            ".controller" => true,
            ".ttf" => true,
            ".otf" => true,
            ".lua" => true,
            ".bytes" => true,
            ".mp4" => true,
            ".mov" => true,
            ".webm" => true,
            ".avi" => true,
            _ => false,
        };
    }

    #endregion

    #region Enhanced Manifest Generation

    private static AssetBundleManifest GenerateEnhancedManifest(
        string outputDir,
        string version,
        BuildTarget target,
        List<AssetBundleBuild> buildDefs,
        UnityEngine.AssetBundleManifest unityManifest)
    {
        var manifest = new AssetBundleManifest
        {
            version = version,
            buildTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            platform = GetPlatformName(target),
            bundles = new List<AssetBundleEntry>(),
            dependencyGraph = new Dictionary<string, List<string>>()
        };

        // Build dependency graph from Unity's manifest
        string[] allBundleNames = unityManifest.GetAllAssetBundles();
        foreach (string bundleName in allBundleNames)
        {
            string[] deps = unityManifest.GetDirectDependencies(bundleName);
            manifest.dependencyGraph[bundleName] = new List<string>(deps);
        }

        // Build enhanced entries
        foreach (string bundleName in allBundleNames)
        {
            string bundlePath = Path.Combine(outputDir, bundleName);
            if (!File.Exists(bundlePath))
            {
                Log.w($"Bundle file not found: {bundlePath}", "AssetBundleBuildRules");
                continue;
            }

            FileInfo fi = new FileInfo(bundlePath);

            // Parse bundle name to extract layer/module/resType
            ParseBundleName(bundleName, out AssetBundleLayer layer, out string moduleName, out AssetBundleResourceType resType);

            // Find the build definition for this bundle to get asset list
            var buildDef = buildDefs.FirstOrDefault(d => d.assetBundleName == bundleName);
            List<string> assetList = buildDef.assetNames?.ToList() ?? new List<string>();

            var entry = new AssetBundleEntry
            {
                name = bundleName,
                md5 = ComputeHash<MD5CryptoServiceProvider>(bundlePath),
                sha256 = ComputeHash<SHA256CryptoServiceProvider>(bundlePath),
                crc32 = ComputeCRC32(bundlePath),
                size = fi.Length,
                layer = layer,
                compression = BundleCompression.LZ4,
                resourceType = resType,
                dependencies = manifest.dependencyGraph.ContainsKey(bundleName)
                    ? manifest.dependencyGraph[bundleName]
                    : new List<string>(),
                assets = assetList
            };

            manifest.bundles.Add(entry);
        }

        return manifest;
    }

    private static void ParseBundleName(string bundleName, out AssetBundleLayer layer, out string moduleName, out AssetBundleResourceType resType)
    {
        layer = AssetBundleLayer.Base;
        moduleName = "unknown";
        resType = AssetBundleResourceType.Unknown;

        // Format: {layer}_{module}_{resType}.ab
        string nameWithoutExt = Path.GetFileNameWithoutExtension(bundleName);
        string[] parts = nameWithoutExt.Split('_');

        if (parts.Length >= 1 && Enum.TryParse(parts[0], true, out AssetBundleLayer parsedLayer))
        {
            layer = parsedLayer;
        }

        if (parts.Length >= 3 && Enum.TryParse(parts[parts.Length - 1], true, out AssetBundleResourceType parsedType))
        {
            resType = parsedType;
            moduleName = string.Join("_", parts, 1, parts.Length - 2);
        }
        else if (parts.Length >= 2)
        {
            moduleName = string.Join("_", parts, 1, parts.Length - 1);
        }
    }

    #endregion

    #region Hash Helpers

    private static string ComputeHash<T>(string filePath) where T : HashAlgorithm, new()
    {
        using (var hash = new T())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] result = hash.ComputeHash(stream);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < result.Length; i++)
            {
                sb.Append(result[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }

    private static string ComputeCRC32(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        {
            uint crc = 0xFFFFFFFF;
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                crc ^= (uint)b;
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc >> 1) ^ (0xEDB88320 & (uint)(-(crc & 1)));
                }
            }
            return (~crc).ToString("X8");
        }
    }

    private static string GetPlatformName(BuildTarget target)
    {
        return target switch
        {
            BuildTarget.StandaloneWindows64 => "Windows",
            BuildTarget.StandaloneWindows => "Windows",
            BuildTarget.Android => "Android",
            BuildTarget.iOS => "iOS",
            _ => target.ToString(),
        };
    }

    #endregion

    #region Data Classes

    private class BundleRule
    {
        public string dirPattern;
        public AssetBundleLayer layer;
        public string moduleName;
        public AssetBundleResourceType resType;
    }

    #endregion
}
