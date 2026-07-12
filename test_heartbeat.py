#!/usr/bin/env python3
"""
test_heartbeat.py — Unit tests for heartbeat system, skill sync, and decompile trigger.

Tests use a mock HTTP server to simulate the game mod, so they can run
without the game being launched.

Run:  python -m pytest test_heartbeat.py -v
  or: python test_heartbeat.py
"""

import json
import os
import shutil
import sys
import tempfile
import threading
import time
from http.server import HTTPServer, BaseHTTPRequestHandler
from pathlib import Path

# Ensure mcp_gateway is importable
sys.path.insert(0, str(Path(__file__).resolve().parent))

from mcp_gateway.heartbeat import HeartbeatManager
from mcp_gateway.skill_sync import sync_skill_docs, compute_fingerprint, sync_namespace

# ═══════════════════════════════════════════════════════════════
#  Mock mod HTTP server
# ═══════════════════════════════════════════════════════════════

class MockModHandler(BaseHTTPRequestHandler):
    """Simulates the C# mod's HTTP server for heartbeat + tool calls."""

    # Class-level state set by tests
    _state_lock = threading.Lock()
    _first_heartbeat = True
    _heartbeat_count = 0
    _tool_count = 5
    _active_modules = []
    _tool_responses = {}  # method -> result dict
    _decompile_called = False
    _decompile_output_dir = None
    _fail_after = -1  # -1 = never; >=0 = start returning 503 after this many heartbeats

    def log_message(self, *args):
        pass  # suppress stderr noise

    def do_GET(self):
        if self.path == "/ping":
            self._respond(200, {"status": "ok", "port": 0, "auth": False})
        else:
            self._respond(404, {"error": "not found"})

    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", 0))).decode("utf-8")
        path = self.path

        if path == "/heartbeat":
            data = json.loads(body) if body else {}
            with MockModHandler._state_lock:
                MockModHandler._heartbeat_count += 1
                is_first = MockModHandler._first_heartbeat
                if MockModHandler._first_heartbeat:
                    MockModHandler._first_heartbeat = False
                count = MockModHandler._heartbeat_count

            if MockModHandler._fail_after >= 0 and count > MockModHandler._fail_after:
                self._respond(503, {"error": "server down"})
                return

            self._respond(200, {
                "status": "ok",
                "port": 0,
                "auth": False,
                "toolCount": MockModHandler._tool_count,
                "sessionId": "test-session-001",
                "isFirstHeartbeat": is_first,
                "timestamp": "2026-01-01T00:00:00Z",
                "workspacePath": data.get("workspacePath", ""),
                "pid": data.get("pid", 0),
                "activeModules": MockModHandler._active_modules,
            })
            return

        # JSON-RPC tool calls
        try:
            req = json.loads(body)
            method = req.get("method", "")
            params = req.get("params", {})

            if method == "decompile_source":
                with MockModHandler._state_lock:
                    MockModHandler._decompile_called = True
                    MockModHandler._decompile_output_dir = params.get("outputDir", "")
                self._respond(200, {
                    "result": {
                        "status": "decompiled",
                        "manifestPath": os.path.join(params.get("outputDir", ""), ".decompile_manifest.json"),
                        "log": ["Witch.dll: decompiling...", "Witch.dll: DONE"],
                        "dlls": {
                            "Witch.dll": {"hash": "abc123", "dir": "abc123"},
                            "Witch.Core.dll": {"hash": "def456", "dir": "def456"},
                        }
                    }
                })
                return

            if method in MockModHandler._tool_responses:
                self._respond(200, {"result": MockModHandler._tool_responses[method]})
                return

            if method == "list_tools":
                tools = [{"name": f"tool_{i}", "description": f"Test tool {i}", "inputSchema": {"type": "object"}}
                         for i in range(MockModHandler._tool_count)]
                self._respond(200, {"result": {"tools": tools}})
                return

            self._respond(200, {"error": {"code": -32601, "message": f"Method not found: {method}"}})
        except Exception as e:
            self._respond(200, {"error": {"code": -32603, "message": str(e)}})

    def _respond(self, code, obj):
        data = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    @classmethod
    def reset(cls, active_modules=None, tool_count=5, tool_responses=None, fail_after=-1):
        with cls._state_lock:
            cls._first_heartbeat = True
            cls._heartbeat_count = 0
            cls._tool_count = tool_count
            cls._active_modules = active_modules or []
            cls._tool_responses = tool_responses or {}
            cls._decompile_called = False
            cls._decompile_output_dir = None
            cls._fail_after = fail_after


