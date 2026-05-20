using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProtoServer
{
    /// <summary>
    /// Config table loader for server side.
    /// Supports JSON and Lua modes, switchable at runtime.
    /// </summary>
    public static class ConfigLoader
    {
        public enum LoadMode
        {
            Json,
            Lua
        }

        private static LoadMode _mode = LoadMode.Json;
        private static readonly Dictionary<Type, object> _cache = new Dictionary<Type, object>();

        /// <summary>
        /// Set load mode. Call before loading configs.
        /// </summary>
        public static void SetMode(LoadMode mode)
        {
            _mode = mode;
            Console.WriteLine($"[ConfigLoader] Mode set to: {mode}");
        }

        /// <summary>
        /// Load config by type. Auto-resolves path from type name.
        /// JSON: DesignConfig/Json/{TypeName}.json
        /// Lua:  DesignConfig/Lua/{TypeName}.lua
        /// </summary>
        public static List<T> Load<T>() where T : class
        {
            Type type = typeof(T);
            if (_cache.ContainsKey(type))
            {
                Console.WriteLine($"[ConfigLoader] Config already loaded: {type.Name}");
                return (List<T>)_cache[type];
            }

            string baseDir = GetDesignConfigDir();
            List<T> result;

            if (_mode == LoadMode.Json)
            {
                string path = Path.Combine(baseDir, "Json", type.Name + ".json");
                result = LoadJson<T>(path);
            }
            else
            {
                string path = Path.Combine(baseDir, "Lua", type.Name + ".lua");
                result = LoadLua<T>(path);
            }

            _cache[type] = result;
            Console.WriteLine($"[ConfigLoader] Loaded config: {type.Name} ({result.Count} items) [{_mode}]");
            return result;
        }

        /// <summary>
        /// Load config from explicit JSON path.
        /// </summary>
        public static List<T> LoadJson<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[ConfigLoader] [ERROR] JSON file not found: {filePath}");
                return new List<T>();
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                var wrapper = JsonConvert.DeserializeObject<ConfigWrapper<T>>(json);
                return wrapper?.items ?? new List<T>();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ConfigLoader] [ERROR] Failed to parse JSON: {filePath}\n{e.Message}");
                return new List<T>();
            }
        }

        /// <summary>
        /// Load config from explicit Lua path.
        /// Uses lua53.exe to execute the Lua file and parse the returned table as JSON.
        /// </summary>
        public static List<T> LoadLua<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[ConfigLoader] [ERROR] Lua file not found: {filePath}");
                return new List<T>();
            }

            try
            {
                string json = ExecuteLuaToJson(filePath);
                var wrapper = JsonConvert.DeserializeObject<ConfigWrapper<T>>(json);
                return wrapper?.items ?? new List<T>();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ConfigLoader] [ERROR] Failed to parse Lua: {filePath}\n{e.Message}");
                return new List<T>();
            }
        }

        /// <summary>
        /// Get a single config item by predicate.
        /// </summary>
        public static T Get<T>(Func<T, bool> predicate) where T : class
        {
            var list = Load<T>();
            foreach (var item in list)
            {
                if (predicate(item))
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Get all config items of type.
        /// </summary>
        public static List<T> GetAll<T>() where T : class
        {
            return Load<T>();
        }

        /// <summary>
        /// Clear all cached configs.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
            Console.WriteLine("[ConfigLoader] Cache cleared");
        }

        #region Internal

        private static string GetDesignConfigDir()
        {
            // DesignConfig is at project root, same level as ProtoServer folder
            // ProtoServer/ProtoServer/bin/Debug/ -> go up to PureMVC_Framework/
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Navigate: bin/Debug -> ProtoServer -> ProtoServer -> PureMVC_Framework
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            return Path.Combine(projectRoot, "DesignConfig");
        }

        /// <summary>
        /// Execute lua53 to convert Lua table to JSON.
        /// Lua file must return a table. We wrap it with a JSON encoder.
        /// </summary>
        private static string ExecuteLuaToJson(string luaFilePath)
        {
            // Build a Lua script that loads the config file and outputs JSON
            string tempLua = Path.GetTempFileName() + ".lua";
            try
            {
                string escapedPath = luaFilePath.Replace("\\", "\\\\");
                string wrapper = $@"
-- Simple JSON encoder for Lua config tables
local function json_encode(val)
    if type(val) == ""table"" then
        -- Check if array (all keys are sequential integers starting from 1)
        local isArray = true
        local maxIdx = 0
        for k, v in pairs(val) do
            if type(k) ~= ""number"" or k < 1 or k ~= math.floor(k) then
                isArray = false
                break
            end
            if k > maxIdx then maxIdx = k end
        end
        if isArray and maxIdx > 0 then
            local parts = {{}}
            for i = 1, maxIdx do
                if val[i] ~= nil then
                    table.insert(parts, json_encode(val[i]))
                else
                    table.insert(parts, ""null"")
                end
            end
            return ""["" .. table.concat(parts, "","") .. ""]""
        else
            local parts = {{}}
            for k, v in pairs(val) do
                local key = ""\"""" .. tostring(k) .. ""\""""
                table.insert(parts, key .. "":"" .. json_encode(v))
            end
            return ""{{"" .. table.concat(parts, "","") .. ""}}""
        end
    elseif type(val) == ""string"" then
        local escaped = val:gsub(""\\"", ""\\\\""):gsub(""\"""", ""\\\"""")
        return ""\"""" .. escaped .. ""\""""
    elseif type(val) == ""number"" then
        return tostring(val)
    elseif type(val) == ""boolean"" then
        return val and ""true"" or ""false""
    else
        return ""null""
    end
end

local config = dofile([[{escapedPath}]])
-- Convert Lua table to JSON array format
local items = {{}}
for k, v in pairs(config) do
    table.insert(items, v)
end
local result = {{ items = items }}
print(json_encode(result))
";
                File.WriteAllText(tempLua, wrapper, Encoding.UTF8);

                // Run lua53
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "lua53",
                    Arguments = $"\"{tempLua}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"[ConfigLoader] Lua stderr: {error}");
                    }

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"lua53 exited with code {process.ExitCode}: {error}");
                    }

                    return output.Trim();
                }
            }
            finally
            {
                if (File.Exists(tempLua))
                    File.Delete(tempLua);
            }
        }

        #endregion

        [Serializable]
        private class ConfigWrapper<T>
        {
            public List<T> items;
        }
    }
}
