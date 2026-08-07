using System;
using WebSocketSharp.Server;

namespace WitchModMCP.Terminal
{
    public class WebSocketServer : IDisposable
    {
        private WebSocketSharp.Server.WebSocketServer _server;
        private bool _disposed;

        public void Start(int port)
        {
            _server = new WebSocketSharp.Server.WebSocketServer(port);
            _server.AddWebSocketService<LuaConsoleBehavior>("/ws");
            _server.Start();

            Commands.Log(WitchModMCPEntry.MOD_TAG, $"[WebSocketServer] Listening on ws://localhost:{port}/ws");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _server?.Stop();
                _server = null;
            }
            catch { }
        }
    }
}
