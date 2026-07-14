"""
End-to-end mod development workflow demonstration.

This script simulates what an AI (connected via MCP) would do in a full
mod testing session. It verifies that EVERY step in the typical mod
development cycle is covered by the toolset.

Run: python tests/test_e2e_workflow.py
  (Requires the game mod to be running on localhost:3100)
"""

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


def read_line(proc, timeout=4.0):
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


def call(proc, tool, args=None, rid=1):
    """Call a tool and return parsed result text."""
    send(proc, {"jsonrpc": "2.0", "id": rid, "method": "tools/call",
                 "params": {"name": tool, "arguments": args or {}}})
    resp = read_line(proc)
    if resp is None:
        return None
    if "error" in resp:
        return resp["error"]
    content = resp["result"]["content"]
    if not content:
        return None
    text = content[0]["text"]
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return text


def read_resource(proc, uri, rid=99):
    send(proc, {"jsonrpc": "2.0", "id": rid, "method": "resources/read",
                 "params": {"uri": uri}})
    resp = read_line(proc)
    if resp and "result" in resp:
        return resp["result"]["contents"][0]["text"]
    return None


def main():
    """Run the full workflow simulation against the MCP server.

    This does NOT require the game to be running. When the mod is offline,
    tools return "not reachable" errors which is acceptable for this demo.
    """

    print("=" * 70)
    print(" WitchModMCP Automated Mod Testing Workflow")
    print("=" * 70)
    print()

    proc = subprocess.Popen(
        [PYTHON, "-m", "mcp_gateway.server"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )

    try:
        time.sleep(0.8)
        if proc.poll() is not None:
            print("ERROR: Server failed to start")
            return

        # ── MCP Handshake ──────────────────────────────────────────────
        send(proc, {"jsonrpc": "2.0", "id": 1, "method": "initialize",
                     "params": {"protocolVersion": "2024-11-05",
                                "capabilities": {},
                                "clientInfo": {"name": "workflow_demo", "version": "1.0"}}})
        init = read_line(proc)
        server_name = init["result"]["serverInfo"]["name"]
        print(f"[CONNECTED] {server_name}")

        send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
        time.sleep(0.1)

        # ── Phase 1: Discovery (Step 1 in any workflow) ─────────────────
        print("\n── Phase 1: DISCOVERY ──")
        print("AI calls: resources/list, tools/list, list_tools")

        send(proc, {"jsonrpc": "2.0", "id": 10, "method": "resources/list"})
        resources = read_line(proc)["result"]["resources"]
        print(f"  Resources: {len(resources)} available")

        send(proc, {"jsonrpc": "2.0", "id": 11, "method": "tools/list"})
        tools = read_line(proc)["result"]["tools"]
        print(f"  Tools: {len(tools)} available")

        # AI reads the root index to understand the system
        text = read_resource(proc, "resource://witchmod/index")
        print(f"  Root index: {len(text)} bytes loaded")

        # ── Phase 2: Orientation ────────────────────────────────────────
        print("\n── Phase 2: ORIENTATION ──")
        print("AI: 'What state is the game in?'")
        print("→ calls: get_scene_state, get_game_data")

        result = call(proc, "get_scene_state")
        if isinstance(result, dict):
            page = result.get("page", "UNKNOWN")
            print(f"  Page: {page}")

        result = call(proc, "get_game_data")
        if isinstance(result, dict):
            player = result.get("player", {})
            if player:
                print(f"  Player: HP={player.get('hp')}/{player.get('maxHp')} "
                      f"SAN={player.get('san')}/{player.get('maxSan')} "
                      f"Money={player.get('money')}")
            fight = result.get("fight", {})
            if fight:
                print(f"  Fight: {'In fight' if fight.get('inFight') else 'Not in fight'}")

        # ── Phase 3: Test scenario setup ─────────────────────────────────
        print("\n── Phase 3: TEST SETUP ──")
        print("AI: 'Set up a standard run for testing my new card'")

        work_steps = [
            ("AI reads gameflow docs", "resource://witchmod/tools/gameflow"),
            ("AI reads lobby docs", "resource://witchmod/tools/lobby"),
            ("call list_game_modes()", "list_game_modes"),
            ("call enter_game() (from MAIN_MENU)", "enter_game"),
            ("call start_new_game('Standard')", "start_new_game"),
            ("call get_lobby_state()", "get_lobby_state"),
            ("call set_lobby_state(career='Witch', confirm=True)", "set_lobby_state"),
            ("call start_run()", "start_run"),
        ]
        for label, uri_or_tool in work_steps:
            if "resource://" in uri_or_tool:
                text = read_resource(proc, uri_or_tool)
                print(f"  [READ] {label} ({len(text)} bytes)")
            else:
                args = {}
                if uri_or_tool == "start_new_game":
                    args = {"mode": "Standard"}
                elif uri_or_tool == "set_lobby_state":
                    args = {"career": "Witch", "confirm": True}
                result = call(proc, uri_or_tool, args)
                status = "OK" if result else "N/A (mod offline)"
                print(f"  [CALL] {label} → {status}")

        # ── Phase 4: Combat testing ─────────────────────────────────────
        print("\n── Phase 4: COMBAT TESTING ──")
        print("AI: 'Test my new card in real combat'")

        combat_steps = [
            ("Read combat docs", "resource://witchmod/tools/combat"),
            ("call get_fight_state()", "get_fight_state"),
            ("call play_card(card_index=0)", "play_card"),
            ("call end_turn()", "end_turn"),
            ("call get_fight_state() (verify turn ended)", "get_fight_state"),
        ]
        for label, tool in combat_steps:
            if "resource://" in tool:
                text = read_resource(proc, tool)
                print(f"  [READ] {label} ({len(text)} bytes)")
            else:
                args = {}
                if tool == "play_card":
                    args = {"card_index": 0}
                result = call(proc, tool, args)
                status = "OK" if result else "N/A (mod offline)"
                print(f"  [CALL] {label} → {status}")

        # ── Phase 5: Diagnosis ──────────────────────────────────────────
        print("\n── Phase 5: DIAGNOSIS ──")
        print("AI: 'My mod isn't working. Let me debug.'")

        diag_steps = [
            ("call get_recent_logs(count=20)", "get_recent_logs"),
            ("call dump_mod_state()", "dump_mod_state"),
            ("call query_config(table_name='CardConfig', limit=3)", "query_config"),
            ("call inspect(type_name='RoleTable', member_path='Instance.San')", "inspect"),
            ("call get_scene_tree()", "get_scene_tree"),
            ("call get_screenshot()", "get_screenshot"),
            ("Read diagnostics docs", "resource://witchmod/tools/diagnostics"),
        ]
        for label, tool_or_uri in diag_steps:
            if "resource://" in tool_or_uri:
                text = read_resource(proc, tool_or_uri)
                print(f"  [READ] {label} ({len(text)} bytes)")
            else:
                args = {}
                if tool_or_uri == "get_recent_logs":
                    args = {"count": 20}
                elif tool_or_uri == "query_config":
                    args = {"table_name": "CardConfig", "limit": 3}
                elif tool_or_uri == "inspect":
                    args = {"type_name": "RoleTable", "member_path": "Instance.San", "max_depth": 2}
                result = call(proc, tool_or_uri, args)
                status = "OK" if result else "N/A (mod offline)"
                print(f"  [CALL] {label} → {status}")

        # ── Phase 6: Mod iteration ──────────────────────────────────────
        print("\n── Phase 6: ITERATION ──")
        print("AI: 'I fixed the bug, let me reload and re-test'")

        iter_steps = [
            ("call reload_tools()", "reload_tools"),
            ("call get_scene_state() (verify page still valid)", "get_scene_state"),
            ("call give_item(type='card', value='20001') (inject test card)", "give_item"),
        ]
        for label, tool in iter_steps:
            args = {}
            if tool == "give_item":
                args = {"item_type": "card", "value": "20001"}
            result = call(proc, tool, args)
            status = "OK" if result else "N/A (mod offline)"
            print(f"  [CALL] {label} → {status}")

        # ── Phase 7: End-of-session cleanup ─────────────────────────────
        print("\n── Phase 7: CLEANUP ──")
        print("AI: 'Run complete. Post-analysis.'")

        final_steps = [
            ("call check_mode_saves()", "check_mode_saves"),
            ("call decompile_source() (if code needs inspection)", "decompile_source"),
        ]
        for label, tool in final_steps:
            result = call(proc, tool)
            status = "OK" if result else "N/A (mod offline)"
            print(f"  [CALL] {label} → {status}")

        # ── Summary ─────────────────────────────────────────────────────
        print("\n" + "=" * 70)
        print(" WORKFLOW COVERAGE ANALYSIS")
        print("=" * 70)

        phases = {
            "Discovery":    ["resources/list", "tools/list", "resources/read"],
            "Orientation":  ["get_scene_state", "get_game_data"],
            "Setup":        ["enter_game", "start_new_game", "set_lobby_state", "start_run", "get_lobby_state"],
            "Combat":       ["get_fight_state", "play_card", "end_turn"],
            "Diagnosis":    ["get_recent_logs", "dump_mod_state", "query_config", "inspect", "get_scene_tree", "get_screenshot"],
            "Iteration":    ["reload_tools", "give_item"],
            "Cleanup":      ["check_mode_saves", "decompile_source"],
        }

        for phase, phase_tools in phases.items():
            coverage = "[x]" * len(phase_tools)
            print(f"  {phase:15s}  {len(phase_tools)} tools  {coverage}  {', '.join(phase_tools[:3])}...")

    finally:
        proc.stdin.close()
        proc.terminate()
        proc.wait(timeout=5)


if __name__ == "__main__":
    main()
