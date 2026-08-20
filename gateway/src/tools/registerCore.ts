/**
 * registerCore.ts — always-available core tools (ping / reload_tools / deploy_mod).
 *
 * Port of `mcp_gateway/tools.py::register_core_tools`. These exist even before
 * the heartbeat connects. reload_tools and deploy_mod need access to the
 * shared registrar / mod / heartbeat, supplied via a context object.
 */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { execFileSync, spawn } from "node:child_process";
import { platform } from "node:process";
import { existsSync, readdirSync, rmSync, cpSync } from "node:fs";
import { basename, join, normalize } from "node:path";
import { z } from "zod";
import type { ModConnection } from "../modClient.js";
import type { DynamicToolRegistrar } from "./dynamicTools.js";

export interface GatewayContext {
  server: McpServer;
  mod: ModConnection;
  registrar: DynamicToolRegistrar;
  /** Resolved (possibly empty) cached game install path. */
  cachedGamePath: string;
  /** Browser Lua console URL ("" when the console server is not running). */
  consoleUrl: string;
  setCachedGamePath(path: string): void;
}

const textContent = (text: string) => ({ content: [{ type: "text" as const, text }] });

export function registerCoreTools(ctx: GatewayContext): number {
  const { server, registrar } = ctx;

  server.registerTool(
    "ping",
    {
      description:
        "Simple ping-pong test. Returns {ok: true} — verifies the gateway process is alive. " +
        "Does NOT verify the game mod is reachable; use list_tools (after heartbeat) for that.",
      inputSchema: z.object({}),
    },
    async () => {
      return textContent(JSON.stringify({ ok: true }));
    },
  );

  server.registerTool(
    "reload_tools",
    {
      description:
        "热重载所有 MCP 工具并同步更新网关 schema。调用后 C# 端重新加载工具 DLL，" +
        "网关自动重新拉取最新的 inputSchema，使新增参数立即生效。",
      inputSchema: z.object({}),
    },
    async () => {
      const resp = await ctx.mod.callTool("reload_tools", {});
      if (resp.error) {
        return textContent(JSON.stringify(resp.error));
      }
      const csharp = resp.result ?? resp;

      // Re-sync schema from mod.
      try {
        const listResp = await ctx.mod.callTool("list_tools", {});
        if (!listResp.error) {
          const tools = (listResp.result as { tools?: { name: string }[] })?.tools;
          if (tools && tools.length > 0) {
            registrar.unregisterDynamicTools();
            const n = registrar.registerToolList(tools);
            console.error(`reload_tools: schema re-sync registered ${n} tools`);
            try {
              ctx.server.sendToolListChanged();
              console.error("reload_tools: sent tools/list_changed");
            } catch (e) {
              console.error(`reload_tools: list_changed failed: ${(e as Error).message}`);
            }
          }
        }
      } catch (e) {
        console.error(`reload_tools: schema re-sync failed: ${(e as Error).message}`);
      }

      return textContent(JSON.stringify(csharp, null, 2));
    },
  );

  registerDeployTool(ctx);
  registerOpenConsoleTool(ctx);

  return 4; // ping + reload_tools + deploy_mod + open_console
}

/** Open the gateway-hosted Lua console page in the default browser. */
function registerOpenConsoleTool(ctx: GatewayContext): void {
  const { server } = ctx;

  server.registerTool(
    "open_console",
    {
      description:
        "在默认浏览器中打开 Lua 控制台页面（网关托管的 CodeMirror 版 REPL，" +
        "通过游戏内 WebSocket 执行 Lua）。返回控制台 URL。",
      inputSchema: z.object({}),
    },
    async () => {
      const url = ctx.consoleUrl;
      if (!url) {
        return textContent(
          JSON.stringify({ error: "console server is not running (MCP_DISABLE_CONSOLE=1?)" }),
        );
      }
      try {
        if (platform === "win32") {
          spawn("cmd", ["/c", "start", "", url], { detached: true, stdio: "ignore" }).unref();
        } else if (platform === "darwin") {
          spawn("open", [url], { detached: true, stdio: "ignore" }).unref();
        } else {
          spawn("xdg-open", [url], { detached: true, stdio: "ignore" }).unref();
        }
        return textContent(JSON.stringify({ ok: true, url }));
      } catch (e) {
        return textContent(
          JSON.stringify({ error: "failed to open browser: " + ((e as Error).message ?? e) }),
        );
      }
    },
  );
}

