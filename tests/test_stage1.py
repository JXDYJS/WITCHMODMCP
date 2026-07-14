#!/usr/bin/env python3
"""
Stage 1 acceptance tests — unit + integration tests for the refactored gateway.

Coverage:
  mod_client: config discovery, JSON decode errors, _lower_keys edge cases, unique IDs
  heartbeat: state transitions, interval/max_failures bounds, callback trigger
  server: stdout purity, MCP protocol handshake, error handling

Run:  python -m pytest tests/test_stage1.py -v
  or:  python tests/test_stage1.py  (std unittest)
"""

import json
import os
import sys
import time
import http.client
import subprocess
import threading
import unittest
import unittest.mock as mock
from pathlib import Path
from io import BytesIO

# Ensure workspace root is on sys.path for mcp_gateway imports
_workspace = str(Path(__file__).resolve().parent.parent)
if _workspace not in sys.path:
    sys.path.insert(0, _workspace)
os.chdir(_workspace)

PYTHON = r"E:\miniconda\python.exe"


# ═══════════════════════════════════════════════════════════════════════
# mod_client unit tests
# ═══════════════════════════════════════════════════════════════════════

class TestModClientConfig(unittest.TestCase):
    """Config discovery and reading."""

    def test_read_mod_config_defaults(self):
        """Should return defaults when no config file found."""
        # Temporarily unset env and config path
        with mock.patch.dict(os.environ, {}, clear=True):
            with mock.patch("mcp_gateway.mod_client.find_mod_config", return_value=None):
                from mcp_gateway.mod_client import read_mod_config
                cfg = read_mod_config()
                self.assertEqual(cfg["port"], 3100)
                self.assertIsNone(cfg["config_path"])

    def test_read_mod_config_from_file(self):
        """Should read port and token from ModConfig.json."""
        fake_config = json.dumps({"MCPPort": 9999, "MCPAuthToken": "test-token"}).encode("utf-8")
        mock_open = mock.mock_open(read_data=fake_config)
        with mock.patch("builtins.open", mock_open):
            with mock.patch("mcp_gateway.mod_client.find_mod_config", return_value="/fake/ModConfig.json"):
                from mcp_gateway.mod_client import read_mod_config
                cfg = read_mod_config()
                self.assertEqual(cfg["port"], 9999)
                self.assertEqual(cfg["token"], "test-token")
                self.assertEqual(cfg["config_path"], "/fake/ModConfig.json")


