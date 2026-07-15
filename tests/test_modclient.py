"""Test mod_client.call_tool directly, mirroring what the gateway does."""
import json
from mcp_gateway.mod_client import ModConnection

mod = ModConnection(3100, "witch-mod-mcp-dev-2026")

tests = [
    ("set_card_pile", {"pile": "hand", "action": "set", "cards": ["card_1","card_2","card_3","card_4","card_5"]}),
    ("play_card", {"index": 0, "targetIndex": 0}),
    ("play_card", {"cardId": "card_3", "targetIndex": 0}),
    ("set_fight_entity", {"target": "player", "hp": 100}),
    ("set_fight_entity", {"target": "player", "shield": 50}),
    ("set_card_pile", {"pile": "draw", "action": "add", "cards": ["card_1","card_2"]}),
    ("set_card_pile", {"pile": "discard", "action": "clear"}),
]

for name, params in tests:
    resp = mod.call_tool(name, params)
    result = resp.get("result", resp)
    error = resp.get("error")
    msg = result.get("message") if isinstance(result, dict) else ""
    result_short = msg or json.dumps(result, ensure_ascii=False)[:120]
    status = "OK" if not error and result.get("result") != "error" else "FAIL"
    print(f"[{status}] {name:30s} {result_short}")