def start_mock_server(port=0):
    """Start mock mod server on given port (0 = auto-assign). Returns (server, actual_port)."""
    server = HTTPServer(("127.0.0.1", port), MockModHandler)
    thread = threading.Thread(target=server.serve_forever, daemon=True, name="mock-mod")
    thread.start()
    return server, server.server_address[1]


# ═══════════════════════════════════════════════════════════════
#  Mock ModConnection (uses HTTPClient to talk to mock server)
# ═══════════════════════════════════════════════════════════════

class MockModConn:
    """Mimics ModConnection for HeartbeatManager."""
    def __init__(self, port):
        self.port = port
        self.token = ""
        self._conn = None

    def _get_conn(self):
        import http.client
        if self._conn is None:
            self._conn = http.client.HTTPConnection("127.0.0.1", self.port, timeout=3)
        return self._conn

    def ping(self):
        try:
            conn = self._get_conn()
            conn.request("GET", "/ping")
            resp = conn.getresponse()
            return json.loads(resp.read().decode("utf-8"))
        except Exception as e:
            return {"status": "error", "message": str(e)}

    def call_tool(self, method, params=None):
        body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": method, "params": params or {}})
        try:
            conn = self._get_conn()
            conn.request("POST", "/", body, {"Content-Type": "application/json"})
            resp = conn.getresponse()
            data = json.loads(resp.read().decode("utf-8"))
            return MockModConn._lower_keys(data)
        except Exception as e:
            return {"jsonrpc": "2.0", "error": {"code": -32000, "message": str(e)}}

    @staticmethod
    def _lower_keys(d):
        if isinstance(d, dict):
            return {k[0].lower() + k[1:]: MockModConn._lower_keys(v) for k, v in d.items()}
        elif isinstance(d, list):
            return [MockModConn._lower_keys(v) for v in d]
        return d


# ═══════════════════════════════════════════════════════════════
#  Test helpers
# ═══════════════════════════════════════════════════════════════

PASS = 0
FAIL = 0
LOG = []


def _log(s):
    print(s)
    LOG.append(s)


def _assert(name, condition, detail=""):
    global PASS, FAIL
    if condition:
        _log(f"  PASS  {name}")
        PASS += 1
    else:
        _log(f"  FAIL  {name}: {detail}")
        FAIL += 1


