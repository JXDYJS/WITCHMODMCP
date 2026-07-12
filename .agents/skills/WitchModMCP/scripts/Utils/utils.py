"""Read-only helpers for decompiled source access.

Decompilation is handled by the DeveloperTools mod's `decompile_source` tool.
This module provides lightweight freshness checks against the cached manifest.
"""

import json
import hashlib
from pathlib import Path
from typing import Optional

DLL_NAMES = ["Witch.dll", "Witch.Core.dll"]
MCP_PORT = 3100


def _call_decompile_source(output_dir: str) -> dict:
    """Call the C# server's decompile_source tool and return parsed result."""
    import http.client

    body = json.dumps({
        "jsonrpc": "2.0", "id": 1, "method": "decompile_source",
        "params": {"outputDir": output_dir, "force": False}
    })
    conn = http.client.HTTPConnection("localhost", MCP_PORT, timeout=180)
    try:
        conn.request("POST", "/", body, {"Content-Type": "application/json"})
        resp = conn.getresponse()
        data = json.loads(resp.read().decode("utf-8"))
        return data.get("Result") or data.get("result") or {}
    finally:
        conn.close()


def ensure_src_updated(output_dir: str) -> Optional[str]:
    """Call decompile_source to ensure cache is fresh, return output_dir or None."""
    result = _call_decompile_source(output_dir)
    if result.get("error"):
        print(f"[witch-mod-mcp] decompile_source error: {result['error']}")
        return None
    print(f"[witch-mod-mcp] source {result.get('status', '?')}: {output_dir}")
    return output_dir


def verify_source_fresh(output_dir: str) -> tuple[bool, str]:
    """Check manifest freshness without triggering decompilation.

    Returns (is_fresh, reason_string).
    """
    manifest_path = Path(output_dir) / ".decompile_manifest.json"
    if not manifest_path.is_file():
        return False, "no manifest found — call decompile_source first"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        ts = manifest.get("lastDecompileTime", "unknown")
        return True, f"last decompiled {ts}"
    except Exception as e:
        return False, f"manifest read failed: {e}"
