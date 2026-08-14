/**
 * config.ts — environment configuration for the gateway.
 *
 * Mirrors the env vars consumed by the Python gateway (server.py) plus an
 * optional MCP_WORKSPACE_DIR to pin the workspace root.
 */
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { env } from "node:process";
import { fileURLToPath } from "node:url";

const THIS_FILE = fileURLToPath(import.meta.url);

export interface GatewayConfig {
  /** Game mod HTTP JSON-RPC port (default 3100). */
  modPort: number;
  /** Heartbeat interval in seconds (default 5). */
  heartbeatInterval: number;
  /** Consecutive failures before the mod is marked disconnected (default 3). */
  heartbeatMaxFailures: number;
  /** Decompile cache dir (default: <workspace>/.cache/game_src). */
  decompileDir: string;
  /** Skip the automatic decompile on first heartbeat. */
  disableDecompile: boolean;
  /** Absolute workspace root (contains both `gateway/` and `mcp_gateway/`). */
  workspaceDir: string;
}

function numOr(fallback: number, ...keys: string[]): number {
  for (const k of keys) {
    const raw = env[k];
    if (raw !== undefined && raw !== "" && Number.isFinite(Number(raw))) {
      return Number(raw);
    }
  }
  return fallback;
}

/** Walk up from this file looking for a dir containing `mcp_gateway`. */
function findWorkspaceFromThisFile(): string {
  let dir = dirname(THIS_FILE); // .../gateway/src
  dir = dirname(dir); // .../gateway
  for (let i = 0; i < 6; i++) {
    dir = dirname(dir); // .../workspace -> parent -> ...
    if (existsSync(join(dir, "mcp_gateway"))) return dir;
  }
  // Fallback: the `gateway` package root (contains src/).
  return dirname(dirname(THIS_FILE));
}

export function loadConfig(): GatewayConfig {
  const workspaceDir = env["MCP_WORKSPACE_DIR"] ?? findWorkspaceFromThisFile();
  return {
    modPort: numOr(3100, "MCP_MOD_PORT"),
    heartbeatInterval: numOr(5, "MCP_HEARTBEAT_INTERVAL"),
    heartbeatMaxFailures: numOr(3, "MCP_HEARTBEAT_MAX_FAIL"),
    decompileDir: env["MCP_DECOMPILE_DIR"] ?? join(workspaceDir, ".cache", "game_src"),
    disableDecompile: env["MCP_DISABLE_DECOMPILE"] === "1",
    workspaceDir,
  };
}
