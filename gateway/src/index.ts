/**
 * index.ts — WitchModMCP MCP gateway entry point (Node/TS).
 *
 * Port of `mcp_gateway/server.py` over the official MCP SDK:
 *  - Registers `ping` / `reload_tools` / `deploy_mod` core tools up front.
 *  - Discovers the C# mod's real tools via `list_tools` (dynamically, after the
 *    first successful heartbeat) and re-registers them as MCP tools, sending
 *    `notifications/tools/list_changed` so clients refresh their lists.
 *  - Exposes witchSkill docs as MCP resources.
 *  - Runs over stdio.
 *
 * Env: MCP_MOD_PORT, MCP_HEARTBEAT_INTERVAL, MCP_HEARTBEAT_MAX_FAIL,
 *      MCP_DECOMPILE_DIR, MCP_DISABLE_DECOMPILE, MCP_WORKSPACE_DIR.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { loadConfig, type GatewayConfig } from "./config.js";
import { startConsoleServer, type ConsoleServerHandle } from "./consoleServer.js";
import { ModConnection, log as modLog } from "./modClient.js";
import { HeartbeatManager, type HeartbeatResponse } from "./heartbeat.js";
import { DynamicToolRegistrar } from "./tools/dynamicTools.js";
import { registerCoreTools, type GatewayContext } from "./tools/registerCore.js";
import { registerResources } from "./resources.js";
import { mkdirSync } from "node:fs";

function log(msg: string): void {
  console.error(`[gateway] ${msg}`);
}

export async function main(): Promise<void> {
  const config: GatewayConfig = loadConfig();
  log(`Mod port: ${config.modPort}`);
  log(`Workspace: ${config.workspaceDir}`);

  const mod = new ModConnection(config.modPort);

  const server = new McpServer(
    { name: "witch-mod-mcp-gateway", version: "3.0.0" },
    {
      instructions:
        "WitchModMCP gateway server — proxies MCP tools to the game mod. " +
        "Tools are discovered dynamically after the game mod heartbeat connects; " +
        "wait for notifications/tools/list_changed before calling game-mod tools. " +
        "If a tool returns 'Game mod is not reachable', start the game with the " +
        "WitchModMCP mod loaded.",
    },
  );

  // Browser Lua console (CodeMirror page served locally; WS exec stays in-game).
  let consoleServer: ConsoleServerHandle | null = null;
  if (!config.disableConsole) {
    try {
      consoleServer = await startConsoleServer({
        port: config.consolePort,
        modPort: config.modPort,
      });
      if (consoleServer) log(`Lua console: ${consoleServer.url}`);
    } catch (e) {
      log(`console server failed to start: ${(e as Error).message}`);
    }
  } else {
    log("Lua console disabled (MCP_DISABLE_CONSOLE=1)");
  }

  // Shared state fed to core tools.
  let cachedGamePath = "";
  const registrar = new DynamicToolRegistrar(server, mod);
  const ctx: GatewayContext = {
    server,
    mod,
    registrar,
    cachedGamePath,
    consoleUrl: consoleServer?.url ?? "",
    setCachedGamePath: (p) => {
      cachedGamePath = p;
      log(`cached game path: ${p}`);
    },
  };

  const coreCount = registerCoreTools(ctx);
  log(`Registered ${coreCount} core tools (ping/reload_tools/deploy_mod/open_console)`);

  // Sync discovery at startup (works if the mod is already running).
  try {
    const disc = await mod.callTool("list_tools", {});
    if (!disc.error) {
      const tools = (disc.result as { tools?: { name: string }[] })?.tools;
      if (tools && tools.length > 0) {
        const n = registrar.registerToolList(tools);
        log(`Registered ${n} C# tools (sync startup discovery)`);
      } else {
        log("C# mod returned empty tool list — only core tools available");
      }
    } else {
      log(`C# mod responded with error: ${disc.error.message}`);
    }
  } catch (e) {
    log(`C# mod not reachable at startup (${(e as Error).message}) — only core until heartbeat`);
  }

  const resourceCount = registerResources({ workspaceDir: config.workspaceDir, server });
  if (resourceCount > 0) {
    log(`Registered ${resourceCount} skill doc resources`);
  }

  // ── Heartbeat wiring ──
  let lastToolCount = 0;
  let lastReloadCount = 0;
  const heartbeat = new HeartbeatManager({
    sendHeartbeat: (wsDir) => mod.sendHeartbeat(wsDir),
    workspaceDir: config.workspaceDir,
    interval: config.heartbeatInterval,
    maxFailures: config.heartbeatMaxFailures,
    onFirstHeartbeat: async (resp) => {
      lastToolCount = resp.toolCount ?? 0;
      lastReloadCount = resp.reloadCount ?? 0;
      log(`First heartbeat — sessionId=${resp.sessionId ?? "?"}, toolCount=${lastToolCount}`);

      registratorSync(mod, config, ctx, registrar, server);

      try {
        server.sendToolListChanged();
        log("sent notifications/tools/list_changed");
      } catch (e) {
        log(`send tools/list_changed failed: ${(e as Error).message}`);
      }
    },
    onHeartbeat: async (resp: HeartbeatResponse) => {
      const tc = resp.toolCount ?? 0;
      const rc = resp.reloadCount ?? 0;
      let changed = false;
      if (tc !== lastToolCount && tc > 0) {
        log(`Heartbeat: toolCount changed from ${lastToolCount} to ${tc}`);
        lastToolCount = tc;
        changed = true;
      }
      if (rc !== lastReloadCount) {
        log(`Heartbeat: reloadCount changed from ${lastReloadCount} to ${rc}`);
        lastReloadCount = rc;
        changed = true;
      }
      if (!changed) return;

      registratorSync(mod, config, ctx, registrar, server);
      try {
        server.sendToolListChanged();
      } catch (e) {
        log(`send tools/list_changed failed: ${(e as Error).message}`);
      }
    },
  });
  heartbeat.start();
  log("Heartbeat manager started — background retries until game mod responds");

  // ── Run stdio transport ──
  const transport = new StdioServerTransport();
  // The SDK's connect() resolves as soon as the transport starts (it only
  // registers stdin listeners), so waiting on it would let the finally block
  // below tear down the console server immediately after startup. Instead,
  // wait until the client disconnects: the SDK chains this onclose callback
  // (set before connect) into its own close handling.
  let sessionEnded: () => void = () => {};
  const sessionEnd = new Promise<void>((resolve) => {
    sessionEnded = resolve;
  });
  transport.onclose = () => sessionEnded();
  // The SDK transport only listens for stdin 'data'/'error' — if a client
  // disconnects without erroring, neither fires and the process would linger.
  // Watch the raw stream too so the gateway exits cleanly on EOF.
  process.stdin.on("end", sessionEnded);
  process.stdin.on("close", sessionEnded);
  try {
    await server.connect(transport);
    await sessionEnd;
  } finally {
    log("Shutting down...");
    heartbeat.stop();
    if (consoleServer) {
      await consoleServer.close().catch((e) => log(`console server close failed: ${(e as Error).message}`));
    }
    log("Gateway stopped.");
  }
}

/**
 * Fresh re-registration: clear dynamic tools, register all C# tools, cache the
 * game path, and trigger a decompile (unless disabled).
 */
