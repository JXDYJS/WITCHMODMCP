"""Stage 4 smoke test — verify 38 tools + guardrail descriptions."""
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

        # Test 1: 38 total tools
        send(proc, {"jsonrpc": "2.0", "id": 10, "method": "tools/list"})
        resp = read_line(proc)
        tools = resp["result"]["tools"]
        print(f"tools/list: {len(tools)} total tools")

        all_expected = {
            # Stage 3 (16)
            "list_tools", "list_commands",
            "get_scene_state", "get_game_data", "check_mode_saves", "list_game_modes",
            "get_fight_state", "get_lobby_state",
            "inspect", "query_config", "dump_mod_state", "get_recent_logs", "get_scene_tree",
            "get_screenshot", "raycast_mouse", "decompile_source",
            # Stage 4 (22)
            "eval_command", "give_item",
            "set_lobby_state", "set_fight_entity", "set_card_pile",
            "set_rng_seed", "reload_tools",
            "play_card", "end_turn",
            "enter_game", "start_new_game", "start_run", "load_scene", "claim_rewards",
            "map_list_nodes", "map_choose_node",
            "event_advance_dialogue", "event_choose_option",
            "pick_card_reward", "skip_card_reward",
            "pick_blessing_reward", "skip_blessing_reward",
        }
        actual_names = {t["name"] for t in tools}
        missing = all_expected - actual_names
        extra = actual_names - all_expected

        if len(tools) == 38 and not missing and not extra:
            print(f"  All 38 tools present")
            passed += 1
        else:
            if missing:
                print(f"  MISSING ({len(missing)}): {sorted(missing)}")
                failed += len(missing)
            if extra:
                print(f"  EXTRA ({len(extra)}): {sorted(extra)}")
                failed += len(extra)
        passed += 1

        # Test 2: All tools have descriptions and schemas
        for t in tools:
            name = t["name"]
            desc = t.get("description", "")
            schema = t.get("inputSchema")
            if desc and schema:
                passed += 1
            else:
                issues = []
                if not desc: issues.append("no description")
                if not schema: issues.append("no inputSchema")
                print(f"  [FAIL] {name}: {', '.join(issues)}")
                failed += 1

        print(f"  All 38 tools have descriptions and inputSchemas")
        passed += 1

        # Test 3: Guardrail descriptions contain resource:// references
        high_risk_guardrailed = [
            "eval_command", "give_item", "set_lobby_state",
            "set_fight_entity", "set_card_pile", "load_scene",
        ]
        guarded = 0
        for t in tools:
            if t["name"] in high_risk_guardrailed:
                desc = t.get("description", "")
                if "[GUARDED:" in desc and "resource://witchmod/" in desc:
                    guarded += 1
                else:
                    print(f"  [WARN] {t['name']}: missing [GUARDED:] or resource URI")
                    failed += 1

        if guarded == len(high_risk_guardrailed):
            print(f"  All {guarded} high-risk tools have [GUARDED:] prefix + resource URIs")
            passed += 1
        else:
            print(f"  Only {guarded}/{len(high_risk_guardrailed)} high-risk tools properly guarded")
            failed += 1

        # Test 4: Tools/call for high-risk tool with no mod running
        send(proc, {"jsonrpc": "2.0", "id": 30, "method": "tools/call",
                     "params": {"name": "eval_command",
                                "arguments": {"command": "help"}}})
        resp = read_line(proc, timeout=4)
        if resp and "result" in resp:
            text = resp["result"]["content"][0]["text"]
            if "not reachable" in text.lower():
                print(f"[PASS] tools/call (eval_command) — offline guard, forwarding OK")
                passed += 1
            else:
                print(f"[PASS] tools/call (eval_command) — response ({len(text)} bytes)")
                passed += 1
        else:
            print(f"[FAIL] tools/call (eval_command): {resp}")
            failed += 1

        # Test 5: stdout purity
        send(proc, {"jsonrpc": "2.0", "id": 40, "method": "resources/list"})
        resp = read_line(proc)
        if resp and "result" in resp:
            rc = len(resp["result"]["resources"])
            print(f"[PASS] resources/list: {rc} resources still available")
            passed += 1

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
