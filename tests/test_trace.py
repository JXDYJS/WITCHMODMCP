"""Debug play_card issue: trace actual HTTP body from gateway."""
import sys, http.client, json
# Direct trace
from mcp_gateway.mod_client import ModConnection

_orig_req = ModConnection._request

def _trace(self, method, path, body=None, auth=False, timeout=5):
    if body:
        sys.stderr.write(f"[TRACE] HTTP body sent: {body[:400]}\n")
    status, data = _orig_req(self, method, path, body, auth, timeout)
    sys.stderr.write(f"[TRACE] HTTP response ({status}): {data[:400]}\n")
    return status, data

ModConnection._request = _trace

mod = ModConnection(3100, "witch-mod-mcp-dev-2026")

# First set hand
mod.call_tool("set_card_pile", {"pile": "hand", "action": "set", "cards": ["card_1","card_2","card_3"]})

# Then play_card with index and targetIndex
sys.stderr.write("=== call_tool(play_card, {index:0, targetIndex:0}) ===\n")
resp = mod.call_tool("play_card", {"index": 0, "targetIndex": 0})
sys.stderr.write(f"response: {json.dumps(resp, ensure_ascii=False)[:300]}\n")
