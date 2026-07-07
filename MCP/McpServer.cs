using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WitchModMCP.MCP
{
    public class McpServer : IDisposable
    {
        HttpListener _listener;

        public void Start(int port)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            Task.Run(ListenLoop);
        }

        private async Task ListenLoop()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await ProcessRequest(context);
                }
                catch
                {
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.AppendHeader("Access-Control-Allow-Origin", "*");

            if (context.Request.HttpMethod != "POST")
            {
                var error = Encoding.UTF8.GetBytes("{\"error\":\"Use POST\"}");
                context.Response.OutputStream.Write(error, 0, error.Length);
                context.Response.Close();
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            string responseJson = await McpRouter.HandleRequest(body ?? "{}");

            byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        public void Dispose()
        {
            _listener?.Stop();
        }
    }
}
