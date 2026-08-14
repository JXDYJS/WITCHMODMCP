/**
 * heartbeat.ts — background heartbeat for the gateway.
 *
 * Port of `mcp_gateway/heartbeat.py`. Node is single-threaded, so instead of a
 * dedicated daemon thread we use a `setInterval`. On the first successful
 * heartbeat a one-time callback fires; afterwards `onHeartbeat` fires only on
 * tool/reload count changes. After `maxFailures` consecutive failures the mod
 * is marked disconnected.
 *
 * Thread-safety (of the async registry mutations the Python version had to
 * schedule) is handled by the caller: all callbacks are invoked synchronously
 * from this interval tick, and dynamic-tool registration is expected to run on
 * the same synchronous flow.
 */
export interface HeartbeatResponse {
  sessionId?: string;
  toolCount?: number;
  reloadCount?: number;
  [key: string]: unknown;
}

export type HeartbeatCallback = (resp: HeartbeatResponse) => void | Promise<void>;

export interface HeartbeatManagerOptions {
  sendHeartbeat: (workspaceDir: string) => Promise<[boolean, Record<string, unknown>]>;
  workspaceDir: string;
  onFirstHeartbeat?: HeartbeatCallback;
  onHeartbeat?: HeartbeatCallback;
  interval?: number;
  maxFailures?: number;
}

export class HeartbeatManager {
  readonly DEFAULT_INTERVAL = 5;
  readonly DEFAULT_MAX_FAILURES = 3;

  private readonly sendHeartbeat: HeartbeatManagerOptions["sendHeartbeat"];
  private readonly workspaceDir: string;
  private readonly onFirstHeartbeat?: HeartbeatCallback;
  private readonly onHeartbeat?: HeartbeatCallback;
  private readonly intervalMs: number;
  private readonly maxFailures: number;

  private _timer: ReturnType<typeof setInterval> | null = null;
  private _connected = false;
  private _firstHeartbeatDone = false;
  private _consecutiveFailures = 0;
  private _sessionId: string | undefined;

  constructor(opts: HeartbeatManagerOptions) {
    this.sendHeartbeat = opts.sendHeartbeat;
    this.workspaceDir = opts.workspaceDir;
    this.onFirstHeartbeat = opts.onFirstHeartbeat;
    this.onHeartbeat = opts.onHeartbeat;
    this.intervalMs = Math.max(opts.interval ?? this.DEFAULT_INTERVAL, 0.1) * 1000;
    this.maxFailures = Math.max(opts.maxFailures ?? this.DEFAULT_MAX_FAILURES, 1);
  }

  get connected(): boolean {
    return this._connected;
  }

  get firstHeartbeatDone(): boolean {
    return this._firstHeartbeatDone;
  }

  get sessionId(): string | undefined {
    return this._sessionId;
  }

  start(): void {
    if (this._timer !== null) return;
    void this.tick();
    this._timer = setInterval(() => void this.tick(), this.intervalMs);
    // Do not keep the Node process alive solely for the heartbeat.
    if (typeof this._timer.unref === "function") {
      this._timer.unref();
    }
  }

  stop(): void {
    if (this._timer !== null) {
      clearInterval(this._timer);
      this._timer = null;
    }
  }

  private async tick(): Promise<void> {
    const [ok, resp] = await this.sendHeartbeat(this.workspaceDir);
    const hb = (resp ?? {}) as HeartbeatResponse;

    let triggered: "first" | "change" | null = null;
    if (ok) {
      this._connected = true;
      this._consecutiveFailures = 0;
      if (!this._firstHeartbeatDone) {
        this._firstHeartbeatDone = true;
        this._sessionId = hb.sessionId;
        triggered = "first";
      } else {
        triggered = "change";
      }
    } else {
      this._consecutiveFailures += 1;
      if (this._consecutiveFailures >= this.maxFailures) {
        this._connected = false;
      }
    }

    const cb =
      triggered === "first" ? this.onFirstHeartbeat : triggered === "change" ? this.onHeartbeat : null;
    if (cb) {
      try {
        await cb(hb);
      } catch (err) {
        console.error("[heartbeat] callback error:", err);
      }
    }
  }
}
