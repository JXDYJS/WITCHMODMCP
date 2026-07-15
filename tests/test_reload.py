"""Test C# tools after hot-reload."""
import json, time
from mcp_gateway.mod_client import ModConnection
mod = ModConnection(3100, "witch-mod-mcp-dev-2026")

tests = [
    ("set_card_pile", {"pile":"hand","action":"set","cards":["card_1","card_2"]}),
    ("play_card", {"index":0,"targetIndex":0}),
    ("set_fight_entity", {"target":"0","hp":50}),
]

for name, params in tests:
    resp = mod.call_tool(name, params)
    result = resp.get("result", resp)
    error = resp.get("error")
    if error:
        print(f"[FAIL] {name}: code={error.get('code')} msg={error.get('message','')[:100]}")
    else:
        ok = result.get("result") != "error"
        msg = result.get("message") or ""
        print(f"[{'OK' if ok else 'FAIL'}] {name}: {result.get('result','?')} {msg[:80]}")
