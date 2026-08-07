using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class DecompileSourceTool : IMcpTool
    {
        public string Name => "decompile_source";
        public string Description => "反编译指定游戏 DLL 到 outputDir，按 DLL hash 分目录缓存。默认反编译 Witch.dll + Witch.Core.dll，可通过 dlls 参数指定其他 DLL（在 Managed 目录下查找，或传绝对路径）。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["outputDir"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "反编译缓存根目录。每个 DLL 按 hash 分到 {outputDir}/{hash}/ 子目录下"
                },
                ["dlls"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "要反编译的 DLL 列表（文件名或绝对路径），默认 [\"Witch.dll\", \"Witch.Core.dll\"]",
                },
                ["force"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "强制重新反编译所有 DLL，即使 hash 匹配缓存",
                    ["default"] = false
                },
                ["clean"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "清理 outputDir 中过期的 hash 缓存目录（dlls 中没有的 DLL 的旧缓存）",
                    ["default"] = false
                }
            },
            ["required"] = new JArray { "outputDir" }
        };

        private static readonly Regex HashDirPattern = new(@"^[0-9a-f]{64}$", RegexOptions.Compiled);

        public Task<JToken> Execute(JToken args)
        {
            return Task.Run<JToken>(() =>
            {
                var result = new JObject();
                var log = new List<string>();

                try
                {
                    string outputDir = args?["outputDir"]?.Value<string>();
                    if (string.IsNullOrEmpty(outputDir))
                    {
                        result["error"] = "outputDir is required";
                        return result;
                    }

                    var asmName = GetType().Assembly.GetName().Name;
                    var asmDir = McpToolPlugin.GetAssemblyDirectory(asmName);
                    if (asmDir == null)
                    {
                        result["error"] = "Cannot resolve assembly directory";
                        return result;
                    }

                    string modDir;
                    if (asmDir.EndsWith("Scripts", StringComparison.OrdinalIgnoreCase))
                        modDir = Path.GetDirectoryName(asmDir);
                    else
                        modDir = asmDir;

                    var dataDir = Path.GetDirectoryName(Path.GetDirectoryName(modDir));
                    var managedDir = Path.Combine(dataDir, "Managed");

                    var dllNames = args?["dlls"]?.ToObject<string[]>()
                        ?? new[] { "Witch.dll", "Witch.Core.dll" };
                    if (dllNames.Length == 0)
                    {
                        result["error"] = "dlls is empty";
                        return result;
                    }

                    var targets = new List<(string Name, string Path)>();
                    foreach (var entry in dllNames)
                    {
                        var resolved = ResolveDllPath(entry, managedDir);
                        if (resolved == null)
                        {
                            result["error"] = $"DLL not found: {entry}";
                            return result;
                        }
                        targets.Add((Path.GetFileName(resolved), resolved));
                    }

                    bool force = args?["force"]?.Value<bool>() ?? false;
                    bool clean = args?["clean"]?.Value<bool>() ?? false;

                    var decompileDll = Path.Combine(modDir, "mcp_plugins", "decompile", "publish", "Decompile.dll");
                    if (!File.Exists(decompileDll))
                    {
                        result["error"] = $"Decompile plugin not found at {decompileDll}";
                        return result;
                    }

                    outputDir = Path.GetFullPath(outputDir);

                    if (clean && Directory.Exists(outputDir))
                        CleanStaleCache(outputDir, targets, log);

                    var manifestPath = Path.Combine(outputDir, ".decompile_manifest.json");
                    var manifest = LoadManifest(manifestPath);

                    var targetHashes = new Dictionary<string, string>();
                    foreach (var t in targets)
                        targetHashes[t.Name] = HashFile(t.Path);

                    var freshTargets = new List<string>();
                    var launchedPids = new List<int>();
                    var stillRunningPids = new List<int>();

                    foreach (var t in targets)
                    {
                        var hash = targetHashes[t.Name];
                        var targetDir = Path.Combine(outputDir, hash);

                        var storedHash = manifest.Value<string>(t.Name);
                        var storedDir  = manifest.Value<string>(t.Name + "Dir");

                        bool cacheHit = !force
                            && storedHash == hash
                            && storedDir  == hash
                            && Directory.Exists(targetDir);

                        if (cacheHit)
                        {
                            freshTargets.Add(t.Name);
                            continue;
                        }

                        var lockPath = Path.Combine(outputDir, $".decompile_{hash}.lock");
                        if (File.Exists(lockPath))
                        {
                            try
                            {
                                var lockData = JObject.Parse(File.ReadAllText(lockPath));
                                var pid = lockData.Value<int>("processId");
                                try
                                {
                                    using var existing = Process.GetProcessById(pid);
                                    if (!existing.HasExited)
                                    {
                                        stillRunningPids.Add(pid);
                                        log.Add($"{t.Name}: already running (pid={pid})");
                                        continue;
                                    }
                                }
                                catch (ArgumentException ex)
                                {
                                    Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[DecompileSourceTool] pid parse: {ex.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[DecompileSourceTool] process check: {ex.Message}");
                            }

                            var successFile = Path.Combine(targetDir, ".SUCCESS");
                            if (File.Exists(successFile))
                            {
                                File.Delete(lockPath);
                                manifest[t.Name] = hash;
                                manifest[t.Name + "Dir"] = hash;
                                freshTargets.Add(t.Name);
                                log.Add($"{t.Name}: recovered from finished process (hash={hash})");
                            }
                            else
                            {
                                log.Add($"{t.Name}: previous process failed, remove lock and retry");
                                File.Delete(lockPath);
                                if (Directory.Exists(targetDir))
                                {
                                    try { Directory.Delete(targetDir, recursive: true); }
                                    catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[DecompileSourceTool] clean stale dir: {ex.Message}"); }
                                }
                            }
                            continue;
                        }

                        Directory.CreateDirectory(targetDir);

                        var psi = new ProcessStartInfo("dotnet")
                        {
                            Arguments = $"\"{decompileDll}\" \"{t.Path}\" \"{targetDir}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        };

                        using var proc = Process.Start(psi);
                        if (proc == null)
                        {
                            log.Add($"  FAILED: could not start dotnet for {t.Name}");
                            continue;
                        }

                        File.WriteAllText(lockPath, new JObject
                        {
                            ["processId"] = proc.Id,
                            ["dllName"] = t.Name,
                            ["hash"] = hash,
                            ["targetDir"] = targetDir,
                        }.ToString());

                        launchedPids.Add(proc.Id);
                        log.Add($"{t.Name}: started (pid={proc.Id}, hash={hash})");
                    }

                    if (freshTargets.Count == targets.Count)
                    {
                        manifest["lastDecompileTime"] = DateTime.Now.ToString("O");
                        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
                        File.WriteAllText(manifestPath, manifest.ToString());
                        result["status"] = "fresh";
                        result["manifestPath"] = manifestPath;
                        result["log"] = new JArray(log);
                        return result;
                    }

                    if (stillRunningPids.Count > 0)
                    {
                        result["status"] = "running";
                        result["processIds"] = new JArray(stillRunningPids);
                        result["outputDir"] = outputDir;
                        result["log"] = new JArray(log);
                        return result;
                    }

                    if (launchedPids.Count > 0)
                    {
                        result["status"] = "started";
                        result["processIds"] = new JArray(launchedPids);
                        result["outputDir"] = outputDir;
                        result["log"] = new JArray(log);
                        return result;
                    }

                    manifest["lastDecompileTime"] = DateTime.Now.ToString("O");
                    Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
                    File.WriteAllText(manifestPath, manifest.ToString());

                    result["status"] = "decompiled";
                    result["manifestPath"] = manifestPath;
                    result["log"] = new JArray(log);

                    var dlls = new JObject();
                    foreach (var t in targets)
                    {
                        var h = manifest.Value<string>(t.Name);
                        var d = manifest.Value<string>(t.Name + "Dir");
                        dlls[t.Name] = new JObject
                        {
                            ["hash"] = h ?? "",
                            ["dir"] = d ?? ""
                        };
                    }
                    result["dlls"] = dlls;
                }
                catch (Exception ex)
                {
                    result["error"] = $"Decompilation failed: {ex.Message}";
                }

                return result;
            });
        }

        private static void CleanStaleCache(string outputDir, List<(string Name, string Path)> targets, List<string> log)
        {
            var validHashes = new HashSet<string>(targets.Select(t =>
            {
                using var s = File.OpenRead(t.Path);
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
            }));

            var removed = 0;
            foreach (var dir in Directory.EnumerateDirectories(outputDir))
            {
                var name = Path.GetFileName(dir);
                if (!HashDirPattern.IsMatch(name)) continue;
                if (!validHashes.Contains(name))
                {
                    try { Directory.Delete(dir, recursive: true); removed++; }
                    catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[DecompileSourceTool] clean hash dir: {ex.Message}"); }
                }
            }

            foreach (var f in Directory.EnumerateFiles(outputDir, ".decompile_*.lock"))
            {
                try { File.Delete(f); }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[DecompileSourceTool] clean lock: {ex.Message}"); }
            }

            log.Add($"clean: removed {removed} stale hash dirs + stale locks");
        }

        private static string ResolveDllPath(string entry, string managedDir)
        {
            if (File.Exists(entry))
                return Path.GetFullPath(entry);

            if (entry.Contains(Path.DirectorySeparatorChar) || entry.Contains(Path.AltDirectorySeparatorChar))
            {
                var combined = Path.Combine(managedDir, entry);
                if (File.Exists(combined))
                    return combined;
                return null;
            }

            var withExt = entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? entry : entry + ".dll";
            var inManaged = Path.Combine(managedDir, withExt);
            if (File.Exists(inManaged))
                return inManaged;

            return null;
        }

        private static string HashFile(string path)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static JObject LoadManifest(string path)
        {
            if (!File.Exists(path)) return new JObject();
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch { return new JObject(); }
        }
    }
}