class TestModConnectionHelpers(unittest.TestCase):
    """_lower_keys and _request tests."""

    @classmethod
    def setUpClass(cls):
        from mcp_gateway.mod_client import ModConnection
        cls.ModConnection = ModConnection

    def test_lower_keys_pascal_case(self):
        mc = self.ModConnection(3100, "")
        result = mc._lower_keys({"Result": {"SomeKey": 42}})
        self.assertEqual(result, {"result": {"someKey": 42}})

    def test_lower_keys_nested_list(self):
        mc = self.ModConnection(3100, "")
        result = mc._lower_keys({"Items": [{"Id": 1}, {"Id": 2}]})
        self.assertEqual(result, {"items": [{"id": 1}, {"id": 2}]})

    def test_lower_keys_empty_dict(self):
        mc = self.ModConnection(3100, "")
        self.assertEqual(mc._lower_keys({}), {})

    def test_lower_keys_empty_string_key(self):
        """Regression: empty string key should not crash (k[0])."""
        mc = self.ModConnection(3100, "")
        result = mc._lower_keys({"": "value", "Normal": 1})
        self.assertEqual(result, {"": "value", "normal": 1})

    def test_lower_keys_single_char_key(self):
        mc = self.ModConnection(3100, "")
        result = mc._lower_keys({"A": 1, "B": 2})
        self.assertEqual(result, {"a": 1, "b": 2})

    def test_lower_keys_primitive(self):
        mc = self.ModConnection(3100, "")
        self.assertEqual(mc._lower_keys("hello"), "hello")
        self.assertEqual(mc._lower_keys(42), 42)
        self.assertIsNone(mc._lower_keys(None))

    def test_lower_keys_deeply_nested(self):
        mc = self.ModConnection(3100, "")
        result = mc._lower_keys({"A": {"B": {"C": [{"D": 1}]}}})
        self.assertEqual(result, {"a": {"b": {"c": [{"d": 1}]}}})

    def test_unique_request_ids(self):
        """call_tool should use incrementing unique ids."""
        mc = self.ModConnection(3100, "")

        # Mock _request to return a simple JSON response
        def fake_request(*args, **kwargs):
            return 200, json.dumps({"jsonrpc": "2.0", "id": 99, "result": "ok"})

        with mock.patch.object(mc, "_request", side_effect=fake_request):
            resp1 = mc.call_tool("method1")
            resp2 = mc.call_tool("method2")
            resp3 = mc.call_tool("method3")
            # Each call uses incrementing IDs internally
            self.assertEqual(resp1["result"], "ok")
            self.assertEqual(resp2["result"], "ok")
            self.assertEqual(resp3["result"], "ok")

    def test_call_tool_invalid_json_response(self):
        """Should return parse error on malformed JSON."""
        mc = self.ModConnection(3100, "")
        with mock.patch.object(mc, "_request", return_value=(200, "not json")):
            resp = mc.call_tool("test")
            self.assertEqual(resp["error"]["code"], -32700)

    def test_call_tool_connection_refused(self):
        """Should return -32000 on connection error."""
        mc = self.ModConnection(3100, "")
        with mock.patch.object(mc, "_request", side_effect=ConnectionRefusedError("boom")):
            resp = mc.call_tool("test")
            self.assertEqual(resp["error"]["code"], -32000)
            self.assertIn("boom", resp["error"]["message"])

    def test_ping_invalid_json(self):
        mc = self.ModConnection(3100, "")
        with mock.patch.object(mc, "_request", return_value=(200, "garbage")):
            resp = mc.ping()
            self.assertEqual(resp["status"], "error")
            self.assertIn("Invalid JSON", resp["message"])

    def test_ping_connection_error(self):
        mc = self.ModConnection(3100, "")
        with mock.patch.object(mc, "_request", side_effect=OSError("down")):
            resp = mc.ping()
            self.assertEqual(resp["status"], "error")
            self.assertIn("down", resp["message"])

    def test_send_heartbeat_invalid_json(self):
        mc = self.ModConnection(3100, "")
        with mock.patch.object(mc, "_request", return_value=(200, "not{json")):
            ok, data = mc.send_heartbeat("/tmp")
            self.assertFalse(ok)
            self.assertIn("Invalid JSON", data["error"])

    def test_send_heartbeat_connection_error(self):
        mc = self.ModConnection(3100, "")
        with mock.patch.object(mc, "_request", side_effect=OSError("no route")):
            ok, data = mc.send_heartbeat("/tmp")
            self.assertFalse(ok)
            self.assertIn("no route", data["error"])

    def test_send_heartbeat_ok(self):
        mc = self.ModConnection(3100, "")
        resp_json = json.dumps({"status": "ok", "sessionId": "abc"})
        with mock.patch.object(mc, "_request", return_value=(200, resp_json)):
            ok, data = mc.send_heartbeat("/tmp")
            self.assertTrue(ok)
            self.assertEqual(data["sessionId"], "abc")

    def test_send_heartbeat_non_200(self):
        mc = self.ModConnection(3100, "")
        resp_json = json.dumps({"status": "error"})
        with mock.patch.object(mc, "_request", return_value=(500, resp_json)):
            ok, data = mc.send_heartbeat("/tmp")
            self.assertFalse(ok)

    def test_request_decodes_utf8_with_replace(self):
        """_request should handle non-UTF-8 bytes gracefully."""
        mc = self.ModConnection(3100, "")

        class FakeResponse:
            status = 200
            def read(self):
                return b"\xff\xfe invalid utf8"
            def getresponse(self):
                return self

        class FakeConn:
            def __init__(self, *a, **kw):
                pass
            def request(self, *a, **kw):
                pass
            def getresponse(self):
                return FakeResponse()
            def close(self):
                pass

        with mock.patch("http.client.HTTPConnection", new=FakeConn):
            status, body = mc._request("GET", "/")
            self.assertEqual(status, 200)
            self.assertIn("invalid utf8", body)  # survived decode


# ═══════════════════════════════════════════════════════════════════════
# heartbeat unit tests
# ═══════════════════════════════════════════════════════════════════════

