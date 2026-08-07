using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WitchModMCP.Terminal;

namespace WitchModMCP.MCP
{
    public class McpServer : IDisposable
    {
        private const int MaxBodyBytes = 4 * 1024 * 1024;

        private HttpListener _listener;
        private WebSocketServer _wsServer;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private volatile bool _shuttingDown;
        private int _port;

        public bool IsShuttingDown => _shuttingDown;

        /// <summary>Mod root directory (set by Entry before Start).</summary>
        public string ModDirectory { get; set; }

        public void Start(int port)
        {
            _port = port;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                _ = Task.Run(ListenLoop);
                _wsServer = new WebSocketServer();
                _wsServer.Start(port + 1);
                Commands.Log(WitchModMCPEntry.MOD_TAG, $"[McpServer] Listening on http://localhost:{port}/");
            }
            catch (HttpListenerException ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] Failed to start HTTP listener on port {port}: {ex.Message}");
                _listener?.Close();
                _listener = null;
                _cts?.Cancel();
                _cts = null;
            }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] Unexpected error starting listener on port {port}: {ex.Message}");
                _listener?.Close();
                _listener = null;
                _cts?.Cancel();
                _cts = null;
            }
        }

        private async Task ListenLoop()
        {
            while (!_shuttingDown && !_cts.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    if (_shuttingDown || _cts.IsCancellationRequested)
                    {
                        try { context.Response?.Close(); } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] context.Close: {ex.Message}"); }
                        break;
                    }
                    _ = ProcessRequest(context);
                }
                catch (HttpListenerException) when (_shuttingDown || _cts.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_shuttingDown || _cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_shuttingDown && !_cts.IsCancellationRequested)
                        Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ListenLoop error: {ex.Message}");
                    else
                        break;
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            // Reject requests during shutdown to prevent hangs
            if (_shuttingDown)
            {
                try
                {
                    context.Response.StatusCode = 503;
                    var err = Encoding.UTF8.GetBytes("{\"status\":\"shutting_down\"}");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ProcessRequest shutdown 503: {ex.Message}"); }
                finally
                {
                    try { context.Response?.Close(); } catch (Exception ex2) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ProcessRequest close: {ex2.Message}"); }
                }
                return;
            }

            try
            {
                context.Response.ContentType = "application/json; charset=utf-8";

                // ──── GET /ping — alive check (no auth required) ────
                if (context.Request.HttpMethod == "GET" &&
                    context.Request.Url.AbsolutePath.TrimEnd('/') == "/ping")
                {
                    var pong = Encoding.UTF8.GetBytes(
                        $"{{\"status\":\"ok\",\"port\":{_port}}}");
                    context.Response.ContentLength64 = pong.Length;
                    context.Response.OutputStream.Write(pong, 0, pong.Length);
                    return;
                }

                // ──── POST /heartbeat — heartbeat (no auth required) ────
                if (context.Request.HttpMethod == "POST" &&
                    context.Request.Url.AbsolutePath.TrimEnd('/') == "/heartbeat")
                {
                    string hbBody;
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        hbBody = await ReadWithLimit(reader, MaxBodyBytes);
                    }

                    string hbResponse = await McpRouter.HandleHeartbeat(
                        _port,
                        hbBody ?? "{}");

                    var hbBuf = Encoding.UTF8.GetBytes(hbResponse);
                    context.Response.ContentLength64 = hbBuf.Length;
                    await context.Response.OutputStream.WriteAsync(hbBuf, 0, hbBuf.Length);
                    return;
                }

                // ──── GET /console — serve xterm.js terminal page ────
                if (context.Request.HttpMethod == "GET" &&
                    context.Request.Url.AbsolutePath.TrimEnd('/') == "/console")
                {
                    await ServeConsolePage(context);
                    return;
                }

                // ──── GET /xterm/* — static files from Terminal/node_modules/@xterm ────
                if (context.Request.HttpMethod == "GET" &&
                    context.Request.Url.AbsolutePath.StartsWith("/xterm/"))
                {
                    await ServeXtermStatic(context);
                    return;
                }

                // ──── GET /xterm-readline/* — static files from Terminal/node_modules/xterm-readline ────
                if (context.Request.HttpMethod == "GET" &&
                    context.Request.Url.AbsolutePath.StartsWith("/xterm-readline/"))
                {
                    await ServeXtermReadlineStatic(context);
                    return;
                }

                // ──── Only POST for tool calls ────
                if (context.Request.HttpMethod != "POST")
                {
                    var error = Encoding.UTF8.GetBytes("{\"error\":\"Use POST\"}");
                    context.Response.OutputStream.Write(error, 0, error.Length);
                    return;
                }

                // ──── Body size limit ────
                if (context.Request.ContentLength64 > MaxBodyBytes)
                {
                    context.Response.StatusCode = 413;
                    var error = Encoding.UTF8.GetBytes("{\"error\":\"Request body too large\"}");
                    context.Response.ContentLength64 = error.Length;
                    context.Response.OutputStream.Write(error, 0, error.Length);
                    return;
                }

                // ──── Read body ────
                string body;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    body = await ReadWithLimit(reader, MaxBodyBytes);
                }

                if (body == null)
                {
                    context.Response.StatusCode = 413;
                    var error = Encoding.UTF8.GetBytes("{\"error\":\"Request body too large\"}");
                    context.Response.ContentLength64 = error.Length;
                    context.Response.OutputStream.Write(error, 0, error.Length);
                    return;
                }

                // ──── Route ────
                string responseJson = await McpRouter.HandleRequest(body ?? "{}");

                byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                try
                {
                    var errorJson = $"{{\"jsonrpc\":\"2.0\",\"error\":{{\"code\":-32603,\"message\":\"Server error: {ex.Message}\"}}}}";
                    var errorBytes = Encoding.UTF8.GetBytes(errorJson);
                    context.Response.ContentLength64 = errorBytes.Length;
                    context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                }
                catch (Exception ex2)
                {
                    Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ProcessRequest error json: {ex2.Message}");
                }
            }
            finally
            {
                try
                {
                    context.Response?.Close();
                }
                catch (Exception ex)
                {
                    Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ProcessRequest finally close: {ex.Message}");
                }
            }
        }

        private async Task ServeConsolePage(HttpListenerContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(ModDirectory))
                {
                    context.Response.StatusCode = 500;
                    var err = Encoding.UTF8.GetBytes("Server not configured (ModDirectory missing)");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                var htmlPath = Path.Combine(ModDirectory, "Terminal", "console.html");
                if (!File.Exists(htmlPath))
                {
                    context.Response.StatusCode = 404;
                    var err = Encoding.UTF8.GetBytes($"console.html not found at {htmlPath}");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                var html = File.ReadAllText(htmlPath, Encoding.UTF8);
                var buf = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buf.Length;
                await context.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
                Commands.Log(WitchModMCPEntry.MOD_TAG, "[McpServer] Served console.html");
            }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ServeConsolePage error: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    var err = Encoding.UTF8.GetBytes($"Internal error: {ex.Message}");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                }
                catch { }
            }
        }

        private async Task ServeXtermStatic(HttpListenerContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(ModDirectory))
                {
                    context.Response.StatusCode = 500;
                    var err = Encoding.UTF8.GetBytes("Server not configured");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                // URL: /xterm/xterm/lib/xterm.js
                // Maps to: {ModDirectory}/Terminal/node_modules/@xterm/xterm/lib/xterm.js
                var relativePath = context.Request.Url.AbsolutePath
                    .TrimStart('/')
                    .Substring("xterm/".Length); // "xterm/lib/xterm.js"

                var filePath = Path.Combine(ModDirectory, "Terminal", "node_modules", "@xterm", relativePath);
                var resolved = Path.GetFullPath(filePath);

                // Security: ensure resolved path is under the expected base
                var baseDir = Path.GetFullPath(Path.Combine(ModDirectory, "Terminal", "node_modules", "@xterm"));
                if (!resolved.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 403;
                    var err = Encoding.UTF8.GetBytes("Forbidden");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                if (!File.Exists(resolved))
                {
                    context.Response.StatusCode = 404;
                    var err = Encoding.UTF8.GetBytes("Not found");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                var ext = Path.GetExtension(resolved).ToLowerInvariant();
                var mime = ext switch
                {
                    ".css" => "text/css; charset=utf-8",
                    ".js" => "application/javascript; charset=utf-8",
                    ".mjs" => "application/javascript; charset=utf-8",
                    ".map" => "application/octet-stream",
                    ".png" => "image/png",
                    ".woff2" => "font/woff2",
                    _ => "application/octet-stream"
                };

                var content = File.ReadAllBytes(resolved);
                context.Response.ContentType = mime;
                context.Response.ContentLength64 = content.Length;
                await context.Response.OutputStream.WriteAsync(content, 0, content.Length);
            }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ServeXtermStatic error: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    var err = Encoding.UTF8.GetBytes($"Internal error: {ex.Message}");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                }
                catch { }
            }
        }

        private async Task ServeXtermReadlineStatic(HttpListenerContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(ModDirectory))
                {
                    context.Response.StatusCode = 500;
                    var err = Encoding.UTF8.GetBytes("Server not configured");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                // URL: /xterm-readline/lib/readline.js
                // Maps to: {ModDirectory}/Terminal/node_modules/xterm-readline/lib/readline.js
                var relativePath = context.Request.Url.AbsolutePath
                    .TrimStart('/')
                    .Substring("xterm-readline/".Length);

                var filePath = Path.Combine(ModDirectory, "Terminal", "node_modules", "xterm-readline", relativePath);
                var resolved = Path.GetFullPath(filePath);

                // Security: ensure resolved path is under the expected base
                var baseDir = Path.GetFullPath(Path.Combine(ModDirectory, "Terminal", "node_modules", "xterm-readline"));
                if (!resolved.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 403;
                    var err = Encoding.UTF8.GetBytes("Forbidden");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                if (!File.Exists(resolved))
                {
                    context.Response.StatusCode = 404;
                    var err = Encoding.UTF8.GetBytes("Not found");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                    return;
                }

                var ext = Path.GetExtension(resolved).ToLowerInvariant();
                var mime = ext switch
                {
                    ".css" => "text/css; charset=utf-8",
                    ".js" => "application/javascript; charset=utf-8",
                    ".mjs" => "application/javascript; charset=utf-8",
                    ".map" => "application/octet-stream",
                    ".png" => "image/png",
                    ".woff2" => "font/woff2",
                    _ => "application/octet-stream"
                };

                var content = File.ReadAllBytes(resolved);
                context.Response.ContentType = mime;
                context.Response.ContentLength64 = content.Length;
                await context.Response.OutputStream.WriteAsync(content, 0, content.Length);
            }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ServeXtermReadlineStatic error: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    var err = Encoding.UTF8.GetBytes($"Internal error: {ex.Message}");
                    context.Response.ContentLength64 = err.Length;
                    context.Response.OutputStream.Write(err, 0, err.Length);
                }
                catch { }
            }
        }

        private static async Task<string> ReadWithLimit(StreamReader reader, int maxBytes)
        {
            var buffer = new char[8192];
            var sb = new StringBuilder(maxBytes);
            int totalRead = 0;

            while (totalRead < maxBytes + 1)
            {
                int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (charsRead == 0) break;
                totalRead += charsRead;
                if (totalRead > maxBytes) return null;
                sb.Append(buffer, 0, charsRead);
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _shuttingDown = true;

            // Cancel the listen token first — this signals the ListenLoop
            try { _cts?.Cancel(); } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] Dispose Cancel: {ex.Message}"); }

            try { _wsServer?.Dispose(); } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] Dispose ws: {ex.Message}"); }

            // Close forcefully aborts pending GetContextAsync() calls
            try { _listener?.Close(); } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] Dispose Close: {ex.Message}"); }

            _listener = null;
            try { _cts?.Dispose(); } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] Dispose: {ex.Message}"); }
            _cts = null;
        }
    }
}
