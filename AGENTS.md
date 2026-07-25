# WitchModMCP

WitchModMCP 是《魔女:终末旅途》的一个 Mod，通过 MCP 协议暴露游戏运行时状态，让 AI 代理能够实时读取游戏状态、导航菜单、管理战斗和检查运行时数据。

`witchSkill` 是该项目提供的 AI 指导知识库，包含游戏机制说明、工具用法、战斗策略和 Mod 开发指南。

## 查找 Skill

Skill 文件可能存在于以下位置：

1. **全局 skill 目录**（跨项目共享）：
   - opencode：`~/.config/opencode/agents/skills/witchSkill/`
   - Claude Code：`~/.claude/skills/witchSkill/`
2. **项目 skill 目录**（当前项目独有）：`.agents/skills/witchSkill/`