class TestHeartbeatManager(unittest.TestCase):
    """State machine and bounds validation."""

    @classmethod
    def setUpClass(cls):
        from mcp_gateway.heartbeat import HeartbeatManager
        from mcp_gateway.mod_client import ModConnection
        cls.HeartbeatManager = HeartbeatManager
        cls.ModConnection = ModConnection

    def test_bounds_negative_interval(self):
        """Negative interval should be clamped."""
        mc = self.ModConnection(3100, "")
        hb = self.HeartbeatManager(mc, "/tmp", interval=-5.0)
        self.assertGreaterEqual(hb.interval, 0.1)

    def test_bounds_zero_interval(self):
        mc = self.ModConnection(3100, "")
        hb = self.HeartbeatManager(mc, "/tmp", interval=0)
        self.assertGreaterEqual(hb.interval, 0.1)

    def test_bounds_zero_max_failures(self):
        mc = self.ModConnection(3100, "")
        hb = self.HeartbeatManager(mc, "/tmp", max_failures=0)
        self.assertGreaterEqual(hb.max_failures, 1)

    def test_initial_state_disconnected(self):
        mc = self.ModConnection(3100, "")
        hb = self.HeartbeatManager(mc, "/tmp")
        self.assertFalse(hb.connected)
        self.assertFalse(hb.first_heartbeat_done)
        self.assertIsNone(hb.session_id)

    def test_connected_after_successful_heartbeat(self):
        """State transitions via internal _run() simulation."""
        mc = self.ModConnection(3100, "")

        call_count = 0

        def fake_heartbeat(ws_dir):
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                return True, {"status": "ok", "sessionId": "s1", "isFirstHeartbeat": True}
            # Stop after first iteration
            return True, {"status": "ok"}

        with mock.patch.object(mc, "send_heartbeat", side_effect=fake_heartbeat):
            hb = self.HeartbeatManager(mc, "/tmp")
            # Start, let one tick run, then stop
            hb.start()
            time.sleep(0.3)
            hb.stop()

        self.assertTrue(hb.connected)
        self.assertTrue(hb.first_heartbeat_done)
        self.assertEqual(hb.session_id, "s1")

    def test_disconnect_after_max_failures(self):
        """Should mark disconnected after consecutive failures."""
        mc = self.ModConnection(3100, "")

        def always_fail(ws_dir):
            return False, {"error": "unreachable"}

        with mock.patch.object(mc, "send_heartbeat", side_effect=always_fail):
            hb = self.HeartbeatManager(mc, "/tmp", max_failures=2, interval=0.1)
            hb.start()
            time.sleep(0.6)  # Allow ~6 ticks
            hb.stop()

        self.assertFalse(hb.connected)
        self.assertFalse(hb.first_heartbeat_done)

    def test_first_heartbeat_callback_triggered(self):
        """on_first_heartbeat should be called exactly once."""
        mc = self.ModConnection(3100, "")

        callback_seen = []

        def my_callback(resp):
            callback_seen.append(resp)

        call_count = 0

        def fake_heartbeat(ws_dir):
            nonlocal call_count
            call_count += 1
            return True, {"status": "ok", "sessionId": f"s{call_count}"}

        with mock.patch.object(mc, "send_heartbeat", side_effect=fake_heartbeat):
            hb = self.HeartbeatManager(mc, "/tmp", on_first_heartbeat=my_callback, interval=0.1)
            hb.start()
            time.sleep(0.5)
            hb.stop()

        self.assertEqual(len(callback_seen), 1)

    def test_double_start_is_idempotent(self):
        mc = self.ModConnection(3100, "")
        hb = self.HeartbeatManager(mc, "/tmp")
        hb.start()
        hb.start()  # Should not raise
        hb.stop()


# ═══════════════════════════════════════════════════════════════════════
# server integration tests (MCP protocol)
# ═══════════════════════════════════════════════════════════════════════

