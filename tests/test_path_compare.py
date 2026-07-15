"""Compare MCP gateway path vs direct HTTP path for combat tools."""
import http.client, json, sys
from mcp_gateway.mod_client import ModConnection

mod = ModConnection(3100, "witch-mod-mcp-dev-2026")

tests = [
    ("set_card_pile", {"pile": "hand", "action": "set", "cards": ["card_1","card_2","card_3"]}),
    ("play_card",     {"index": 0, "targetIndex": 0}),
    ("set_fight_entity", {"target": "player", "hp": 100}),
]

# Path A: mod_client.call_tool (same as gateway uses)
print("=== PATH A: gateway's mod_client.call_tool ===")
for name, params in tests:
    resp = mod.call_tool(name, params)
    result = resp.get("result", resp)
    error = resp.get("error")
    msg = result.get("message") if isinstance(result, dict) else ""
    if error:
        print(f"[FAIL] {name:25s} Error(code={error['code']}): {error['message'][:100]}")
    else:
        ok = result.get("result") != "error"
        brief = result.get("result") or msg or json.dumps(result)[:120]
        print(f"[{'OK' if ok else 'FAIL'}] {name:25s} {brief}")

# Path B: direct HTTP (same JSON body)
print("\n=== PATH B: direct HTTP with same body ===")
conn = http.client.HTTPConnection("localhost", 3100, timeout=5)
for name, params in tests:
    body = json.dumps({"jsonrpc":"2.0","id":1,"method":name,"params":params})
    conn.request("POST", "/", body, {"Content-Type":"application/json","Authorization":"Bearer witch-mod-mcp-dev-2026"})
    resp = conn.getresponse()
    data = json.loads(resp.read())
    result = data.get("Result") or {}
    err = data.get("Error") or {}
    msg = result.get("message") or err.get("message") or ""
    if err:
        print(f"[FAIL] {name:25s} Error: {err.get('message','?')[:100]}")
    else:
        ok = result.get("result") != "error"
        brief = result.get("result") or msg or json.dumps(result)[:120]
        print(f"[{'OK' if ok else 'FAIL'}] {name:25s} {brief}")
