using System;

namespace WitchModMCP.MCP
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class MCPSkillNamespaceAttribute : Attribute
    {
        public string RelativeFolderPath { get; }

        public MCPSkillNamespaceAttribute(string relativeFolderPath)
        {
            RelativeFolderPath = relativeFolderPath;
        }
    }
}
