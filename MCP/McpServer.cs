using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WitchModMCP.MCP
{
    public class McpServer : IDisposable
    {
        private const int MaxBodyBytes = 4 * 1024 * 1024;

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private bool _disposed;
        private int _port;
        private string _authToken;

        public void Start(int port, string authToken = null)
        {
            _port = port;
            _authToken = string.IsNullOrWhiteSpace(authToken) ? null : authToken;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                _cts = new CancellationTokenSource();
                _ = Task.Run(ListenLoop);

                if (_authToken != null)
                    Commands.Log(WitchModMCPEntry.MOD_TAG, $"[McpServer] Auth enabled, listening on http://localhost:{port}/");
                else
                    Commands.Log(WitchModMCPEntry.MOD_TAG, $"[McpServer] Auth disabled, listening on http://localhost:{port}/");
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
            while (!_cts.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequest(context);
                }
                catch (HttpListenerException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (!_cts.IsCancellationRequested)
                {
                    Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpServer] ListenLoop error: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            try
            {
                context.Response.ContentType = "application/json; charset=utf-8";

                // ──── GET /ping — alive check (no auth required) ────
                if (context.Request.HttpMethod == "GET" &&
                    context.Request.Url.AbsolutePath.TrimEnd('/') == "/ping")
                {
                    var pong = Encoding.UTF8.GetBytes(
                        $"{{\"status\":\"ok\",\"port\":{_port},\"auth\":{( _authToken != null ? "true" : "false")}}}");
                    context.Response.ContentLength64 = pong.Length;
                    context.Response.OutputStream.Write(pong, 0, pong.Length);
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

                // ──── Auth check (skip for ping) ────
                if (_authToken != null)
                {
                    string methodName = TryExtractMethod(body);
                    bool isPing = methodName != null &&
                                  methodName.Equals("ping", StringComparison.OrdinalIgnoreCase);

                    if (!isPing)
                    {
                        string authHeader = context.Request.Headers["Authorization"];
                        if (string.IsNullOrEmpty(authHeader) ||
                            !authHeader.Equals($"Bearer {_authToken}", StringComparison.Ordinal))
                        {
                            context.Response.StatusCode = 401;
                            var err = Encoding.UTF8.GetBytes(
                                $"{{\"jsonrpc\":\"2.0\",\"error\":{{\"code\":-32001,\"message\":\"Unauthorized: invalid or missing token\"}}}}");
                            context.Response.ContentLength64 = err.Length;
                            context.Response.OutputStream.Write(err, 0, err.Length);
                            return;
                        }
                    }
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
                catch
                {
                }
            }
            finally
            {
                try
                {
                    context.Response?.Close();
                }
                catch
                {
                }
            }
        }

        private static string TryExtractMethod(string body)
        {
            try
            {
                using var reader = new Newtonsoft.Json.JsonTextReader(
                    new System.IO.StringReader(body))
                {
                    CloseInput = false
                };

                while (reader.Read())
                {
                    if (reader.TokenType == Newtonsoft.Json.JsonToken.PropertyName &&
                        string.Equals("method", (string)reader.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        reader.Read();
                        return reader.Value?.ToString();
                    }
                }
            }
            catch
            {
            }
            return null;
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

            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listener?.Close();
            }
            catch
            {
            }

            _listener = null;
            _cts?.Dispose();
            _cts = null;
        }
    }
}
