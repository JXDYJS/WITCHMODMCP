import subprocess, json, sys, os, time, threading
os.chdir(os.path.dirname(os.path.abspath(__file__)))
env = os.environ.copy()
env["MCP_HEARTBEAT_INTERVAL"] = "30"

proc = subprocess.Popen(
    [sys.executable, "run_gateway.py"],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    env=env,
)

# Send one initialize message
req = {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}
payload = json.dumps(req, ensure_ascii=False)
proc.stdin.write(("Content-Length: %d\r\n\r\n%s" % (len(payload), payload)).encode())
proc.stdin.flush()

# Read response (with timeout)
import select
header = b""
deadline = time.time() + 10
while time.time() < deadline:
    c = proc.stdout.read(1)
    if not c:
        break
    header += c
    if header.endswith(b"\r\n\r\n"):
        break

if header.endswith(b"\r\n\r\n"):
    length = int(header.split(b":")[1].strip())
    body = proc.stdout.read(length)
    resp = json.loads(body.decode())
    result = resp.get("result", {})
    ok = "serverInfo" in result
    print("initialize:", "OK" if ok else "FAIL")
    if ok:
        print("  server:", result["serverInfo"])
else:
    print("No response header received")

proc.kill()
proc.wait()
os.remove(__file__)
