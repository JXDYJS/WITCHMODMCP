# Toolset auto-update

The MCP toolset (`WitchModMCP.Contracts.dll`) can be hot-updated from a remote manifest
hosted on GitHub Pages. The framework DLL (`WitchModMCP.dll`) is never touched at runtime.

This project reuses its own GitHub repo for Pages — no separate repo is needed.

## How it works

1. `ModConfig.json` declares `UpdateManifestUrl` and the local `ToolsetVersion`.
2. On every game start (or when `update_tools` is called), the tool fetches the manifest.
3. If `manifest.version` differs from `ToolsetVersion`, it downloads `WitchModMCP.Contracts.dll`,
   verifies SHA256, atomically replaces the file on disk, and runs `reload_tools`.
4. Any failure (network, hash mismatch, replace error) is logged as a warning and the
   currently loaded tools keep running.

The framework is **not** updated at runtime — `WitchModMCP.dll` is locked once loaded.
Ship framework changes by re-deploying the whole mod folder and restarting the game.

## One-time Pages setup

1. On the source repo (`https://github.com/JXDYJS/WITCHMODMCP`), open
   **Settings → Pages → Build from branch → `main` / `/ (root)` → Save**.
2. After the first publish the script drops a `.nojekyll` file at the root so GitHub Pages
   serves the manifest and DLL as raw files (instead of running them through Jekyll).
3. Pages URL becomes:
   ```
   https://JXDYJS.github.io/WITCHMODMCP/manifest.json
   https://JXDYJS.github.io/WITCHMODMCP/WitchModMCP.Contracts.dll
   ```

## Publishing a new toolset

```powershell
# Pages repo == source repo: clone (or use an existing clone) and point the script at it.
git clone https://github.com/JXDYJS/WITCHMODMCP ../WITCHMODMCP-pages
pwsh scripts/publish-tools.ps1 -PagesRepo ../WITCHMODMCP-pages -Version 1.0.0
```

The script:
- runs `dotnet build -p:BuildPart=Tools` (fast — no framework rebuild)
- copies the DLL into the Pages repo
- writes `manifest.json` with SHA256 + size
- commits and pushes

The script will prompt for the Pages base URL on first run. Use:
`https://JXDYJS.github.io/WITCHMODMCP`

After the first run you can ignore the prompt by hard-coding the URL inside the script
(see the `Read-Host` line).

## Wiring up a client (the mod on your machine)

In `WitchModMCP/ModConfig.json`:

```json
{
  "ToolsetVersion": "0.0.0",
  "UpdateManifestUrl": "https://JXDYJS.github.io/WITCHMODMCP/manifest.json",
  "CheckUpdateOnStartup": false
}
```

- `ToolsetVersion` is bumped automatically after each successful update.
- `UpdateManifestUrl` is the only setting you ever need to touch.
- `CheckUpdateOnStartup: true` runs an update check ~2 seconds after the game starts.
- Leave it `false` and trigger updates manually with `update_tools({})` if you'd rather.

## Manual use

The `update_tools` MCP tool is always available:

```
update_tools({})              # update only if remote version differs
update_tools({"force": true}) # re-download + verify even if versions match
```

Returns JSON like:

```json
{ "status": "updated",      "localVersion": "1.0.0", "remoteVersion": "1.0.1", "toolCount": 52 }
{ "status": "up_to_date",   "localVersion": "1.0.1", "remoteVersion": "1.0.1" }
{ "status": "skipped",      "code": "manifest_fetch_failed", "reason": "..." }
```

All `skipped` responses are non-fatal — the running tools are untouched.

## Local end-to-end test (before any network round trip)

You can point the manifest URL at a local HTTP server to exercise the full pipeline
without GitHub Pages:

1. Build the toolset: `dotnet build -p:BuildPart=Tools`
2. Compute SHA256 + size of `WitchModMCP/Scripts/WitchModMCP.Contracts.dll`.
3. Drop the DLL into a folder, e.g. `C:/tmp/mcp-update/`, then write a `manifest.json`
   next to it with the values computed above.
4. `cd C:/tmp/mcp-update/ && python -m http.server 8000`
5. Set `UpdateManifestUrl` to `http://localhost:8000/manifest.json`,
   `CheckUpdateOnStartup: true`, and `ToolsetVersion: "0.0.0"`.
6. Start the game — `update_tools` will read the local server, download, verify, replace, and reload.

## Rolling back

Inside the Pages repo:

```bash
git revert HEAD       # undo the last release
git push
```

Next game launch auto-pulls the previous version.

## Failure modes (all non-fatal)

| code | meaning |
|------|---------|
| `no_manifest_url` | `UpdateManifestUrl` is empty |
| `config_unreadable` | `ModConfig.json` could not be parsed |
| `manifest_fetch_failed` | network/HTTP error |
| `manifest_parse_failed` | manifest is not valid JSON |
| `manifest_invalid` | missing `tools.url` or `tools.sha256` |
| `download_failed` | DLL download failed or timed out |
| `size_mismatch` | downloaded size does not match `size` in manifest |
| `hash_mismatch` | SHA256 does not match manifest |
| `replace_failed` | `File.Replace` / `File.Move` threw (file in use, AV scanner, etc.) |

In every case the on-disk DLL and running tools are unchanged.
