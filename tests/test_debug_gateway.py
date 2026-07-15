"""Debug: trace the exact HTTP body sent by mod_client to the C# mod."""
import http.client, json, sys

# Monkey-patch before any gateway imports
import mcp_gateway.mod_client as mc

_orig_request = mc.ModConnection._request
def _patched_request(self, method, path, body=None, auth=False, timeout=5):
    sys.stderr.write(f"[DBG] HTTP {method} {path} auth={auth}\n")
    sys.stderr.write(f"[DBG] body: {body[:300]}\n")
    status, data = _orig_request(self, method, path, body, auth, timeout)
    sys.stderr.write(f"[DBG] response status={status}\n")
    sys.stderr.write(f"[DBG] response: {data[:500]}\n")
    return status, data
mc.ModConnection._request = _patched_request

# Now import and test
from mcp_gateway.server import main

if __name__ == "__main__":
    main()
