"""Check C# mod tool registry."""
import json, http.client
from mcp_gateway.mod_client import ModConnection

mod = ModConnection(3100, "witch-mod-mcp-dev-2026")
resp = mod.call_tool("list_tools", {})
tools = resp.get("result", {}).get("tools", [])
names = [t["name"] for t in tools]
print(f"C# tools: {len(tools)}")
print(f"has play_card: {'play_card' in names}")
print(f"has set_fight_entity: {'set_fight_entity' in names}")
print(f"has get_scene_state: {'get_scene_state' in names}")
