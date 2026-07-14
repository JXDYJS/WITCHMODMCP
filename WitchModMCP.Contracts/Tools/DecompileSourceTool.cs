using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class DecompileSourceTool : IMcpTool
    {
        public string Name => "decompile_source";
        public string Description => "反编译 Witch.dll / Witch.Core.dll 到指定目录，按 DLL hash 分目录管理。自动检测 hash 变化，已缓存的不会重复翻编。";
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
                ["force"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "强制重新反编译所有 DLL，即使 hash 匹配缓存",
                    ["default"] = false
                }
            },
            ["required"] = new JArray { "outputDir" }
        };

        public Task<JToken> Execute(JToken args)
        {
            return Task.Run<JToken>(() =>
            {
                var result = new JObject();
                var log = new List<string>();

                try
                {
                    string outputDir = args?["outputDir"]?.Value<string>();
                    bool force = args?["force"]?.Value<bool>() ?? false;

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

                    // asmDir = Mods/.../DeveloperTools/Scripts; one up for mod root
                    string modDir;
                    if (asmDir.EndsWith("Scripts", StringComparison.OrdinalIgnoreCase))
                        modDir = Path.GetDirectoryName(asmDir);
                    else
                        modDir = asmDir;

                    var dataDir = Path.GetDirectoryName(Path.GetDirectoryName(modDir));
                    var managedDir = Path.Combine(dataDir, "Managed");
                    var witchDll = Path.Combine(managedDir, "Witch.dll");
                    var coreDll = Path.Combine(managedDir, "Witch.Core.dll");

                    if (!File.Exists(witchDll) || !File.Exists(coreDll))
                    {
                        result["error"] = $"Game DLLs not found in {managedDir}";
                        return result;
                    }

                    outputDir = Path.GetFullPath(outputDir);

                    var decompileProj = Path.Combine(modDir, "mcp_plugins", "decompile", "Decompile.csproj");
                    if (!File.Exists(decompileProj))
                    {
                        result["error"] = $"Decompile project not found at {decompileProj}";
                        return result;
                    }

                    var manifestPath = Path.Combine(outputDir, ".decompile_manifest.json");
                    var manifest = LoadManifest(manifestPath);

                    var targets = new[]
                    {
                        new { Name = "Witch.dll",  Path = witchDll },
                        new { Name = "Witch.Core.dll", Path = coreDll },
                    };

                    bool anyDecompiled = false;

                    foreach (var t in targets)
                    {
                        var hash = HashFile(t.Path);
                        var targetDir = Path.Combine(outputDir, hash);

                        var storedHash = manifest.Value<string>(t.Name);
                        var storedDir  = manifest.Value<string>(t.Name + "Dir");

                        bool cacheHit = !force
                            && storedHash == hash
                            && storedDir  == hash
                            && Directory.Exists(targetDir);

                        if (cacheHit)
                        {
                            log.Add($"{t.Name}: fresh (hash={hash})");
                            continue;
                        }

                        log.Add($"{t.Name}: decompiling (hash={hash})...");
                        Directory.CreateDirectory(targetDir);

                        var psi = new ProcessStartInfo("dotnet")
                        {
                            Arguments = $"run --project \"{decompileProj}\" -- \"{t.Path}\" \"{targetDir}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8,
                        };

                        using var proc = Process.Start(psi);
                        if (proc == null)
                        {
                            log.Add($"  FAILED: could not start dotnet");
                            continue;
                        }

                        var stdout = proc.StandardOutput.ReadToEndAsync();
                        var stderr = proc.StandardError.ReadToEndAsync();

                        if (!proc.WaitForExit(180_000))
                        {
                            proc.Kill();
                            log.Add($"  TIMEOUT (180s)");
                            continue;
                        }

                        if (proc.ExitCode != 0)
                        {
                            log.Add($"  EXIT CODE {proc.ExitCode}");
                            if (!string.IsNullOrEmpty(stderr.Result))
                                log.Add($"  STDERR: {stderr.Result.Trim()}");
                            continue;
                        }

                        manifest[t.Name] = hash;
                        manifest[t.Name + "Dir"] = hash;
                        anyDecompiled = true;
                        log.Add($"  DONE -> {targetDir}");
                    }

                    manifest["lastDecompileTime"] = DateTime.Now.ToString("o");
                    Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
                    File.WriteAllText(manifestPath, manifest.ToString());

                    result["status"] = anyDecompiled ? "decompiled" : "fresh";
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
