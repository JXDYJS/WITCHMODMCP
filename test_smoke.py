"""Check heartbeat + mod_client independently."""
import json
import subprocess
import sys
import time
import os

os.chdir(r'E:\Witch\WitchModMCP')
PYTHON = r'E:\miniconda\python.exe'

# Test 1: mod_client module import and config reading
print("=== Test 1: mod_client config ===")
from mcp_gateway.mod_client import ModConnection, read_mod_config, find_mod_config
cfg = read_mod_config()
print(f"Port: {cfg['port']}, Token: {repr(cfg['token'][:8])}..., Config: {cfg['config_path']}")
print()

# Test 2: heartbeat module import
print("=== Test 2: heartbeat import ===")
from mcp_gateway.heartbeat import HeartbeatManager
hb = HeartbeatManager(
    mod_conn=ModConnection(cfg['port'], cfg['token']),
    workspace_dir=os.getcwd(),
)
assert hasattr(hb, 'start')
assert hasattr(hb, 'stop')
assert hasattr(hb, 'connected')
assert hasattr(hb, 'first_heartbeat_done')
print("HeartbeatManager OK")
print()

# Test 3: server module import
print("=== Test 3: server import ===")
from mcp_gateway.server import mcp, check_mod_connected, log
print(f"FastMCP app: {mcp.name}")
print(f"check_mod_connected: {check_mod_connected.__name__}")
print()

# Test 4: Quick stdio test with a real process
print("=== Test 4: MCP initialize (end-to-end) ===")
proc = subprocess.Popen(
    [PYTHON, '-m', 'mcp_gateway.server'],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
)
time.sleep(0.8)

if proc.poll() is not None:
    stderr_raw = proc.stderr.read()
    print(f"FAIL: server exited with code {proc.returncode}")
    print(stderr_raw.decode('utf-8', errors='replace')[-500:])
    sys.exit(1)

# Send initialize
msg = json.dumps({
    "jsonrpc": "2.0", "id": 0, "method": "initialize",
    "params": {"protocolVersion": "2024-11-05", "capabilities": {},
               "clientInfo": {"name": "test", "version": "1.0"}}
}) + '\n'
proc.stdin.write(msg.encode('utf-8'))
proc.stdin.flush()
time.sleep(0.5)

stdout_line = proc.stdout.readline()
init_resp = json.loads(stdout_line)
print(f"  name: {init_resp['result']['serverInfo']['name']}")
print(f"  capabilities: {list(init_resp['result']['capabilities'].keys())}")
print(f"  protocol: {init_resp['result']['protocolVersion']}")
print("  PASS: initialize")

# Send initialized + tools/list
proc.stdin.write(b'{"jsonrpc":"2.0","method":"notifications/initialized"}\n')
proc.stdin.write(b'{"jsonrpc":"2.0","id":1,"method":"tools/list"}\n')
proc.stdin.flush()
time.sleep(0.5)

stdout_line2 = proc.stdout.readline()
tools_resp = json.loads(stdout_line2)
assert tools_resp['id'] == 1
assert tools_resp['result']['tools'] == []
print("  PASS: tools/list (empty)")

# Send resources/list
proc.stdin.write(b'{"jsonrpc":"2.0","id":2,"method":"resources/list"}\n')
proc.stdin.flush()
time.sleep(0.3)

stdout_line3 = proc.stdout.readline()
res_resp = json.loads(stdout_line3)
assert res_resp['result']['resources'] == []
print("  PASS: resources/list (empty)")

print()
print("=== ALL TESTS PASSED ===")

proc.stdin.close()
proc.terminate()
proc.wait(timeout=3)