def _create_skill_docs(base_dir: str, module_name: str, files: dict) -> str:
    """Create a fake skill doc directory. Returns the absolute path."""
    skill_dir = os.path.join(base_dir, module_name, "mcp_skills")
    os.makedirs(skill_dir, exist_ok=True)
    for rel_path, content in files.items():
        full = os.path.join(skill_dir, rel_path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8") as f:
            f.write(content)
    return skill_dir


# ═══════════════════════════════════════════════════════════════
#  Tests
# ═══════════════════════════════════════════════════════════════

def test_heartbeat_first_and_subsequent():
    """Test that HeartbeatManager detects first heartbeat and then keepalives."""
    _log("\n=== test_heartbeat_first_and_subsequent ===")
    MockModHandler.reset(active_modules=[], tool_count=3)

    srv, port = start_mock_server()
    try:
        mod_conn = MockModConn(port)
        workspace = "/tmp/test-workspace"

        mgr = HeartbeatManager(mod_conn, workspace, on_first_heartbeat=lambda r: None, interval=0.3)
        mgr.start()

        # Wait for first heartbeat
        time.sleep(0.5)
        _assert("connected after first hb", mgr.connected, f"connected={mgr.connected}")
        _assert("first_heartbeat_done", mgr.first_heartbeat_done, f"done={mgr.first_heartbeat_done}")
        _assert("session_id set", mgr.session_id == "test-session-001", f"sid={mgr.session_id}")

        count1 = MockModHandler._heartbeat_count
        _assert("heartbeat sent at least once", count1 >= 1, f"count={count1}")

        # Wait for more heartbeats
        time.sleep(1.0)
        count2 = MockModHandler._heartbeat_count
        _assert("heartbeat continued", count2 > count1, f"count went {count1} -> {count2}")
        # Only the first should be isFirstHeartbeat=true
        _assert("still connected", mgr.connected)

        mgr.stop()
    finally:
        srv.shutdown()


def test_heartbeat_disconnect_on_failure():
    """Test that HeartbeatManager marks disconnected after max_failures.

    Uses fail_after to make the mock server return 503 after N heartbeats,
    simulating a game crash without needing to actually kill the server.
    """
    _log("\n=== test_heartbeat_disconnect_on_failure ===")
    MockModHandler.reset(fail_after=2)  # start failing after 2 successful heartbeats

    srv, port = start_mock_server()
    try:
        mod_conn = MockModConn(port)
        # Close cached connection between heartbeats so 503s are actually seen
        mgr = HeartbeatManager(mod_conn, "/tmp", on_first_heartbeat=lambda r: None, interval=0.2, max_failures=2)
        mgr.start()

        time.sleep(0.5)
        _assert("connected initially", mgr.connected)

        # Wait enough for 2+ failures (server returns 503 after heartbeat 2)
        time.sleep(2.0)
        _assert("disconnected after failures", not mgr.connected, f"connected={mgr.connected}")

        mgr.stop()
    finally:
        srv.shutdown()
        srv.server_close()


def test_skill_sync_local():
    """Test that skill docs are correctly copied to local .agents/skills/."""
    _log("\n=== test_skill_sync_local ===")

    with tempfile.TemporaryDirectory() as tmp:
        # Create fake skill docs for two mods
        skills_dir_F = _create_skill_docs(tmp, "WitchModMCP", {
            "SKILL.md": "# WitchModMCP\nBase mod.\n",
            "skills/combat/SKILL.md": "# Combat\nCombat tools.\n",
        })
        skills_dir_D = _create_skill_docs(tmp, "DeveloperTools", {
            "SKILL.md": "# DeveloperTools\nExtended.\n",
            "skills/diagnostics/SKILL.md": "# Diagnostics\nDebug.\n",
        })

        local_skills = os.path.join(tmp, "local_skills")
        os.makedirs(local_skills, exist_ok=True)

        heartbeat_resp = {
            "status": "ok",
            "activeModules": [
                {"assemblyName": "WitchModMCP", "skillPath": skills_dir_F},
                {"assemblyName": "DeveloperTools", "skillPath": skills_dir_D},
            ],
        }

        result = sync_skill_docs(heartbeat_resp, local_skills)

        _assert("synced WitchModMCP", result["synced"].get("WitchModMCP") == 2, f"{result['synced']}")
        _assert("synced DeveloperTools", result["synced"].get("DeveloperTools") == 2, f"{result['synced']}")
        _assert("no errors", len(result["errors"]) == 0, str(result["errors"]))

        # Verify files exist
        f1 = Path(local_skills) / "WitchModMCP" / "SKILL.md"
        f2 = Path(local_skills) / "WitchModMCP" / "skills" / "combat" / "SKILL.md"
        f3 = Path(local_skills) / "DeveloperTools" / "SKILL.md"
        f4 = Path(local_skills) / "DeveloperTools" / "skills" / "diagnostics" / "SKILL.md"

        _assert("local SKILL.md (WitchModMCP)", f1.exists())
        _assert("local combat SKILL.md", f2.exists())
        _assert("local SKILL.md (DeveloperTools)", f3.exists())
        _assert("local diagnostics SKILL.md", f4.exists())

        # Verify content
        content = f1.read_text(encoding="utf-8")
        _assert("content correct", "# WitchModMCP" in content, f"got: {content[:50]}")

        # Verify MASTER_INDEX exists
        idx = Path(local_skills) / "MASTER_INDEX.md"
        _assert("MASTER_INDEX generated", idx.exists())
        idx_content = idx.read_text(encoding="utf-8")
        _assert("index has WitchModMCP", "WitchModMCP" in idx_content)
        _assert("index has DeveloperTools", "DeveloperTools" in idx_content)


def test_skill_sync_global():
    """Test that skill docs are also copied to global ~/.config/opencode/skills/."""
    _log("\n=== test_skill_sync_global ===")

    with tempfile.TemporaryDirectory() as tmp:
        # Create fake skill docs
        skill_src = _create_skill_docs(tmp, "TestMod", {
            "SKILL.md": "# TestMod\nGlobal test.\n",
        })

        local_skills = os.path.join(tmp, "local")
        global_skills = os.path.join(tmp, "global")
        os.makedirs(local_skills, exist_ok=True)
        os.makedirs(global_skills, exist_ok=True)

        heartbeat_resp = {
            "status": "ok",
            "activeModules": [
                {"assemblyName": "TestMod", "skillPath": skill_src},
            ],
        }

        result = sync_skill_docs(heartbeat_resp, local_skills, global_skills)

        _assert("synced to local", (Path(local_skills) / "TestMod" / "SKILL.md").exists())
        _assert("synced to global", (Path(global_skills) / "TestMod" / "SKILL.md").exists(),
                f"global dir: {global_skills}")

        # Content should match
        local_md = (Path(local_skills) / "TestMod" / "SKILL.md").read_text(encoding="utf-8")
        global_md = (Path(global_skills) / "TestMod" / "SKILL.md").read_text(encoding="utf-8")
        _assert("local == global content", local_md == global_md)
        _assert("content is TestMod", "# TestMod" in local_md)


def test_skill_sync_idempotent():
    """Test that calling sync again with no changes doesn't re-copy."""
    _log("\n=== test_skill_sync_idempotent ===")

    with tempfile.TemporaryDirectory() as tmp:
        skill_src = _create_skill_docs(tmp, "IdempotentMod", {
            "SKILL.md": "# Idempotent\nTest.\n",
        })

        local_skills = os.path.join(tmp, "skills")
        os.makedirs(local_skills, exist_ok=True)

        heartbeat_resp = {
            "status": "ok",
            "activeModules": [
                {"assemblyName": "IdempotentMod", "skillPath": skill_src},
            ],
        }

        result1 = sync_skill_docs(heartbeat_resp, local_skills)
        _assert("first sync copies", result1["synced"].get("IdempotentMod") == 1)

        f = Path(local_skills) / "IdempotentMod" / "SKILL.md"
        mtime1 = f.stat().st_mtime

        time.sleep(0.1)
        result2 = sync_skill_docs(heartbeat_resp, local_skills)
        mtime2 = f.stat().st_mtime

        _assert("second sync is no-op (same mtime)", mtime1 == mtime2,
                f"mtime1={mtime1} mtime2={mtime2}")


def test_skill_sync_missing_path():
    """Test sync handles missing skillPath gracefully."""
    _log("\n=== test_skill_sync_missing_path ===")

    with tempfile.TemporaryDirectory() as tmp:
        local_skills = os.path.join(tmp, "skills")
        os.makedirs(local_skills, exist_ok=True)

        heartbeat_resp = {
            "status": "ok",
            "activeModules": [
                {"assemblyName": "GhostMod", "skillPath": "/nonexistent/path"},
            ],
        }

        result = sync_skill_docs(heartbeat_resp, local_skills)
        _assert("error recorded", len(result["errors"]) > 0, str(result["errors"]))
        _assert("GhostMod not synced", "GhostMod" not in result["synced"])


def test_skill_sync_after_deletion():
    """Test that deleting a synced doc and re-running sync restores it."""
    _log("\n=== test_skill_sync_after_deletion ===")

    with tempfile.TemporaryDirectory() as tmp:
        skill_src = _create_skill_docs(tmp, "DeleteMod", {
            "SKILL.md": "# DeleteMod\nTest.\n",
            "skills/core/SKILL.md": "# Core\nCore skills.\n",
        })

        local_skills = os.path.join(tmp, "skills")
        os.makedirs(local_skills, exist_ok=True)

        heartbeat_resp = {
            "status": "ok",
            "activeModules": [
                {"assemblyName": "DeleteMod", "skillPath": skill_src},
            ],
        }

        # 1. Initial sync
        sync_skill_docs(heartbeat_resp, local_skills)
        f1 = Path(local_skills) / "DeleteMod" / "SKILL.md"
        f2 = Path(local_skills) / "DeleteMod" / "skills" / "core" / "SKILL.md"
        _assert("initial sync: SKILL.md exists", f1.exists())
        _assert("initial sync: core SKILL.md exists", f2.exists())

        # 2. Delete one file
        f1.unlink()
        _assert("file deleted", not f1.exists())

        # 3. Re-sync — should detect missing file and re-copy
        sync_skill_docs(heartbeat_resp, local_skills)
        _assert("re-sync: SKILL.md restored", f1.exists())
        content = f1.read_text(encoding="utf-8")
        _assert("re-sync: content correct", "# DeleteMod" in content)

        # 4. Also delete the entire directory
        shutil.rmtree(Path(local_skills) / "DeleteMod")
        _assert("directory deleted", not (Path(local_skills) / "DeleteMod").exists())

        # 5. Re-sync — should recreate everything
        sync_skill_docs(heartbeat_resp, local_skills)
        _assert("re-sync after dir delete: SKILL.md restored", f1.exists())
        _assert("re-sync after dir delete: core SKILL.md restored", f2.exists())


def test_decompile_triggered_on_first_heartbeat():
    """Test that decompile_source is called when first heartbeat callback fires."""
    _log("\n=== test_decompile_triggered_on_first_heartbeat ===")

    MockModHandler.reset(active_modules=[], tool_count=3)

    srv, port = start_mock_server()
    try:
        mod_conn = MockModConn(port)

        decompile_called = threading.Event()
        decompile_dir = None

        def on_first_hb(resp):
            nonlocal decompile_dir
            decompile_dir = os.path.join(os.path.dirname(__file__), ".cache", "test_game_src")
            os.makedirs(decompile_dir, exist_ok=True)
            decomp_resp = mod_conn.call_tool("decompile_source", {"outputDir": decompile_dir})
            if decomp_resp.get("result", {}).get("status"):
                decompile_called.set()

        mgr = HeartbeatManager(mod_conn, os.path.dirname(__file__), on_first_heartbeat=on_first_hb, interval=0.3)
        mgr.start()

        time.sleep(0.8)
        _assert("decompile_source was called", decompile_called.is_set(),
                f"decompile_called={decompile_called.is_set()}")
        _assert("decompile output dir captured", MockModHandler._decompile_called,
                f"mock decompile_called={MockModHandler._decompile_called}")
        _assert("decompile output dir matches", MockModHandler._decompile_output_dir == decompile_dir,
                f"got={MockModHandler._decompile_output_dir}")

        mgr.stop()
    finally:
        srv.shutdown()


def test_full_pipeline_skill_sync_and_decompile():
    """End-to-end: first heartbeat triggers skill sync + decompile."""
    _log("\n=== test_full_pipeline_skill_sync_and_decompile ===")

    with tempfile.TemporaryDirectory() as tmp:
        # Create realistic skill docs
        skill_src = _create_skill_docs(tmp, "WitchModMCP", {
            "SKILL.md": "# WitchModMCP Full\nFull test.\n",
            "skills/meta/SKILL.md": "# Meta\nState.\n",
        })

        workspace = tmp
        local_skills = os.path.join(workspace, ".agents", "skills")
        decompile_dir = os.path.join(workspace, ".cache", "game_src")

        MockModHandler.reset(
            active_modules=[
                {"assemblyName": "WitchModMCP", "skillPath": skill_src},
            ],
            tool_count=5,
        )

        srv, port = start_mock_server()
        try:
            mod_conn = MockModConn(port)

            def on_first_hb(resp):
                # 1. Sync skills
                sync_skill_docs(resp, local_skills)
                # 2. Trigger decompile
                mod_conn.call_tool("decompile_source", {"outputDir": decompile_dir})

            mgr = HeartbeatManager(mod_conn, workspace, on_first_heartbeat=on_first_hb, interval=0.3)
            mgr.start()

            time.sleep(0.8)

            # Verify skills synced
            _assert("skill SKILL.md synced", (Path(local_skills) / "WitchModMCP" / "SKILL.md").exists())
            _assert("skill meta SKILL.md synced", (Path(local_skills) / "WitchModMCP" / "skills" / "meta" / "SKILL.md").exists())
            _assert("MASTER_INDEX generated", (Path(local_skills) / "MASTER_INDEX.md").exists())

            # Verify decompile was called
            _assert("decompile was called", MockModHandler._decompile_called)
            _assert("decompile output dir correct", MockModHandler._decompile_output_dir == decompile_dir)

            mgr.stop()
        finally:
            srv.shutdown()


def test_heartbeat_workspace_path_forwarded():
    """Test that workspacePath is included in heartbeat body."""
    _log("\n=== test_heartbeat_workspace_path_forwarded ===")

    MockModHandler.reset()
    srv, port = start_mock_server()
    try:
        mod_conn = MockModConn(port)
        workspace = "/custom/workspace/path"

        captured = {}

        original_send = mod_conn._get_conn
        mgr = HeartbeatManager(mod_conn, workspace, on_first_heartbeat=lambda r: None, interval=0.3)
        mgr.start()

        time.sleep(0.5)

        # The mock server's heartbeat response echoes workspacePath
        if mgr._last_response:
            captured = mgr._last_response

        _assert("workspacePath echoed", captured.get("workspacePath") == workspace,
                f"got={captured.get('workspacePath')}")
        _assert("pid is set", captured.get("pid") == os.getpid(),
                f"got={captured.get('pid')}")

        mgr.stop()
    finally:
        srv.shutdown()


# ═══════════════════════════════════════════════════════════════
#  Main
# ═══════════════════════════════════════════════════════════════

def main():
    _log("╔══════════════════════════════════════════════╗")
    _log("║  Heartbeat & Skill Sync Test Suite           ║")
    _log("╚══════════════════════════════════════════════╝")

    test_heartbeat_first_and_subsequent()
    test_heartbeat_disconnect_on_failure()
    test_skill_sync_local()
    test_skill_sync_global()
    test_skill_sync_idempotent()
    test_skill_sync_missing_path()
    test_skill_sync_after_deletion()
    test_decompile_triggered_on_first_heartbeat()
    test_full_pipeline_skill_sync_and_decompile()
    test_heartbeat_workspace_path_forwarded()

    _log(f"\n{'═' * 50}")
    _log(f"Results: {PASS} passed, {FAIL} failed")
    _log(f"{'═' * 50}")

    # Save log
    with open("test_heartbeat_log.txt", "w", encoding="utf-8") as f:
        f.write("\n".join(LOG))

    return 1 if FAIL > 0 else 0


if __name__ == "__main__":
    sys.exit(main())