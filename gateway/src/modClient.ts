/**
 * modClient.ts — HTTP client for the WitchModMCP in-game JSON-RPC server.
 *
 * Port of `mcp_gateway/mod_client.py`. Communication with the C# mod goes
 * through this module. Node uses the global `fetch` (no extra dependency).
 */
import { lowerFirstKey } from "./tools/camelCase.js";

export const DEFAULT_MOD_PORT = 3100;

// Long guard for unusual mod responses; all current C# tools are asynchronous
// and return quickly (see DecompileSourceTool etc.), so the old Python 120s
// default is intentionally NOT reproduced. Heartbeat uses a smaller timeout.
const TOOL_TIMEOUT_MS = 30_000;
const HEARTBEAT_TIMEOUT_MS = 5_000;

export interface ToolLogFn {
  (msg: string): void;
}

/** @param msg log to stderr (never stdout — stdout is the MCP channel). */
export function log(msg: string): void {
  console.error(`[mod_client] ${msg}`);
}

export interface ModToolResponse {
  jsonrpc?: string;
  id?: number | string;
  result?: unknown;
  error?: { code: number; message: string };
}

export class ModConnection {
  readonly port: number;
  private _idCounter = 0;

  constructor(port: number) {
    this.port = port;
  }

  private nextId(): number {
    return ++this._idCounter;
  }

  private async request(
    method: string,
    path: string,
    body: string | null,
    timeoutMs: number,
  ): Promise<[number, string]> {
    const url = `http://localhost:${this.port}${path}`;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    try {
      const resp = await fetch(url, {
        method,
        headers: body !== null ? { "Content-Type": "application/json" } : undefined,
        body: body !== null ? body : undefined,
        signal: controller.signal,
      });
      const text = await resp.text();
      return [resp.status, text];
    } finally {
      clearTimeout(timer);
    }
  }

  /**
   * POST a JSON-RPC tool call to the mod.
   * Returns the normalised JSON-RPC response dict. Keys are normalised from
   * PascalCase (C# Newtonsoft) to camelCase.
   */
  async callTool(method: string, params?: Record<string, unknown> | null): Promise<ModToolResponse> {
    const reqId = this.nextId();
    const body = JSON.stringify({
      jsonrpc: "2.0",
      id: reqId,
      method,
      params: params ?? {},
    });
    try {
      const [status, raw] = await this.request("POST", "/", body, TOOL_TIMEOUT_MS);
      if (status !== 200) {
        return {
          jsonrpc: "2.0",
          error: {
            code: -32000,
            message: `Mod returned HTTP ${status}: ${raw.slice(0, 200)}`,
          },
        };
      }
      const data = JSON.parse(raw);
      return lowerFirstKey(data) as ModToolResponse;
    } catch (err) {
      return {
        jsonrpc: "2.0",
        error: {
          code: err instanceof SyntaxError ? -32700 : -32000,
          message:
            err instanceof SyntaxError
              ? "Invalid JSON response from mod"
              : `Mod connection failed: ${(err as Error).message}`,
        },
      };
    }
  }

  /**
   * POST /heartbeat — send a heartbeat to the mod (no auth).
   * Returns [ok, responseDict]; ok is true when status==200 and status=="ok".
   */
  async sendHeartbeat(workspaceDir: string): Promise<[boolean, Record<string, unknown>]> {
    const body = JSON.stringify({
      workspacePath: workspaceDir,
      pid: process.pid,
      keepalive: true,
    });
    try {
      const [status, raw] = await this.request("POST", "/heartbeat", body, HEARTBEAT_TIMEOUT_MS);
      const data = JSON.parse(raw);
      const ok = status === 200 && (data as { status?: string }).status === "ok";
      return [ok, data];
    } catch (err) {
      return [false, { error: (err as Error).message }];
    }
  }
}
