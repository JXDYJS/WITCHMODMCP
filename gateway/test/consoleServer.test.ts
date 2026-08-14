import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { startConsoleServer, type ConsoleServerHandle } from "../src/consoleServer.js";

const HTML_FIXTURE =
  "<!doctype html><title>Witch Lua Console</title>" +
  "<script>window.__WS_PORT__ = @@WS_PORT@@;</script>";

describe("consoleServer", () => {
  let dir: string;
  let handle: ConsoleServerHandle | null = null;

  beforeAll(() => {
    dir = mkdtempSync(join(tmpdir(), "witch-console-"));
    mkdirSync(join(dir, "dist"), { recursive: true });
    writeFileSync(join(dir, "console.html"), HTML_FIXTURE);
    writeFileSync(join(dir, "dist", "console.js"), "export const x = 1;");
  });

  afterAll(async () => {
    await handle?.close();
    rmSync(dir, { recursive: true, force: true });
  });

  it("serves console.html with the ws port injected (modPort + 1)", async () => {
    handle = await startConsoleServer({ port: 0, modPort: 3100, assetsDir: dir });
    expect(handle).not.toBeNull();
    const res = await fetch(handle!.url);
    expect(res.status).toBe(200);
    expect(res.headers.get("content-type")).toContain("text/html");
    const html = await res.text();
    expect(html).toContain("Witch Lua Console");
    expect(html).not.toContain("@@WS_PORT@@"); // placeholder replaced
    expect(html).toContain("window.__WS_PORT__ = 3101;"); // 3100 + 1 injected correctly
  });

  it("serves bundled assets under /dist/", async () => {
    const res = await fetch(`http://127.0.0.1:${handle!.port}/dist/console.js`);
    expect(res.status).toBe(200);
    expect(res.headers.get("content-type")).toContain("javascript");
    expect(await res.text()).toContain("export const x");
  });

  it("redirects / to /console", async () => {
    const res = await fetch(`http://127.0.0.1:${handle!.port}/`, { redirect: "manual" });
    expect(res.status).toBe(301);
    expect(res.headers.get("location")).toBe("/console");
  });

  it("404s unknown paths and blocks path traversal", async () => {
    const nope = await fetch(`http://127.0.0.1:${handle!.port}/nope`);
    expect(nope.status).toBe(404);

    // %2e%2e decodes to .. — must not escape the dist/ root
    const trav = await fetch(
      `http://127.0.0.1:${handle!.port}/dist/%2e%2e/console.html`,
    );
    expect(trav.status).toBe(404);

    const outside = await fetch(`http://127.0.0.1:${handle!.port}/dist/../console.html`);
    expect(outside.status).toBe(404);
  });

  it("rejects non-GET methods", async () => {
    const res = await fetch(`http://127.0.0.1:${handle!.port}/console`, { method: "POST" });
    expect(res.status).toBe(405);
  });

  it("falls back to the next free port when the preferred port is taken", async () => {
    const second = await startConsoleServer({
      port: handle!.port, // already bound by the first instance
      modPort: 3100,
      assetsDir: dir,
    });
    expect(second).not.toBeNull();
    expect(second!.port).toBe(handle!.port + 1); // walked up one port
    const res = await fetch(second!.url);
    expect(res.status).toBe(200);
    await second!.close();
  });
});