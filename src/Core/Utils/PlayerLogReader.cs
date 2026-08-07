using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WitchModMCP.Utils
{
    internal static class PlayerLogReader
    {
        private const int MaxTailBytes = 128 * 1024;
        private const int ChunkSize = 8192;

        internal static void ReadAndEnqueue()
        {
            try
            {
                string logPath = Application.consoleLogPath;
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                    return;

                long length = new FileInfo(logPath).Length;
                if (length == 0)
                    return;

                int tailBytes = (int)Math.Min(length, MaxTailBytes);
                using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                byte[] buffer = new byte[tailBytes];
                if (length > tailBytes)
                    fs.Seek(-tailBytes, SeekOrigin.End);
                int read = fs.Read(buffer, 0, buffer.Length);
                string content = Encoding.UTF8.GetString(buffer, 0, read);

                int totalChunks = (content.Length + ChunkSize - 1) / ChunkSize;
                for (int i = 0; i < totalChunks; i++)
                {
                    int start = i * ChunkSize;
                    int len = Math.Min(ChunkSize, content.Length - start);
                    string chunk = content.Substring(start, len);

                    string entry = i == 0
                        ? $"[Player.log History - {Path.GetFileName(logPath)} ({length} bytes)]\n{chunk}"
                        : chunk;

                    LogBuffer.Enqueue(entry, null, "PlayerLog");
                }
            }
            catch (Exception ex)
            {
                LogBuffer.Enqueue($"[PlayerLogReader] Error: {ex.Message}", ex.ToString(), "Error");
            }
        }
    }
}
