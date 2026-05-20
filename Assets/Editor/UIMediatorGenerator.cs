using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class UIMediatorGenerator
{
    // Framework (Base layer) paths
    private const string UI_MEDIATOR_BASE_FW_PATH = "Assets/Scripts/FrameworkAssembly/UI/";
    private const string UI_CONST_FW_PATH = "Assets/Scripts/FrameworkAssembly/Const/UIConst.cs";

    // HotUpdate paths
    private const string UI_MEDIATOR_BASE_HOT_PATH = "Assets/Scripts/HotUpdateAssembly/UI/";
    private const string UI_CONST_HOT_PATH = "Assets/Scripts/HotUpdateAssembly/Const/HotUpdateUIConst.cs";

    private const string PREFAB_ROOT_BASE = "Assets/ProjectAssets/Base/UIAssets/Prefabs/";
    private const string PREFAB_ROOT_HOTUPDATE = "Assets/ProjectAssets/HotUpdate/UIAssets/Prefabs/";

    /// <summary>
    /// Detect whether the prefab belongs to Base (Framework) or HotUpdate assembly.
    /// </summary>
    private static bool IsHotUpdatePrefab(string prefabAssetPath)
    {
        string normalized = prefabAssetPath.Replace('\\', '/');
        return normalized.StartsWith(PREFAB_ROOT_HOTUPDATE);
    }

    /// <summary>
    /// Get mediator output directory based on prefab layer.
    /// </summary>
    private static string GetMediatorBasePath(string prefabAssetPath)
    {
        return IsHotUpdatePrefab(prefabAssetPath) ? UI_MEDIATOR_BASE_HOT_PATH : UI_MEDIATOR_BASE_FW_PATH;
    }

    /// <summary>
    /// Get the const class name for NAME reference (e.g. "UIConst" or "HotUpdateUIConst").
    /// </summary>
    private static string GetConstClassName(string prefabAssetPath)
    {
        return IsHotUpdatePrefab(prefabAssetPath) ? "HotUpdateUIConst" : "UIConst";
    }

    // Stores info parsed from a single Bind component on the prefab
    private struct BindInfo
    {
        public string varName;        // e.g. txtName
        public string rawNodeName;    // e.g. TxtName  (original, used for button callback naming)
        public string componentType;  // e.g. Text
        public string bindTypeName;   // e.g. TextBind
        public bool isButton;
        public string localVarName;   // e.g. bind_txtName  (local temp variable in foreach)
    }

    // ──────────────────────────────────────────────
    // Menu validation: only active for .prefab files
    // ──────────────────────────────────────────────
    [MenuItem("Assets/Generate UI Mediator", true)]
    private static bool ValidateGenerateUIMediator()
    {
        Object selected = Selection.activeObject;
        if (selected == null) return false;
        string path = AssetDatabase.GetAssetPath(selected);
        return path.EndsWith(".prefab");
    }

    // ──────────────────────────────────────────────
    // Entry point
    // ──────────────────────────────────────────────
    [MenuItem("Assets/Generate UI Mediator")]
    private static void GenerateUIMediator()
    {
        Object selected = Selection.activeObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Error", "No prefab selected!", "OK");
            return;
        }

        string prefabAssetPath = AssetDatabase.GetAssetPath(selected);
        if (!prefabAssetPath.EndsWith(".prefab"))
        {
            EditorUtility.DisplayDialog("Error", "Selected object is not a prefab!", "OK");
            return;
        }

        string prefabName = Path.GetFileNameWithoutExtension(prefabAssetPath);
        string className  = GenerateClassName(prefabName);
        string constName  = className.Replace("Mediator", "");
        string constClass = GetConstClassName(prefabAssetPath);
        string scriptDir  = GetMediatorBasePath(prefabAssetPath) + prefabName + "/";
        string scriptPath = scriptDir + className + ".cs";
        bool isHotUpdate   = IsHotUpdatePrefab(prefabAssetPath);

        // Relative prefab path for const registration
        string prefabRootPath = isHotUpdate ? PREFAB_ROOT_HOTUPDATE : PREFAB_ROOT_BASE;
        string relativePrefabPath = prefabAssetPath.StartsWith(prefabRootPath)
            ? prefabAssetPath.Substring(prefabRootPath.Length)
            : prefabName + ".prefab";

        // ── Scan prefab for Bind components ──
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        if (prefabRoot == null)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to load prefab: {prefabAssetPath}", "OK");
            return;
        }

        List<BindInfo> binds;
        if (!CollectBindInfos(prefabRoot, out binds))
            return;

        if (!Directory.Exists(scriptDir))
            Directory.CreateDirectory(scriptDir);

        bool fileExists = File.Exists(scriptPath);
        if (fileExists)
        {
            // ── Update mode: patch existing script ──
            string existingContent = File.ReadAllText(scriptPath);
            string updatedContent  = UpdateExistingScript(existingContent, binds);
            File.WriteAllText(scriptPath, updatedContent, new System.Text.UTF8Encoding(false));
        }
        else
        {
            // ── Create mode: full generation ──
            string scriptContent = GenerateScriptContent(className, constName, binds, constClass);
            File.WriteAllText(scriptPath, scriptContent, new System.Text.UTF8Encoding(false));

            // Patch the relevant const file on first creation
            PatchConstRegistration(constName, relativePrefabPath, isHotUpdate);
        }

        AssetDatabase.Refresh();

        string mode = fileExists ? "Updated" : "Generated";
        EditorUtility.DisplayDialog("Success",
            $"UI Mediator {mode}!\n\nScript : {className}.cs\nBinds  : {binds.Count}" +
            (fileExists ? "\n\nUpdated: UI Components / InitUIComponents / InitClickEvents / new button callbacks" : $"\nConstant: {constName}\nPrefab  : {relativePrefabPath}"),
            "OK");

        Object scriptAsset = AssetDatabase.LoadAssetAtPath<Object>(scriptPath);
        Selection.activeObject = scriptAsset;
        EditorGUIUtility.PingObject(scriptAsset);
    }

    // ──────────────────────────────────────────────
    // Class name generation
    // ──────────────────────────────────────────────
    private static string GenerateClassName(string prefabName)
    {
        if (prefabName.StartsWith("UI"))
            prefabName = prefabName.Substring(2);

        string[] suffixes = { "Panel", "View", "Window" };
        foreach (string suffix in suffixes)
        {
            if (prefabName.EndsWith(suffix))
            {
                prefabName = prefabName.Substring(0, prefabName.Length - suffix.Length);
                break;
            }
        }
        return "UI" + prefabName + "Mediator";
    }

    // ──────────────────────────────────────────────
    // Collect all IUIBind components from the prefab (depth-first)
    // ──────────────────────────────────────────────
    private static bool CollectBindInfos(GameObject root, out List<BindInfo> result)
    {
        result = new List<BindInfo>();
        var seen = new HashSet<string>(); // duplicate variable name detection

        // GetComponentsInChildren respects hierarchy order (depth-first)
        IUIBind[] allBinds = root.GetComponentsInChildren<IUIBind>(true);

        foreach (IUIBind bind in allBinds)
        {
            Component comp = bind as Component;
            if (comp == null) continue;

            string nodeName      = comp.gameObject.name;
            string componentType = bind.TargetComponentType.Name;
            string bindTypeName  = bind.GetType().Name;
            string varName       = ToCamelCase(nodeName);
            bool   isButton      = bindTypeName == "ButtonBind";

            // Duplicate detection
            if (seen.Contains(varName))
            {
                EditorUtility.DisplayDialog("Naming Conflict",
                    $"Variable name conflict detected!\n\nTwo or more nodes produce the same variable name: \"{varName}\"\n\nPlease rename the duplicate nodes and try again.",
                    "OK");
                result = null;
                return false;
            }
            seen.Add(varName);

            result.Add(new BindInfo
            {
                varName       = varName,
                rawNodeName   = nodeName,
                componentType = componentType,
                bindTypeName  = bindTypeName,
                isButton      = isButton
            });
        }
        return true;
    }

    // ──────────────────────────────────────────────
    // Convert node name to camelCase variable name
    // e.g.  "TxtName"   -> "txtName"
    //       "Btn_Close" -> "btnClose"
    //       "HP Slider" -> "hpSlider"
    // ──────────────────────────────────────────────
    private static string ToCamelCase(string name)
    {
        // Split on underscore, space, hyphen, then remove empty entries
        string[] parts = Regex.Split(name, @"[_\s\-]+");
        var sb = new StringBuilder();
        bool isFirst = true;
        foreach (string raw in parts)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            if (isFirst)
            {
                // First word: force first char lowercase, keep rest as-is
                sb.Append(char.ToLower(raw[0]));
                if (raw.Length > 1) sb.Append(raw.Substring(1));
                isFirst = false;
            }
            else
            {
                // Subsequent words: force first char uppercase, keep rest as-is
                sb.Append(char.ToUpper(raw[0]));
                if (raw.Length > 1) sb.Append(raw.Substring(1));
            }
        }
        return sb.ToString();
    }

    // ──────────────────────────────────────────────
    // Script content generation
    // ──────────────────────────────────────────────
    private static string GenerateScriptContent(string className, string constName, List<BindInfo> binds, string constClass)
    {
        bool hasBinds   = binds != null && binds.Count > 0;
        bool hasButtons = false;
        if (hasBinds)
        {
            foreach (var b in binds)
                if (b.isButton) { hasButtons = true; break; }
        }

        var sb = new StringBuilder();

        // ── usings ──
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.UI;");
        sb.AppendLine();

        // ── class declaration ──
        sb.AppendLine($"public class {className} : UIMediatorBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public new const string NAME = {constClass}.{constName};");
        sb.AppendLine();

        // ── #region UI Components ──
        sb.AppendLine("    #region UI Components");
        if (hasBinds)
        {
            foreach (var b in binds)
                sb.AppendLine($"    [SerializeField] private {b.componentType} {b.varName};");
        }
        else
        {
            sb.AppendLine("    // No Bind components found on prefab");
        }
        sb.AppendLine("    #endregion");
        sb.AppendLine();

        // ── constructor ──
        sb.AppendLine($"    public {className}(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)");
        sb.AppendLine("        : base(mediatorName, viewComponent, layer, isReuseView)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ── InitUIComponents ──
        sb.AppendLine("    protected override void InitUIComponents()");
        sb.AppendLine("    {");
        sb.Append(BuildInitUIComponentsBody(binds, hasBinds, true));
        sb.AppendLine("    }");
        sb.AppendLine();

        // ── RegisterUIEvents ──
        sb.AppendLine("    protected override void RegisterUIEvents()");
        sb.AppendLine("    {");
        sb.AppendLine("        base.RegisterUIEvents();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ── UnRegisterUIEvents ──
        sb.AppendLine("    protected override void UnRegisterUIEvents()");
        sb.AppendLine("    {");
        sb.AppendLine("        base.UnRegisterUIEvents();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ── InitClickEvents (only generated when binds exist) ──
        if (hasBinds)
        {
            sb.AppendLine("    private void InitClickEvents()");
            sb.AppendLine("    {");
            if (hasButtons)
            {
                foreach (var b in binds)
                {
                    if (!b.isButton) continue;
                    string callbackName = "On" + Regex.Replace(b.rawNodeName, @"[_\s\-]+", "") + "Click";
                    sb.AppendLine($"        if ({b.varName} != null)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            {b.varName}.onClick.AddListener({callbackName});");
                    sb.AppendLine("        }");
                }
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // ── Button callbacks ──
        if (hasButtons)
        {
            foreach (var b in binds)
            {
                if (!b.isButton) continue;
                string callbackName = "On" + Regex.Replace(b.rawNodeName, @"[_\s\-]+", "") + "Click";
                sb.AppendLine($"    private void {callbackName}()");
                sb.AppendLine("    {");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        // ── #region Update (Optional) ──
        sb.AppendLine("    #region Update (Optional)");
        sb.AppendLine("    // Uncomment if this UI needs Update functionality");
        sb.AppendLine("    // protected override bool NeedsUpdate() => true;");
        sb.AppendLine("    // protected override UpdateFrequency GetUpdateFrequency() => UpdateFrequency.Low;");
        sb.AppendLine("    // protected override UpdateType[] GetUpdateTypes() => new UpdateType[] { UpdateType.Update };");
        sb.AppendLine("    // protected override void OnUpdate(float deltaTime) { }");
        sb.AppendLine("    // protected override void OnFixedUpdate(float fixedDeltaTime) { }");
        sb.AppendLine("    // protected override void OnLateUpdate(float deltaTime) { }");
        sb.AppendLine("    #endregion");
        sb.AppendLine();

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ──────────────────────────────────────────────
    // Update existing script (partial patch mode)
    // Replaces:
    //   1. #region UI Components body
    //   2. InitUIComponents() method body
    //   3. InitClickEvents() method body
    //   4. Appends new button callback stubs (only if not already present)
    // Everything else in the file is left untouched.
    // ──────────────────────────────────────────────
    private static string UpdateExistingScript(string source, List<BindInfo> binds)
    {
        bool hasBinds   = binds != null && binds.Count > 0;
        bool hasButtons = hasBinds && binds.Exists(b => b.isButton);

        // ── 1. Replace #region UI Components body ──
        string newFieldBlock = BuildFieldBlock(binds, hasBinds);
        source = ReplaceRegionBody(source, "UI Components", newFieldBlock);

        // ── 2. Replace InitUIComponents() body ──
        string newInitBody = BuildInitUIComponentsBody(binds, hasBinds);
        source = ReplaceMethodBody(source, "InitUIComponents", newInitBody);

        // ── 3. Replace InitClickEvents() body ──
        //    If the method doesn't exist yet, inject it before the first button callback or before #region Update
        string newClickBody = BuildInitClickEventsBody(binds, hasButtons);
        if (Regex.IsMatch(source, @"private void InitClickEvents\s*\(\s*\)"))
        {
            source = ReplaceMethodBody(source, "InitClickEvents", newClickBody);
        }
        else
        {
            // Inject the whole method before #region Update (Optional)
            string newMethod = "    private void InitClickEvents()\n    {\n" + newClickBody + "    }\n\n";
            source = Regex.Replace(source,
                @"([ \t]*)#region Update \(Optional\)",
                newMethod + "$0");
        }

        // ── 4. Append missing button callback stubs ──
        if (hasButtons)
        {
            foreach (var b in binds)
            {
                if (!b.isButton) continue;
                string callbackName = "On" + Regex.Replace(b.rawNodeName, @"[_\s\-]+", "") + "Click";
                // Only add if not already defined anywhere in the file
                if (!Regex.IsMatch(source, $@"private void {Regex.Escape(callbackName)}\s*\("))
                {
                    string stub = $"    private void {callbackName}()\n    {{\n    }}\n\n";
                    // Insert before #region Update (Optional)
                    source = Regex.Replace(source,
                        @"([ \t]*)#region Update \(Optional\)",
                        stub + "$0");
                }
            }
        }

        return source;
    }

    // Build the lines that go inside #region UI Components (no region tags)
    private static string BuildFieldBlock(List<BindInfo> binds, bool hasBinds)
    {
        var sb = new StringBuilder();
        if (hasBinds)
        {
            foreach (var b in binds)
                sb.AppendLine($"    [SerializeField] private {b.componentType} {b.varName};");
        }
        else
        {
            sb.AppendLine("    // No Bind components found on prefab");
        }
        return sb.ToString();
    }

    // Build the body lines for InitUIComponents() (no braces)
    private static string BuildInitUIComponentsBody(List<BindInfo> binds, bool hasBinds, bool callInitClickEvents = true)
    {
        var sb = new StringBuilder();
        if (!hasBinds) return sb.ToString();

        var bindsByType   = new Dictionary<string, List<BindInfo>>();
        var bindTypeOrder = new List<string>();
        foreach (var b in binds)
        {
            if (!bindsByType.ContainsKey(b.bindTypeName))
            {
                bindsByType[b.bindTypeName] = new List<BindInfo>();
                bindTypeOrder.Add(b.bindTypeName);
            }
            bindsByType[b.bindTypeName].Add(b);
        }

        foreach (string bindType in bindTypeOrder)
        {
            List<BindInfo> group = bindsByType[bindType];
            if (group.Count == 1)
            {
                BindInfo b = group[0];
                sb.AppendLine($"        {b.varName} = viewTrans.GetComponentInChildren<{bindType}>(true).Component;");
            }
            else
            {
                string arrVar = $"all{bindType}s";
                sb.AppendLine($"        var {arrVar} = viewTrans.GetComponentsInChildren<{bindType}>(true);");
                sb.AppendLine($"        foreach (var bind in {arrVar})");
                sb.AppendLine("        {");
                sb.AppendLine("            switch (bind.gameObject.name)");
                sb.AppendLine("            {");
                foreach (var b in group)
                    sb.AppendLine($"                case \"{b.rawNodeName}\": {b.varName} = bind.Component; break;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }
        }
        if (callInitClickEvents)
            sb.AppendLine("        InitClickEvents();");
        return sb.ToString();
    }

    // Build the body lines for InitClickEvents() (no braces)
    private static string BuildInitClickEventsBody(List<BindInfo> binds, bool hasButtons)
    {
        var sb = new StringBuilder();
        if (!hasButtons) return sb.ToString();

        foreach (var b in binds)
        {
            if (!b.isButton) continue;
            string callbackName = "On" + Regex.Replace(b.rawNodeName, @"[_\s\-]+", "") + "Click";
            sb.AppendLine($"        if ({b.varName} != null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {b.varName}.onClick.AddListener({callbackName});");
            sb.AppendLine("        }");
        }
        return sb.ToString();
    }

    // Replace the body of a #region block.
    // Matches:  #region <name>\n[body lines]\n    #endregion
    // and replaces the body lines with newBody.
    private static string ReplaceRegionBody(string source, string regionName, string newBody)
    {
        // Match from #region NAME to #endregion (non-greedy)
        string pattern = $@"([ \t]*#region {Regex.Escape(regionName)}[ \t]*\r?\n).*?([ \t]*#endregion)";
        return Regex.Replace(source, pattern,
            m => m.Groups[1].Value + newBody + m.Groups[2].Value,
            RegexOptions.Singleline);
    }

    // Replace the body of a method (content between the outermost { }).
    // Finds:  [access] [ret] methodName(...)\n    {\n[body]\n    }
    // and replaces the body with newBody.
    private static string ReplaceMethodBody(string source, string methodName, string newBody)
    {
        // Find method signature: "void MethodName(...)" followed by opening brace
        string sigPattern = $@"([ \t]*(?:protected override|private|public|internal)?[ \t]*\w+[ \t]+{Regex.Escape(methodName)}\s*\([^)]*\)\s*\r?\n[ \t]*\{{)";
        Match m = Regex.Match(source, sigPattern);
        if (!m.Success) return source;

        int bodyStart = m.Index + m.Length; // position after opening brace's newline
        int braceCount = 1;
        int i = bodyStart;
        while (i < source.Length && braceCount > 0)
        {
            if (source[i] == '{') braceCount++;
            else if (source[i] == '}') braceCount--;
            i++;
        }
        // i now points to the character after the matching closing brace
        // The closing brace line includes leading whitespace + "}" + newline
        // Find the start of the closing brace line
        int closeLineStart = i - 1;
        while (closeLineStart > bodyStart && source[closeLineStart] != '\n')
            closeLineStart--;
        if (source[closeLineStart] == '\n') closeLineStart++;

        string before = source.Substring(0, bodyStart);
        string after = source.Substring(closeLineStart);
        return before + "\n" + newBody + after;
    }

    // ──────────────────────────────────────────────
    // Patch HotUpdateUIConst.cs or UIConst.cs
    // ──────────────────────────────────────────────
    private static void PatchConstRegistration(string constName, string prefabPath, bool isHotUpdate)
    {
        if (isHotUpdate)
            PatchHotUpdateUIConst(constName, prefabPath);
        else
            PatchFrameworkUIConst(constName, prefabPath);
    }

    private static void PatchFrameworkUIConst(string constName, string prefabPath)
    {
        if (!File.Exists(UI_CONST_FW_PATH))
        {
            Log.e($"UIConst.cs not found at {UI_CONST_FW_PATH}", "UIMediatorGenerator");
            return;
        }

        string content = File.ReadAllText(UI_CONST_FW_PATH);

        // Add constant if missing
        if (!Regex.IsMatch(content, $@"public const string {constName}\s*="))
        {
            string regionPattern = @"(#region UI Name Constants[^\n]*\n)";
            content = Regex.Replace(content, regionPattern,
                $"$1    public const string {constName} = \"{constName}\";\n");
        }

        // Add RegisterUI call in Init() if missing
        if (!Regex.IsMatch(content, $@"RegisterUI\({constName}, "))
        {
            string initInsertPattern = @"(\s+)(RegisterUI\(UIHotUpdate, )";
            content = Regex.Replace(content, initInsertPattern,
                $"$1RegisterUI({constName}, \"{prefabPath}\", UI_PREFAB_ROOT_BASE);\n$1$2");
        }

        File.WriteAllText(UI_CONST_FW_PATH, content, new System.Text.UTF8Encoding(false));
        Log.d($"Updated UIConst.cs with {constName}", "UIMediatorGenerator");
    }

    private static void PatchHotUpdateUIConst(string constName, string prefabPath)
    {
        if (!File.Exists(UI_CONST_HOT_PATH))
        {
            Log.e($"HotUpdateUIConst.cs not found at {UI_CONST_HOT_PATH}", "UIMediatorGenerator");
            return;
        }

        string content = File.ReadAllText(UI_CONST_HOT_PATH);

        // Add constant if missing
        if (!Regex.IsMatch(content, $@"public const string {constName}\s*="))
        {
            string regionPattern = @"(#region UI Name Constants[^\n]*\n)";
            content = Regex.Replace(content, regionPattern,
                $"$1    public const string {constName} = \"{constName}\";\n");
        }

        // Add RegisterUI call in RegisterTo() if missing
        if (!Regex.IsMatch(content, $@"RegisterUI\({constName}, "))
        {
            string registerInsertPattern = @"(uiConst\.RegisterUI\(UIShop, )";
            content = Regex.Replace(content, registerInsertPattern,
                $"uiConst.RegisterUI({constName}, \"{prefabPath}\", UI_ROOT);\n        $1");
        }

        File.WriteAllText(UI_CONST_HOT_PATH, content, new System.Text.UTF8Encoding(false));
        Log.d($"Updated HotUpdateUIConst.cs with {constName}", "UIMediatorGenerator");
    }

    // ══════════════════════════════════════════════════════════════
    // Item Mediator Generation (Sub-Mediator for list items)
    // ══════════════════════════════════════════════════════════════

    [MenuItem("Assets/Generate Item Mediators", true)]
    private static bool ValidateGenerateItemMediators()
    {
        Object selected = Selection.activeObject;
        if (selected == null) return false;
        string path = AssetDatabase.GetAssetPath(selected);
        return path.EndsWith(".prefab");
    }

    [MenuItem("Assets/Generate Item Mediators")]
    private static void GenerateItemMediatorsAuto()
    {
        GenerateItemMediators(null);
    }

    [MenuItem("Assets/Generate Item Mediator (Manual)", true)]
    private static bool ValidateGenerateItemMediatorManual()
    {
        return ValidateGenerateItemMediators();
    }

    [MenuItem("Assets/Generate Item Mediator (Manual)")]
    private static void GenerateItemMediatorManual()
    {
        string nodeName = EditorInputDialog.Show(
            "Generate Item Mediator",
            "Enter the item node name in the prefab:",
            "GoodItem");
        if (string.IsNullOrEmpty(nodeName)) return;
        GenerateItemMediators(new List<string> { nodeName });
    }

    private static void GenerateItemMediators(List<string> manualNodeNames)
    {
        Object selected = Selection.activeObject;
        string prefabAssetPath = AssetDatabase.GetAssetPath(selected);
        string prefabName = Path.GetFileNameWithoutExtension(prefabAssetPath);

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        if (prefabRoot == null)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to load prefab: {prefabAssetPath}", "OK");
            return;
        }

        string panelClassName = GenerateClassName(prefabName);
        string scriptDir = UI_MEDIATOR_BASE_HOT_PATH + prefabName + "/";

        if (!Directory.Exists(scriptDir))
            Directory.CreateDirectory(scriptDir);

        List<string> itemNodeNames;
        if (manualNodeNames != null && manualNodeNames.Count > 0)
        {
            itemNodeNames = manualNodeNames;
        }
        else
        {
            // Auto-scan: find child nodes whose name contains Cell or Item
            itemNodeNames = new List<string>();
            var allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t == prefabRoot.transform) continue;
                string name = t.gameObject.name;
                if (name.Contains("Cell") || name.Contains("Item"))
                {
                    if (!itemNodeNames.Contains(name))
                        itemNodeNames.Add(name);
                }
            }
        }

        if (itemNodeNames.Count == 0)
        {
            EditorUtility.DisplayDialog("No Item Nodes",
                "No item nodes found in prefab.\n\n" +
                "Auto-scan looks for nodes with 'Cell' or 'Item' in the name.\n" +
                "Use 'Generate Item Mediator (Manual)' to specify a node name manually.",
                "OK");
            return;
        }

        int generated = 0;
        foreach (string nodeName in itemNodeNames)
        {
            // Find the node in prefab
            Transform nodeTrans = prefabRoot.transform.Find(nodeName);
            if (nodeTrans == null)
            {
                // Deep search
                var allT = prefabRoot.GetComponentsInChildren<Transform>(true);
                nodeTrans = System.Array.Find(allT, t => t.gameObject.name == nodeName && t != prefabRoot.transform);
            }
            if (nodeTrans == null)
            {
                Debug.LogWarning($"[UIMediatorGenerator] Node '{nodeName}' not found in prefab, skipping");
                continue;
            }

            // Collect binds from this node
            List<BindInfo> binds;
            if (!CollectBindInfos(nodeTrans.gameObject, out binds))
                continue;

            string itemClassName = "UI" + prefabName.Replace("UI", "").Replace("Panel", "") + nodeName + "Mediator";
            string scriptPath = scriptDir + itemClassName + ".cs";

            bool fileExists = File.Exists(scriptPath);
            if (fileExists)
            {
                string existingContent = File.ReadAllText(scriptPath);
                string updatedContent = UpdateExistingScript(existingContent, binds);
                File.WriteAllText(scriptPath, updatedContent, new UTF8Encoding(false));
            }
            else
            {
                string scriptContent = GenerateItemMediatorContent(itemClassName, nodeName, binds);
                File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(false));
            }

            generated++;
            Debug.Log($"[UIMediatorGenerator] {(fileExists ? "Updated" : "Generated")} item mediator: {itemClassName}.cs");
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success",
            $"Generated/Updated {generated} item mediator(s) for {prefabName}.\n\n" +
            $"Output: {scriptDir}",
            "OK");
    }

    private static string GenerateItemMediatorContent(string className, string nodeName, List<BindInfo> binds)
    {
        bool hasBinds = binds != null && binds.Count > 0;
        bool hasButtons = hasBinds && binds.Exists(b => b.isButton);

        var sb = new StringBuilder();

        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.UI;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Sub-mediator for {nodeName} item.");
        sb.AppendLine("/// Managed by parent panel mediator.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {className} : UIMediatorBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public const string NAME_PREFIX = \"{className}_\";");
        sb.AppendLine();

        // UI Components
        sb.AppendLine("    #region UI Components");
        if (hasBinds)
        {
            foreach (var b in binds)
                sb.AppendLine($"    [SerializeField] private {b.componentType} {b.varName};");
        }
        sb.AppendLine("    #endregion");
        sb.AppendLine();

        // Data fields
        sb.AppendLine("    private System.Action _onClickCallback;");
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    public {className}(GameObject viewComponent, int layer)");
        sb.AppendLine($"        : base(NAME_PREFIX + viewComponent.GetInstanceID(), viewComponent, layer, false)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();

        // InitUIComponents
        sb.AppendLine("    protected override void InitUIComponents()");
        sb.AppendLine("    {");
        sb.Append(BuildInitUIComponentsBody(binds, hasBinds, false));
        sb.AppendLine("    }");
        sb.AppendLine();

        // RegisterUIEvents
        sb.AppendLine("    protected override void RegisterUIEvents()");
        sb.AppendLine("    {");
        sb.AppendLine("        base.RegisterUIEvents();");
        if (hasButtons)
        {
            foreach (var b in binds)
            {
                if (!b.isButton) continue;
                sb.AppendLine($"        if ({b.varName} != null)");
                sb.AppendLine($"            {b.varName}.onClick.AddListener(OnItemClick);");
            }
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // UnRegisterUIEvents
        sb.AppendLine("    protected override void UnRegisterUIEvents()");
        sb.AppendLine("    {");
        if (hasButtons)
        {
            foreach (var b in binds)
            {
                if (!b.isButton) continue;
                sb.AppendLine($"        if ({b.varName} != null)");
                sb.AppendLine($"            {b.varName}.onClick.RemoveListener(OnItemClick);");
            }
        }
        sb.AppendLine("        base.UnRegisterUIEvents();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // SetData
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Bind data and callback. Called by parent mediator after creation.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public void SetData(System.Action onClickCallback)");
        sb.AppendLine("    {");
        sb.AppendLine("        _onClickCallback = onClickCallback;");
        sb.AppendLine("        RefreshView();");
        sb.AppendLine("    }");
        sb.AppendLine();

        // RefreshView
        sb.AppendLine("    private void RefreshView()");
        sb.AppendLine("    {");
        sb.AppendLine("        // TODO: Bind data to UI components here");
        if (hasBinds)
        {
            foreach (var b in binds)
            {
                if (b.componentType == "Text")
                    sb.AppendLine($"        // {b.varName}.text = ...;");
                else if (b.isButton)
                    sb.AppendLine($"        // {b.varName}.interactable = ...;");
            }
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // OnItemClick
        sb.AppendLine("    private void OnItemClick()");
        sb.AppendLine("    {");
        sb.AppendLine("        _onClickCallback?.Invoke();");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("}");
        return sb.ToString();
    }
}