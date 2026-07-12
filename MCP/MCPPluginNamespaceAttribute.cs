using System;

namespace WitchModMCP.MCP
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MCPPluginNamespaceAttribute : Attribute
    {
        public string RelativeFolderPath { get; }

        public MCPPluginNamespaceAttribute(string relativeFolderPath)
        {
            RelativeFolderPath = relativeFolderPath;
        }
    }
}