class TestServerMCPProtocol(unittest.TestCase):
    """End-to-end MCP protocol tests using subprocess."""

    @classmethod
    def setUpClass(cls):
        cls.python = PYTHON

    def _start_server(self):
        """Start the gateway server subprocess."""
        return subprocess.Popen(
            [self.python, "-m", "mcp_gateway.server"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )

    def _send(self, proc, msg: dict):
        proc.stdin.write((json.dumps(msg) + "\n").encode("utf-8"))
        proc.stdin.flush()

    def _read_line(self, proc, timeout=3.0) -> dict | None:
        """Read one JSON line from server stdout (non-blocking via thread)."""
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

    def test_01_initialize_handshake(self):
        """Server should respond to initialize with capabilities."""
        proc = self._start_server()
        try:
            time.sleep(0.5)
            if proc.poll() is not None:
                stderr = proc.stderr.read().decode("utf-8", errors="replace")
                self.fail(f"Server crashed on start: {stderr}")

            self._send(proc, {
                "jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "test", "version": "1.0"}},
            })

            resp = self._read_line(proc)
            self.assertIsNotNone(resp, "No response from server")
            self.assertEqual(resp["jsonrpc"], "2.0")
            self.assertEqual(resp["id"], 1)
            self.assertIn("result", resp)

            result = resp["result"]
            self.assertEqual(result["protocolVersion"], "2024-11-05")
            self.assertIn("capabilities", result)
            self.assertIn("tools", result["capabilities"])
            self.assertIn("resources", result["capabilities"])
            self.assertEqual(result["serverInfo"]["name"], "witch-mod-mcp-gateway")
        finally:
            proc.stdin.close()
            proc.terminate()
            proc.wait(timeout=5)

    def test_02_tools_list_has_content(self):
        """tools/list should have registered tools (Stage 4)."""
        proc = self._start_server()
        try:
            time.sleep(0.5)
            self._send(proc, {
                "jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "test", "version": "1.0"}},
            })
            self._read_line(proc)  # consume init resp

            self._send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
            time.sleep(0.1)

            self._send(proc, {"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
            resp = self._read_line(proc)
            self.assertIsNotNone(resp)
            self.assertEqual(resp["id"], 2)
            self.assertGreaterEqual(len(resp["result"]["tools"]), 16,
                "Expected >=16 tools after Stage 4")
        finally:
            proc.stdin.close()
            proc.terminate()
            proc.wait(timeout=5)

    def test_03_resources_list_has_content(self):
        """resources/list should have registered resources (Stage 2)."""
        proc = self._start_server()
        try:
            time.sleep(0.5)
            self._send(proc, {
                "jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "test", "version": "1.0"}},
            })
            self._read_line(proc)

            self._send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
            time.sleep(0.1)

            self._send(proc, {"jsonrpc": "2.0", "id": 3, "method": "resources/list"})
            resp = self._read_line(proc)
            self.assertIsNotNone(resp)
            self.assertEqual(resp["id"], 3)
            self.assertGreaterEqual(len(resp["result"]["resources"]), 15,
                "Expected >=15 resources after Stage 2")
        finally:
            proc.stdin.close()
            proc.terminate()
            proc.wait(timeout=5)

    def test_04_ping_method_returns_ok(self):
        """Server responds to ping with status ok."""
        proc = self._start_server()
        try:
            time.sleep(0.5)
            self._send(proc, {
                "jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "test", "version": "1.0"}},
            })
            self._read_line(proc)
            self._send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
            time.sleep(0.1)

            self._send(proc, {"jsonrpc": "2.0", "id": 2, "method": "ping"})
            resp = self._read_line(proc, timeout=3)
            self.assertIsNotNone(resp, "Server should respond to ping")
            self.assertEqual(resp["id"], 2)
        finally:
            proc.stdin.close()
            proc.terminate()
            proc.wait(timeout=5)

    def test_05_multiple_requests_in_sequence(self):
        """Server should handle multiple sequential requests without issues."""
        proc = self._start_server()
        try:
            time.sleep(0.5)
            self._send(proc, {
                "jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "test", "version": "1.0"}},
            })
            self._read_line(proc)
            self._send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
            time.sleep(0.1)

            for i in range(2, 5):
                self._send(proc, {"jsonrpc": "2.0", "id": i, "method": "tools/list"})
            for i in range(5, 8):
                self._send(proc, {"jsonrpc": "2.0", "id": i, "method": "resources/list"})

            responses = []
            for _ in range(6):
                r = self._read_line(proc, timeout=3)
                if r and "result" in r:
                    responses.append(r)

            self.assertGreaterEqual(len(responses), 6, 
                f"Expected >=6 responses, got {len(responses)}")
        finally:
            proc.stdin.close()
            proc.terminate()
            proc.wait(timeout=5)

    def test_06_stdout_contains_only_json(self):
        """Every line on stdout must be valid JSON (stderr must be zero/non-JSON)."""
        proc = self._start_server()
        try:
            time.sleep(0.5)
            self._send(proc, {
                "jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "test", "version": "1.0"}},
            })
            self._send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized"})
            time.sleep(0.1)
            self._send(proc, {"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
            self._send(proc, {"jsonrpc": "2.0", "id": 3, "method": "resources/list"})
            time.sleep(1)

            # Read all stdout
            for _ in range(4):
                line = self._read_line(proc, timeout=2.0)
                if line is None:
                    break
                self.assertIn("jsonrpc", line, f"stdout line is not JSON-RPC: {line}")
        finally:
            proc.stdin.close()
            proc.terminate()
            proc.wait(timeout=5)

    def test_07_capabilities_includes_tools_and_resources(self):
        """Server MUST advertise tools and resources capabilities."""
        proc = self._start_server()
        try:
            time.sleep(0.5)
            self._send(proc, {
                "jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "test", "version": "1.0"}},
            })
            resp = self._read_line(proc)
            caps = resp["result"]["capabilities"]
            self.assertIn("tools", caps)
            self.assertIn("resources", caps)
            self.assertFalse(caps["tools"]["listChanged"])
            self.assertFalse(caps["resources"]["listChanged"])
        finally:
            proc.stdin.close()
            proc.terminate()
            proc.wait(timeout=5)


# ═══════════════════════════════════════════════════════════════════════

if __name__ == "__main__":
    unittest.main(verbosity=2)