function registratorSync(
  mod: ModConnection,
  config: GatewayConfig,
  ctx: GatewayContext,
  registrar: DynamicToolRegistrar,
  server: McpServer,
): void {
  try {
    registrar.unregisterDynamicTools();
  } catch (e) {
    log(`unregister_dynamic_tools failed: ${(e as Error).message}`);
  }

  // Re-register dynamically. registerDynamicTools is async; run it and log the result.
  void registrar
    .registerDynamicTools()
    .then((count) => log(`register_dynamic_tools: ${count} tools registered`))
    .catch((e) => log(`register_dynamic_tools failed: ${(e as Error).message}`));

  // Cache game install path for deploy_mod.
  void mod
    .callTool("get_game_info", {})
    .then((resp) => {
      if (!resp.error) {
        const gr = (resp.result as { gameRoot?: string })?.gameRoot ?? "";
        if (gr) ctx.setCachedGamePath(gr);
      }
    })
    .catch(() => undefined);

  triggerDecompile(mod, config);
}

function triggerDecompile(mod: ModConnection, config: GatewayConfig): void {
  if (config.disableDecompile) {
    log("  decompile skipped (MCP_DISABLE_DECOMPILE=1)");
    return;
  }
  try {
    mkdirSync(config.decompileDir, { recursive: true });
  } catch {
    /* fine */
  }
  void mod
    .callTool("decompile_source", { outputDir: config.decompileDir })
    .then((resp) => {
      if (resp.error) {
        log(`  decompile error: ${resp.error.message}`);
        return;
      }
      const result = (resp.result ?? {}) as Record<string, unknown>;
      log(`  decompile_source: ${String(result.status ?? "unknown")}`);
      if (result.error) log(`  decompile error: ${String(result.error)}`);
    })
    .catch((e) => log(`  decompile_source failed: ${(e as Error).message}`));
}

// NOTE: `main` is invoked by the launcher (gateway/bin/gateway.js) via an
// explicit `await main()`. It is NOT auto-invoked here so that importing this
// module never silently starts the server. Run via `npm start` /
// `node gateway/bin/gateway.js` (or import and call main() — it is exported).
