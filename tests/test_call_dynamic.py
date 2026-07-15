"""E2E functional check for dynamically-registered tools through MCP gateway."""
import json, subprocess, sys, time, threading

proc = subprocess.Popen(
    [sys.executable, "-m", "mcp_gateway.server"],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
    text=True, encoding="utf-8", errors="replace", bufsize=1,
)

def send(o):
    proc.stdin.write(json.dumps(o) + "\n"); proc.stdin.flush()

def read_resp(deadline_s=10):
    deadline = time.time() + deadline_s
    while time.time() < deadline:
        line = proc.stdout.readline()
        if not line: continue
        return json.loads(line)
    return None

# Handshake
send({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}})
read_resp(5)
send({"jsonrpc":"2.0","method":"notifications/initialized"})

# Wait for list_changed
deadline = time.time() + 30
while time.time() < deadline:
    m = read_resp(2)
    if m and m.get("method") == "notifications/tools/list_changed":
        break
time.sleep(0.3)

# Send all test calls with sequential IDs
tests = [
    ("get_scene_state",       {}                                              ),
    ("get_game_data",         {}                                              ),
    ("list_commands",         {}                                              ),
    ("query_config",          {"tableName":"Card","limit":2}                  ),
    ("set_card_pile",         {"pile":"hand","action":"set","cards":["card_1","card_2","card_3"]}),
    ("play_card",             {"index":0,"targetIndex":0}                    ),
    ("set_fight_entity",      {"target":"player","hp":100}                   ),
    ("set_fight_entity",      {"target":"player","shield":30}                ),
]

for i, (name, args) in enumerate(tests):
    send({"jsonrpc":"2.0","id":i+100,"method":"tools/call","params":{"name":name,"arguments":args}})

passed = 0
failed = []

for i, (name, args) in enumerate(tests):
    m = read_resp(10)
    if m is None:
        failed.append(f"{name}: timeout"); continue
    r = m.get("result", {})
    is_err = r.get("isError", False)
    text = (r.get("content") or [{}])[0].get("text", "")
    if is_err:
        failed.append(f"{name}: isError=True text={text[:100]}")
    else:
        passed += 1
    brief = text.replace("\n"," ")[:80]
    print(f"[{'OK' if not is_err else 'FAIL':4s}] {name:30s} {brief}")

print(f"\n{passed}/{len(tests)} passed")
for f in failed:
    print(f"  FAIL: {f}")

proc.stdin.close(); proc.terminate()
