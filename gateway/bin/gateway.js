#!/usr/bin/env node
// Launcher for the WitchModMCP MCP gateway.
//
// On Windows, dynamic `import(<absolute path string>)` fails because a drive
// path like "E:\..." is treated as an unsupported "e:" URL scheme, so we must
// convert the entry to a proper file:// URL before importing.
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const entry = pathToFileURL(join(__dirname, "..", "dist", "index.js")).href;

try {
  // The entry module EXPORTS main() — we call it explicitly so that starting
  // via this launcher never falls through the "is this the direct entry?"
  // check (which would mismatch argv[1] and leave the server idling forever).
  const { main } = await import(entry);
  await main();
} catch (err) {
  // Surface a clear startup error instead of a bare module-not-found.
  console.error("[gateway] failed to run gateway:", entry);
  console.error(err);
  process.exit(1);
}
