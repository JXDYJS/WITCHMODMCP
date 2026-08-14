/**
 * consoleServer.ts — local static HTTP server for the browser Lua console.
 *
 * The CodeMirror-based console page (console.html + esbuild bundle) ships in
 * this package, so installing the MCP gateway also installs the console:
 * no more npm install inside the mod folder.
 *
 * The page is served on 127.0.0.1:<MCP_CONSOLE_PORT> (default 3200) and
 * connects to the in-game Lua WebSocket on modPort + 1. If the port is
 * already taken (another gateway instance is running), the server reports
 * it and returns null — the MCP gateway itself keeps running.
 */
import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, extname, join, normalize, sep } from "node:path";
import { fileURLToPath } from "node:url";

export interface ConsoleServerOptions {
  /** Port to bind; use 0 for an ephemeral port (tests). */
  port: number;
  /** Game mod JSON-RPC port; the Lua WebSocket runs on modPort + 1. */
  modPort: number;
  /** Directory containing console.html and dist/ (default: <pkg>/console). */
  assetsDir?: string;
}

export interface ConsoleServerHandle {
  port: number;
  url: string;
  close(): Promise<void>;
}

const HOST = "127.0.0.1";

const MIME: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".mjs": "application/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".map": "application/json",
  ".json": "application/json",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".woff2": "font/woff2",
};

/** Locate the console/ asset dir from src/ (tsx dev) or dist/ (compiled). */
export function defaultAssetsDir(): string {
  return join(dirname(fileURLToPath(import.meta.url)), "..", "console");
}

export async function startConsoleServer(
  opts: ConsoleServerOptions,
): Promise<ConsoleServerHandle | null> {
  const assetsDir = normalize(opts.assetsDir ?? defaultAssetsDir());
  const htmlPath = join(assetsDir, "console.html");
  if (!existsSync(htmlPath)) {
    console.error("[console] console.html not found at " + htmlPath + " — console disabled");
    return null;
  }
  const wsPort = opts.modPort + 1;

  const server = createServer((req, res) => {
    void handleRequest(req, res, assetsDir, wsPort).catch((e) => {
      sendError(res, 500, "Internal error: " + ((e as Error).message ?? String(e)));
    });
  });

  const bound: { port: number } = await new Promise((resolve, reject) => {
    server.once("error", (err: NodeJS.ErrnoException) => {
      if (err.code === "EADDRINUSE") {
        console.error(
          "[console] port " + opts.port + " already in use — another gateway instance is " +
          "probably running; open http://" + HOST + ":" + opts.port + "/console",
        );
        resolve({ port: -1 });
      } else {
        reject(err);
      }
    });
    server.listen(opts.port, HOST, () => {
      const addr = server.address();
      resolve({ port: typeof addr === "object" && addr ? addr.port : opts.port });
    });
  });

  if (bound.port === -1) {
    // EADDRINUSE: nothing of ours is listening; close the dead socket.
    server.close();
    return null;
  }

  return {
    port: bound.port,
    url: "http://" + HOST + ":" + bound.port + "/console",
    close: () =>
      new Promise<void>((resolve, reject) =>
        server.close((e) => (e ? reject(e) : resolve())),
      ),
  };
}

async function handleRequest(
  req: IncomingMessage,
  res: ServerResponse,
  assetsDir: string,
  wsPort: number,
): Promise<void> {
  if (req.method !== "GET" && req.method !== "HEAD") {
    sendError(res, 405, "Method not allowed");
    return;
  }

  let path: string;
  try {
    path = decodeURIComponent(new URL(req.url ?? "/", "http://" + HOST).pathname);
  } catch {
    sendError(res, 400, "Bad request");
    return;
  }

  if (path === "/") {
    res.writeHead(301, { Location: "/console" });
    res.end();
    return;
  }

  if (path === "/console" || path === "/console/") {
    const html = await readFile(join(assetsDir, "console.html"), "utf8");
    const served = html.replaceAll("@@WS_PORT@@", String(wsPort));
    res.writeHead(200, { "Content-Type": MIME[".html"], "Cache-Control": "no-store" });
    res.end(served);
    return;
  }

  if (path.startsWith("/dist/")) {
    const distRoot = normalize(join(assetsDir, "dist"));
    const resolved = normalize(join(assetsDir, path));
    // Security: only serve files inside <assetsDir>/dist/.
    if (resolved.startsWith(distRoot + sep) && existsSync(resolved)) {
      const data = await readFile(resolved);
      res.writeHead(200, {
        "Content-Type": MIME[extname(resolved).toLowerCase()] ?? "application/octet-stream",
        "Cache-Control": "no-store",
      });
      res.end(data);
      return;
    }
    sendError(res, 404, "Not found");
    return;
  }

  sendError(res, 404, "Not found");
}

function sendError(res: ServerResponse, status: number, text: string): void {
  res.writeHead(status, { "Content-Type": "text/plain; charset=utf-8" });
  res.end(text);
}
