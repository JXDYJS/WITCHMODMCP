"""Direct C# mod test — bypass Python gateway."""
import http.client, json

conn = http.client.HTTPConnection("localhost", 3100, timeout=5)

tests = [
    ("play_card", {"index": 0, "targetIndex": 0}),
    ("set_fight_entity", {"target": "player", "hp": 100}),
    ("set_card_pile", {"pile": "hand", "action": "clear"}),
]

for i, (method, params) in enumerate(tests):
    body = json.dumps({"jsonrpc": "2.0", "id": i, "method": method, "params": params})
    conn.request("POST", "/", body, {"Content-Type": "application/json", "Authorization": "Bearer witch-mod-mcp-dev-2026"})
    resp = conn.getresponse()
    data = json.loads(resp.read())
    result = data.get("Result") or {}
    err = data.get("Error") or {}
    msg = (result.get("message") or err.get("message") or "?")[:80]
    has_r = "Result" in data
    print(f"id={i} method={method:20s} hasResult={has_r} msg={msg}")