function registerDeployTool(ctx: GatewayContext): void {
  const { server } = ctx;

  server.registerTool(
    "deploy_mod",
    {
      description:
        "部署 Mod 到游戏并重启验证。将指定文件夹复制到游戏的 Mods 目录，重启游戏，等待重新连接，" +
        "然后验证 Mod 是否加载成功并检查日志错误。支持 Windows。",
      inputSchema: z.object({
        mod_path: z.string().describe("Mod 文件夹路径（如 E:\\WitchModPlayer\\Nightwatcher）"),
        game_path: z.string().optional().describe("游戏安装路径（可选，自动检测 Steam 安装位置）"),
        restart_delay: z.number().int().optional().describe("重启前等待秒数（默认 5）"),
      }),
    },
    async (args) => {
      const { mod_path, game_path, restart_delay } = args;
      return textContent(
        await deployMod(ctx, mod_path, game_path, restart_delay ?? 5),
      );
    },
  );
}

/** Port of `mcp_gateway/tools.py::deploy_mod`. Returns a JSON string. */
async function deployMod(
  ctx: GatewayContext,
  modPath: string,
  gamePath?: string,
  restartDelay = 5,
): Promise<string> {
  if (platform !== "win32") {
    return JSON.stringify({ success: false, error: `deploy_mod not available on ${platform}` });
  }

  const modName = basename(normalize(modPath));
  if (!modName || !existsSync(modPath) || !isDir(modPath)) {
    return JSON.stringify({ success: false, error: `Invalid mod path: ${modPath}` });
  }

  // ── Resolve game path ──
  let resolved = gamePath || ctx.cachedGamePath;
  if (!resolved || !existsSync(resolved) || !isDir(resolved)) {
    try {
      const resp = await ctx.mod.callTool("get_game_info", {});
      if (!resp.error) {
        const gr = (resp.result as { gameRoot?: string })?.gameRoot ?? "";
        if (gr && existsSync(gr) && isDir(gr)) resolved = gr;
      }
    } catch {
      /* ignore */
    }
  }
  if (!resolved || !existsSync(resolved) || !isDir(resolved)) {
    for (const sp of commonSteamPaths()) {
      if (existsSync(sp) && isDir(sp)) {
        resolved = sp;
        break;
      }
    }
  }
  if (!resolved || !existsSync(resolved) || !isDir(resolved)) {
    return JSON.stringify({
      success: false,
      error: "Cannot find game install path. Provide game_path or connect to game first.",
    });
  }
  ctx.setCachedGamePath(resolved);

  // ── Determine data dir and mods dir ──
  let dataDir: string | undefined;
  for (const d of readdirSync(resolved)) {
    const full = join(resolved, d);
    if (isDir(full) && d.endsWith("_Data")) {
      dataDir = full;
      break;
    }
  }
  if (!dataDir) {
    return JSON.stringify({ success: false, error: `Cannot find _Data directory under ${resolved}` });
  }
  const modsDir = join(dataDir, "Mods");
  if (!existsSync(modsDir) || !isDir(modsDir)) {
    return JSON.stringify({ success: false, error: `Mods directory not found: ${modsDir}` });
  }
  const targetDir = join(modsDir, modName);

  // ── Copy mod files ──
  try {
    if (existsSync(targetDir)) fspRemoveRecursive(targetDir);
    fspCopyRecursive(modPath, targetDir);
    console.error(`deploy_mod: copied ${modPath} → ${targetDir}`);
  } catch (e) {
    return JSON.stringify({ success: false, error: `Failed to copy mod: ${(e as Error).message}` });
  }

  // ── Find game executable ──
  const gameExe = findGameExe(resolved);
  if (!gameExe) {
    return JSON.stringify({ success: false, error: `Game executable not found in ${resolved}` });
  }
  const exeName = basename(gameExe);

  // ── Kill game process ──
  try {
    execFileSync("taskkill", ["/F", "/IM", exeName], { stdio: "ignore", timeout: 10_000 });
    console.error(`deploy_mod: killed ${exeName}`);
  } catch {
    /* may already be dead */
  }

  await sleep(1000);
  for (let i = 0; i < 30; i++) {
    try {
      const out = execFileSync("tasklist", ["/FI", `IMAGENAME eq ${exeName}`], {
        encoding: "utf8",
        timeout: 5_000,
      });
      if (!out.includes(exeName)) break;
    } catch {
      /* break condition unknown */
    }
    await sleep(500);
  }

  await sleep(restartDelay * 1000);

  // ── Start game process ──
  try {
    spawn(gameExe, [], { shell: true, detached: true, stdio: "ignore" }).unref();
    console.error(`deploy_mod: started ${gameExe}`);
  } catch (e) {
    return JSON.stringify({
      success: true,
      warning: `Mod deployed but game start failed: ${(e as Error).message}. Start manually.`,
      modName,
      targetPath: targetDir,
    });
  }

  // ── Wait for heartbeat ──
  let connected = false;
  for (let i = 0; i < 60; i++) {
    await sleep(1000);
    try {
      const r = await ctx.mod.callTool("list_tools", {});
      if (!r.error) {
        connected = true;
        await sleep(2000);
        break;
      }
    } catch {
      /* not ready yet */
    }
  }
  if (!connected) {
    return JSON.stringify({
      success: true,
      warning: "Mod files deployed but game did not reconnect within 60s. Start manually.",
      modName,
      targetPath: targetDir,
    });
  }

  // ── Verify mod loaded ──
  const errors: string[] = [];
  let modFound = false;
  try {
    const ms = await ctx.mod.callTool("dump_mod_state", {});
    if (!ms.error) {
      const state = ms.result as Record<string, unknown>;
      for (const key of ["mods", "loadedMods", "activeMods"]) {
        const lst = state[key];
        if (Array.isArray(lst)) {
          for (const m of lst) {
            const mname =
              typeof m === "object" && m !== null
                ? ((m as Record<string, unknown>).name as string) ??
                  ((m as Record<string, unknown>).modName as string) ??
                  ""
                : String(m ?? "");
            if (mname.toLowerCase().includes(modName.toLowerCase())) {
              modFound = true;
              break;
            }
          }
        }
      }
    }
  } catch (e) {
    errors.push(`dump_mod_state error: ${(e as Error).message}`);
  }

  try {
    const logs = await ctx.mod.callTool("get_recent_logs", { count: 200, level: "Error" });
    if (!logs.error && Array.isArray(logs.result)) {
      for (const entry of logs.result as Array<Record<string, unknown>>) {
        const msg = String(entry.message ?? "");
        if (msg.toLowerCase().includes(modName.toLowerCase())) errors.push(msg);
      }
    }
  } catch (e) {
    errors.push(`log check error: ${(e as Error).message}`);
  }

  return JSON.stringify(
    {
      success: true,
      modName,
      sourcePath: modPath,
      targetPath: targetDir,
      modLoaded: modFound,
      errors: errors.length ? errors : null,
    },
    null,
    2,
  );
}

