using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ScreenshotTool : IMcpTool
    {
        public string Name => "get_screenshot";
        public string Description => "获取当前游戏画面截图，返回 base64 编码的 PNG 图片和尺寸信息。用于查看游戏当前视觉状态。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["format"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "图片编码格式：png 或 jpg，默认 png",
                    ["default"] = "png",
                    ["enum"] = new JArray { "png", "jpg" }
                },
                ["quality"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "JPG 质量 (1-100)，仅 format=jpg 时生效，默认 75",
                    ["default"] = 75
                }
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string format = args?["format"]?.Value<string>() ?? "png";
            int quality = args?["quality"]?.Value<int>() ?? 75;
            quality = Math.Max(1, Math.Min(100, quality));

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                try
                {
                    Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
                    if (texture == null)
                    {
                        result["error"] = "CaptureScreenshotAsTexture 返回 null，可能游戏未完成首帧渲染";
                        return (JToken)result;
                    }

                    int width = texture.width;
                    int height = texture.height;

                    byte[] bytes;
                    string mimeType;

                    if (format == "jpg")
                    {
                        bytes = ImageConversion.EncodeToJPG(texture, quality);
                        mimeType = "image/jpeg";
                    }
                    else
                    {
                        bytes = ImageConversion.EncodeToPNG(texture);
                        mimeType = "image/png";
                    }

                    UnityEngine.Object.Destroy(texture);

                    result["mimeType"] = mimeType;
                    result["base64"] = Convert.ToBase64String(bytes);
                    result["width"] = width;
                    result["height"] = height;
                    result["size"] = bytes.Length;
                }
                catch (Exception ex)
                {
                    result["error"] = $"截图失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }
}
