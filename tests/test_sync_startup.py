"""Test: tools visible at handshake, no heartbeat wait."""
import json, subprocess, sys, time

proc = subprocess.Popen(
    [sys.executable, "-m", "mcp_gateway.server"],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
    text=True, encoding="utf-8", errors="replace", bufsize=1,
)
def send(o):
    proc.stdin.write(json.dumps(o) + "\n"); proc.stdin.flush()
def read(deadline=5):
    d = time.time() + deadline
    while time.time() < d:
        l = proc.stdout.readline()
        if l: return json.loads(l)
    return None

# Handshake
send({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}})
read(5)
send({"jsonrpc":"2.0","method":"notifications/initialized"})
time.sleep(0.3)

# tools/list immediately (no wait for heartbeat)
send({"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}})
m = read(5)
tools = m["result"]["tools"]
names = [t["name"] for t in tools]
print(f"Tools at handshake: {len(tools)}")
print(f"  sample: {names[:5]}")
print(f"  has get_scene_state: {'get_scene_state' in names}")
print(f"  has play_card: {'play_card' in names}")
print(f"  has set_fight_entity: {'set_fight_entity' in names}")

proc.stdin.close(); proc.terminate()
