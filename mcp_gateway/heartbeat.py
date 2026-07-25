#!/usr/bin/env python3
"""
heartbeat — Background heartbeat thread for the gateway.

Sends periodic POST /heartbeat via ModConnection to the game mod.
On first heartbeat, triggers the callback (decompile, etc).
After consecutive failures, marks the mod as disconnected.
"""

import sys
import threading
import time
from typing import Callable

from mcp_gateway.mod_client import ModConnection

Callback = Callable[[dict], None]


class HeartbeatManager:
    DEFAULT_INTERVAL = 5.0
    DEFAULT_MAX_FAILURES = 3

    def __init__(
        self,
        mod_conn: ModConnection,
        workspace_dir: str,
        on_first_heartbeat: Callback | None = None,
        on_heartbeat: Callback | None = None,
        interval: float | None = None,
        max_failures: int | None = None,
    ):
        self.mod: ModConnection = mod_conn
        self.workspace_dir = workspace_dir
        self.on_first_heartbeat = on_first_heartbeat
        self.on_heartbeat = on_heartbeat
        self.interval = max(interval or self.DEFAULT_INTERVAL, 0.1)
        self.max_failures = max(max_failures or self.DEFAULT_MAX_FAILURES, 1)

        self._thread: threading.Thread | None = None
        self._stop = threading.Event()
        self._lock = threading.Lock()

        self._connected = False
        self._first_heartbeat_done = False
        self._consecutive_failures = 0
        self._last_response: dict | None = None
        self._session_id: str | None = None

    @property
    def connected(self) -> bool:
        with self._lock:
            return self._connected

    @property
    def first_heartbeat_done(self) -> bool:
        with self._lock:
            return self._first_heartbeat_done

    @property
    def session_id(self) -> str | None:
        with self._lock:
            return self._session_id

    def start(self):
        if self._thread and self._thread.is_alive():
            return
        self._stop.clear()
        self._thread = threading.Thread(target=self._run, daemon=True, name="heartbeat")
        self._thread.start()

    def stop(self):
        self._stop.set()
        if self._thread:
            self._thread.join(timeout=3)

    def _run(self):
        while not self._stop.is_set():
            ok, resp = self.mod.send_heartbeat(self.workspace_dir)

            with self._lock:
                if ok:
                    self._connected = True
                    self._consecutive_failures = 0
                    self._last_response = resp

                    if not self._first_heartbeat_done:
                        self._first_heartbeat_done = True
                        self._session_id = resp.get("sessionId") if resp else None
                        triggered = True
                    else:
                        triggered = False
                else:
                    self._consecutive_failures += 1
                    if self._consecutive_failures >= self.max_failures:
                        self._connected = False
                    triggered = False

            if resp:
                cb = self.on_first_heartbeat if triggered else self.on_heartbeat
                if cb:
                    try:
                        cb(resp)
                    except Exception as e:
                        print(
                            f"[heartbeat] callback error: {e}",
                            file=sys.stderr,
                            flush=True,
                        )

            self._stop.wait(self.interval)
