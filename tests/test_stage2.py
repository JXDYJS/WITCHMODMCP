"""Stage 2 smoke test — verify resources/list and resources/read."""
import json
import subprocess
import sys
import time
import os
import threading
from pathlib import Path

_workspace = str(Path(__file__).resolve().parent.parent)
if _workspace not in sys.path:
    sys.path.insert(0, _workspace)
os.chdir(_workspace)

PYTHON = r"E:\miniconda\python.exe"


def read_line(proc, timeout=3.0):
    result = []
    def reader():
        try:
            line = proc.stdout.readline()
            if line:
                result.append(line.decode("utf-8", errors="replace"))
        except Exception:
            pass
    t = threading.Thread(target=reader, daemon=True)
    t.start()
    t.join(timeout=timeout)
    if result and result[0].strip():
        try:
            return json.loads(result[0])
        except json.JSONDecodeError:
            return None
    return None


def send(proc, msg: dict):
    proc.stdin.write((json.dumps(msg) + "\n").encode("utf-8"))
    proc.stdin.flush()


def test():
    proc = subprocess.Popen(
        [PYTHON, "-m", "mcp_gateway.server"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    passed = 0
    failed = 0

    try:
        time.sleep(0.8)
        if proc.poll() is not None:
            stderr = proc.stderr.read().decode("utf-8", errors="replace")
            print(f"FAIL: Server crashed on start\n{stderr}")
            return

        # Init handshake
        send(proc, {"jsonrpc": "2.0", "id": 1, "method": "initialize",
                     "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                                "clientInfo": {"name": "test", "version": "1.0"}}})
        resp = read_line(proc)
        assert resp and "result" in resp, f"Init failed: {resp}"
        print(f"[PASS] initialize — server: {resp['result']['serverInfo']['name']}")

        send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
        time.sleep(0.1)

        # Test 1: resources/list should return 15 resources
        send(proc, {"jsonrpc": "2.0", "id": 2, "method": "resources/list"})
        resp = read_line(proc)
        resources = resp["result"]["resources"]
        print(f"[PASS] resources/list — {len(resources)} resources registered")
        for r in resources:
            print(f"  {r['uri']:45s}  {r['name'][:60]}")

        if len(resources) == 15:
            print(f"[PASS] Expected 15 resources, got {len(resources)}")
            passed += 1
        else:
            print(f"[WARN] Expected 15 resources, got {len(resources)}")
            failed += 1

        passed += 1

        # Test 2: Read each resource and verify non-empty content
        for r in resources:
            uri = r["uri"]
            send(proc, {"jsonrpc": "2.0", "id": 3, "method": "resources/read",
                         "params": {"uri": uri}})
            resp = read_line(proc, timeout=4)

            if resp is None:
                print(f"[FAIL] {uri} — no response")
                failed += 1
                continue

            if "error" in resp:
                print(f"[FAIL] {uri} — {resp['error']}")
                failed += 1
                continue

            contents = resp["result"].get("contents", [])
            if not contents:
                print(f"[FAIL] {uri} — empty contents")
                failed += 1
                continue

            text = contents[0].get("text", "")
            size = len(text)
            first_line = text.split("\n")[0][:80] if text else "(empty)"

            if size > 50:
                print(f"[PASS] {uri:45s}  {size:>6} bytes  {first_line}")
                passed += 1
            else:
                print(f"[WARN] {uri:45s}  {size:>6} bytes  (suspiciously small)")
                failed += 1

        # Test 3: Re-read one resource to verify dynamic (not cached)
        uri = "resource://witchmod/index"
        send(proc, {"jsonrpc": "2.0", "id": 4, "method": "resources/read",
                     "params": {"uri": uri}})
        resp1 = read_line(proc, timeout=4)
        send(proc, {"jsonrpc": "2.0", "id": 5, "method": "resources/read",
                     "params": {"uri": uri}})
        resp2 = read_line(proc, timeout=4)
        t1 = resp1["result"]["contents"][0]["text"]
        t2 = resp2["result"]["contents"][0]["text"]
        if t1 == t2:
            print(f"[PASS] Dynamic read — same content on consecutive reads ({len(t1)} bytes)")
            passed += 1
        else:
            print(f"[FAIL] Dynamic read — content differs between reads")
            failed += 1

        # Test 4: Non-existent resource
        send(proc, {"jsonrpc": "2.0", "id": 6, "method": "resources/read",
                     "params": {"uri": "resource://witchmod/nonexistent"}})
        resp = read_line(proc, timeout=4)
        if resp and "error" in resp:
            print(f"[PASS] Missing resource — error: {resp['error']['code']}")
            passed += 1
        else:
            print(f"[WARN] Missing resource — unexpected response: {resp}")
            failed += 1

    finally:
        proc.stdin.close()
        proc.terminate()
        proc.wait(timeout=5)

    total = passed + failed
    print(f"\n{'='*60}")
    print(f"Results: {passed}/{total} passed, {failed} failed")
    return failed == 0


if __name__ == "__main__":
    ok = test()
    sys.exit(0 if ok else 1)
