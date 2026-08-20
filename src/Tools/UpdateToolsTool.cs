using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    // Hot-reload WitchModMCP.Contracts.dll from a remote manifest hosted on GitHub Pages.
    // Framework DLL stays untouched — only the plugin DLL is replaced.
    public class UpdateToolsTool : IMcpTool
    {
        public string Name => "update_tools";
        public string Description => "Check the remote manifest for a newer toolset DLL, download it, verify SHA256, atomically replace WitchModMCP.Contracts.dll, and hot-reload. Any failure is logged as a warning and the running toolset keeps working.";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["force"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = false,
                    ["description"] = "Re-download even when remote version equals local version"
                }
            }
        };

        private const int ManifestTimeoutMs = 5000;
        private const int DownloadTimeoutMs = 30000;
        private const string ToolsDllName = "WitchModMCP.Contracts.dll";

        public Task<JToken> Execute(JToken args)
        {
            bool force = args?["force"]?.Value<bool>() ?? false;
            return RunUpdateAsync(force);
        }

        private static async Task<JToken> RunUpdateAsync(bool force)
        {
            var result = new JObject();
            var modDir = WitchModMCPEntry.ModDirectory;
            if (string.IsNullOrEmpty(modDir))
                return Skip(result, "no_mod_dir", "ModDirectory not set");

            var cfgPath = Path.Combine(modDir, "ModConfig.json");
            string manifestUrl, localVersion;
            try
            {
                var cfg = JObject.Parse(File.ReadAllText(cfgPath));
                manifestUrl = cfg.Value<string>("UpdateManifestUrl") ?? "";
                localVersion = cfg.Value<string>("ToolsetVersion") ?? "0.0.0";
            }
            catch (Exception ex)
            {
                return Skip(result, "config_unreadable", ex.Message);
            }
            result["localVersion"] = localVersion;

            if (string.IsNullOrWhiteSpace(manifestUrl))
                return Skip(result, "no_manifest_url", "UpdateManifestUrl not set in ModConfig.json");

            string manifestJson;
            try { manifestJson = await HttpGetStringAsync(manifestUrl, ManifestTimeoutMs); }
            catch (Exception ex) { return Skip(result, "manifest_fetch_failed", ex.Message); }

            JObject manifest;
            try { manifest = JObject.Parse(manifestJson); }
            catch (Exception ex) { return Skip(result, "manifest_parse_failed", ex.Message); }

            var toolsObj = manifest["tools"] as JObject;
            if (toolsObj == null) return Skip(result, "manifest_invalid", "missing tools block");
            string dllUrl = toolsObj.Value<string>("url") ?? "";
            string expectedSha = (toolsObj.Value<string>("sha256") ?? "").ToLowerInvariant();
            long expectedSize = toolsObj.Value<long?>("size") ?? -1;
            if (string.IsNullOrEmpty(dllUrl) || string.IsNullOrEmpty(expectedSha))
                return Skip(result, "manifest_invalid", "missing url or sha256");

            string remoteVersion = manifest.Value<string>("version") ?? "";
            result["remoteVersion"] = remoteVersion;

            if (!force && string.Equals(remoteVersion, localVersion, StringComparison.Ordinal))
            {
                result["status"] = "up_to_date";
                return result;
            }

            byte[] payload;
            try { payload = await HttpGetBytesAsync(dllUrl, DownloadTimeoutMs); }
            catch (Exception ex) { return Skip(result, "download_failed", ex.Message); }

            if (expectedSize > 0 && payload.Length != expectedSize)
                return Skip(result, "size_mismatch",
                    $"expected {expectedSize} bytes, got {payload.Length}");

            string actualSha;
            using (var sha = SHA256.Create())
                actualSha = BitConverter.ToString(sha.ComputeHash(payload))
                    .Replace("-", "").ToLowerInvariant();
            if (!string.Equals(actualSha, expectedSha, StringComparison.Ordinal))
                return Skip(result, "hash_mismatch",
                    $"expected {Short(expectedSha)}, got {Short(actualSha)}");

            return await GameDispatcher.RunOnMainThread(() =>
                ApplyUpdate(modDir, cfgPath, remoteVersion, payload, result));
        }

        // Runs on main thread: writes .new, swaps the DLL, reloads tools, persists version.
        private static JToken ApplyUpdate(string modDir, string cfgPath,
            string remoteVersion, byte[] payload, JObject result)
        {
            var scriptsDir = Path.Combine(modDir, "Scripts");
            var dllPath = Path.Combine(scriptsDir, ToolsDllName);
            var newPath = dllPath + ".new";

            try
            {
                File.WriteAllBytes(newPath, payload);
                // Drop refs to old tool instances so the prior Assembly becomes GC-eligible.
                McpRouter.ClearTools();
                // File.Replace is atomic on NTFS and preserves destination ACLs.
                if (File.Exists(dllPath))
                    File.Replace(newPath, dllPath, null);
                else
                    File.Move(newPath, dllPath);
            }
            catch (Exception ex)
            {
                TryDelete(newPath);
                return Skip(result, "replace_failed",
                    $"{ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                McpRouter.ReloadAllTools();
                WitchModMCPEntry.ResyncLuaAssemblies();
            }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG,
                    $"[UpdateToolsTool] reload failed after replace: {ex.Message}");
                result["status"] = "replaced_reload_failed";
                result["reason"] = ex.Message;
                result["newVersion"] = remoteVersion;
                return result;
            }

            // Persist new version so next startup does not re-download the same bytes.
            try
            {
                var cfg = JObject.Parse(File.ReadAllText(cfgPath));
                cfg["ToolsetVersion"] = remoteVersion;
                File.WriteAllText(cfgPath, cfg.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG,
                    $"[UpdateToolsTool] persist ToolsetVersion: {ex.Message}");
            }

            result["status"] = "updated";
            result["newVersion"] = remoteVersion;
            result["toolCount"] = McpRouter.ToolCount;
            Commands.Log(WitchModMCPEntry.MOD_TAG,
                $"[UpdateToolsTool] updated to {remoteVersion}, toolCount={McpRouter.ToolCount}");
            return result;
        }

        private static JToken Skip(JObject result, string code, string reason)
        {
            Commands.Log(WitchModMCPEntry.MOD_TAG,
                $"[UpdateToolsTool] skipped: {code} ({reason})");
            result["status"] = "skipped";
            result["code"] = code;
            result["reason"] = reason;
            return result;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG,
                    $"[UpdateToolsTool] cleanup failed for {path}: {ex.Message}");
            }
        }

        private static string Short(string sha) =>
            sha.Length >= 8 ? sha.Substring(0, 8) : sha;

        private static async Task<string> HttpGetStringAsync(string url, int timeoutMs)
        {
            EnableTls12();
            using (var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) })
                return await client.GetStringAsync(url);
        }

        private static async Task<byte[]> HttpGetBytesAsync(string url, int timeoutMs)
        {
            EnableTls12();
            using (var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) })
                return await client.GetByteArrayAsync(url);
        }

        // Mono defaults vary across Unity versions — force TLS 1.2 so GitHub Pages works.
        private static void EnableTls12()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12
                    | SecurityProtocolType.Tls11;
            }
            catch { /* best-effort */ }
        }
    }
}
