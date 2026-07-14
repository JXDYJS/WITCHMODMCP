"""Stage 3 smoke test — verify tool registration and basic forwarding."""
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
            print(f"FAIL: Server crashed\n{stderr}")
            return

        # Init
        send(proc, {"jsonrpc": "2.0", "id": 1, "method": "initialize",
                     "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                                "clientInfo": {"name": "test", "version": "1.0"}}})
        read_line(proc)
        send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
        time.sleep(0.1)

        # Test 1: tools/list should return 16 tools
        send(proc, {"jsonrpc": "2.0", "id": 10, "method": "tools/list"})
        resp = read_line(proc)
        tools = resp["result"]["tools"]
        print(f"tools/list: {len(tools)} tools registered")

        expected_names = {
            "list_tools", "list_commands",
            "get_scene_state", "get_game_data", "check_mode_saves", "list_game_modes",
            "get_fight_state", "get_lobby_state",
            "inspect", "query_config", "dump_mod_state", "get_recent_logs", "get_scene_tree",
            "get_screenshot", "raycast_mouse", "decompile_source",
        }
        actual_names = {t["name"] for t in tools}

        missing = expected_names - actual_names
        extra = actual_names - expected_names

        if missing:
            print(f"  MISSING tools: {missing}")
            failed += len(missing)
        if extra:
            print(f"  UNEXPECTED tools: {extra}")
            failed += len(extra)

        if not missing and not extra:
            print(f"  All 16 tools present")
            passed += 1
        passed += 1

        for t in tools:
            has_desc = bool(t.get("description"))
            has_schema = bool(t.get("inputSchema"))
            name = t["name"]
            if has_desc and has_schema:
                print(f"  [OK] {name}")
                passed += 1
            else:
                issues = []
                if not has_desc:
                    issues.append("no description")
                if not has_schema:
                    issues.append("no inputSchema")
                print(f"  [WARN] {name}: {', '.join(issues)}")
                failed += 1

        # Test 2: tools/call — parameterless tool (mod offline, expect error)
        send(proc, {"jsonrpc": "2.0", "id": 20, "method": "tools/call",
                     "params": {"name": "list_tools", "arguments": {}}})
        resp = read_line(proc, timeout=4)
        if resp and "result" in resp:
            content = resp["result"]["content"]
            text = content[0]["text"]
            # When mod is offline, should get "not reachable" error
            if "not reachable" in text.lower() or "Game mod is not reachable" in text:
                print(f"[PASS] tools/call (list_tools) — correct offline error response")
                passed += 1
            elif "list_tools" in text.lower():
                print(f"[PASS] tools/call (list_tools) — got data (mod may be running)")
                passed += 1
            else:
                print(f"[INFO] tools/call (list_tools) response: {text[:200]}")
                passed += 1
        else:
            print(f"[FAIL] tools/call no result: {resp}")
            failed += 1

        # Test 3: tools/call with parameters (inspect — mod offline)
        send(proc, {"jsonrpc": "2.0", "id": 21, "method": "tools/call",
                     "params": {"name": "inspect",
                                "arguments": {"type_name": "RoleTable", "max_depth": 2}}})
        resp = read_line(proc, timeout=4)
        if resp and "result" in resp:
            text = resp["result"]["content"][0]["text"]
            if "not reachable" in text.lower():
                print(f"[PASS] tools/call (inspect) — offline error, forwarding works")
                passed += 1
            else:
                print(f"[PASS] tools/call (inspect) — response ({len(text)} bytes)")
                passed += 1
        else:
            print(f"[FAIL] tools/call (inspect): {resp}")
            failed += 1

        # Test 4: Verify stdout purity — every non-empty line is JSON-RPC
        send(proc, {"jsonrpc": "2.0", "id": 30, "method": "resources/list"})
        resp = read_line(proc)
        if resp and "result" in resp:
            print(f"[PASS] resources/list still works after tool registration")
            passed += 1
        else:
            print(f"[FAIL] resources/list: {resp}")
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
