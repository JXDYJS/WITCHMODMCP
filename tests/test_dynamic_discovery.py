#!/usr/bin/env python3
"""End-to-end dynamic tool discovery test.

Spawns the gateway server as a subprocess (with env vars that match
opencode.json) and verifies:

1.  After MCP handshake, tools/list returns ONLY `ping`.
2.  After the heartbeat daemon thread connects to the running game
    mod, the server sends an unsolicited
    `notifications/tools/list_changed` JSON-RPC notification.
3.  A follow-up tools/list now includes all C# mod tools
    (>= 10 tools total — typically 76+).

Requires:
  * The WitchModMCP mod to be loaded in the running game
    (heartbeat needs to succeed).
  * Working directory: project root (E:\\Witch\\WitchModMCP).
"""

import json
import os
import subprocess
import sys
import time

PROTO = "2025-06-18"

def env():
    e = os.environ.copy()
    e.setdefault("MCP_MOD_PORT", "3100")
    e.setdefault("MCP_MOD_TOKEN", "witch-mod-mcp-dev-2026")
    e.setdefault("MCP_HEARTBEAT_INTERVAL", "2")  # faster for test
    e.setdefault("MCP_HEARTBEAT_MAX_FAIL", "3")
    e["PYTHONPATH"] = os.getcwd()
    e.setdefault("MCP_DISABLE_DECOMPILE", "1")  # skip slow decompile in test
    return e


def send(proc, obj):
    line = json.dumps(obj) + "\n"
    proc.stdin.write(line)
    proc.stdin.flush()


def read_message(proc, deadline_s):
    """Read one JSON-RPC message (notification or response) before deadline.

    Returns parsed dict or None on timeout/EOF.
    """
    while time.time() < deadline_s:
        # poll stderr separately for debugging
        line = proc.stdout.readline()
        if not line:
            time.sleep(0.05)
            continue
        line = line.strip()
        if not line:
            continue
        try:
            return json.loads(line)
        except json.JSONDecodeError:
            print(f"[test] unparseable stdout line: {line!r}", file=sys.stderr)
    return None


def wait_for(proc, predicate, timeout_s, label):
    """Read messages until predicate(msg) returns True or we time out.

    Returns the matching message, or None on timeout.
    """
    deadline = time.time() + timeout_s
    while time.time() < deadline:
        msg = read_message(proc, deadline)
        if msg is None:
            continue
        kind = "response" if "id" in msg else "notification"
        method = msg.get("method") or msg.get("result", {}).get("method", "?") \
            if kind == "notification" else "?"
        brief = json.dumps(msg)[:160]
        print(f"[test] {kind} (id={msg.get('id','–')} method={msg.get('method','–')})")
        if predicate(msg):
            print(f"[test] MATCH for {label}: {brief}")
            return msg
    print(f"[test] TIMEOUT waiting for {label}", file=sys.stderr)
    return None


def main():
    proc = subprocess.Popen(
        [sys.executable, "-m", "mcp_gateway.server"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env(),
        text=True,
        encoding="utf-8",
        errors="replace",
        bufsize=1,  # line-buffered
    )

    try:
        # 1. initialize
        send(proc, {
            "jsonrpc": "2.0", "id": 1, "method": "initialize",
            "params": {
                "protocolVersion": PROTO,
                "capabilities": {},
                "clientInfo": {"name": "test", "version": "1.0"},
            },
        })
        init = wait_for(proc, lambda m: m.get("id") == 1, 10, "initialize")
        assert init, "no initialize response"
        caps = init["result"]["capabilities"]
        assert "tools" in caps, "tools capability missing"
        print(f"[test] init OK — protocolVersion={init['result']['protocolVersion']}")

        # 2. notifications/initialized
        send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})

        # 3. tools/list BEFORE heartbeat (expect just ping)
        send(proc, {"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
        tl_before = wait_for(proc, lambda m: m.get("id") == 2, 10, "tools/list (pre)")
        assert tl_before, "no tools/list response"
        names_before = [t["name"] for t in tl_before["result"]["tools"]]
        print(f"[test] pre-heartbeat tools ({len(names_before)}): {names_before}")
        assert names_before == ["ping"], \
            f"expected only ['ping'], got {names_before}"

        # 4. Wait for unsolicited notifications/tools/list_changed.
        #    Heartbeat fires every MCP_HEARTBEAT_INTERVAL seconds.
        print("[test] waiting for notifications/tools/list_changed …")
        list_changed = wait_for(
            proc,
            lambda m: (m.get("jsonrpc") == "2.0"
                       and m.get("method") == "notifications/tools/list_changed"),
            30,  # generous: heartbeat needs the game mod to be alive
            "tools/list_changed notification",
        )
        assert list_changed, "never received notifications/tools/list_changed"

        # 5. After list_changed, ask for the new list.
        send(proc, {"jsonrpc": "2.0", "id": 3, "method": "tools/list", "params": {}})
        tl_after = wait_for(proc, lambda m: m.get("id") == 3, 10, "tools/list (post)")
        assert tl_after, "no post tools/list response"
        names_after = [t["name"] for t in tl_after["result"]["tools"]]
        print(f"[test] post-heartbeat tools ({len(names_after)})")
        print(f"[test] sample: {names_after[:10]}")

        # Confirm we now have many more than just ping, plus these hallmark tools.
        assert len(names_after) >= 10, \
            f"expected >= 10 dynamic tools, got {len(names_after)}"
        for must_have in ("ping", "get_game_data", "eval_command", "list_commands",
                          "query_config", "reload_tools"):
            assert must_have in names_after, f"missing {must_have} in {names_after}"
        assert "list_tools" not in names_after, \
            "list_tools shouldn't be exposed — it's a Proxied via _forward"
        # Verify each dynamically-registered tool has its C# inputSchema applied
        # (not the default {} that **kwargs would auto-generate)
        for t in tl_after["result"]["tools"]:
            if t["name"] == "give_item":
                props = t["inputSchema"].get("properties", {})
                assert "type" in props and "value" in props, \
                    f"give_item schema not patched from C# — got {t['inputSchema']}"
                break
        print("[test] PASS — dynamic discovery works, client sees tools after list_changed")

    finally:
        proc.stdin.close()
        proc.terminate()
        try:
            proc.wait(timeout=3)
        except subprocess.TimeoutExpired:
            proc.kill()
        # Dump stderr for visibility (may contain non-ASCII / mojibake on Windows)
        err = proc.stderr.read()
        if err:
            sys.stderr.write("\n[test] ====== server stderr (utf-8 safe) ======\n")
            buf = getattr(sys.stderr, "buffer", None)
            payload = err.encode("utf-8", errors="replace") if isinstance(err, str) else err
            if buf is not None:
                buf.write(payload)
                buf.flush()
            else:
                sys.stderr.write(payload.decode("utf-8", errors="replace"))


if __name__ == "__main__":
    main()