// ── helpers ──

function isDir(p: string): boolean {
  try {
    return existsSync(p) && readdirSync(p) !== null && true;
  } catch {
    return false;
  }
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function commonSteamPaths(): string[] {
  const base = "Witch's Apocalyptic Journey";
  return [
    `F:\\steam\\steamapps\\common\\${base}`,
    process.env["ProgramFiles"] ? `${process.env["ProgramFiles"]}\\Steam\\steamapps\\common\\${base}` : "",
    process.env["ProgramFiles(x86)"] ? `${process.env["ProgramFiles(x86)"]}\\Steam\\steamapps\\common\\${base}` : "",
    `D:\\steam\\steamapps\\common\\${base}`,
  ].filter(Boolean);
}

function findGameExe(resolved: string): string | null {
  const candidates: string[] = [];
  const product = basename(resolved);
  candidates.push(join(resolved, `${product}.exe`));
  candidates.push(join(resolved, `${product}.app`, "Contents", "MacOS", product));
  for (const f of readdirSync(resolved)) {
    if (f.toLowerCase().endsWith(".exe")) candidates.push(join(resolved, f));
  }
  for (const c of candidates) {
    if (existsSync(c)) return c;
  }
  return null;
}

function fspRemoveRecursive(dir: string): void {
  rmSync(dir, { recursive: true, force: true });
}
function fspCopyRecursive(from: string, to: string): void {
  cpSync(from, to, { recursive: true, force: true });
}
