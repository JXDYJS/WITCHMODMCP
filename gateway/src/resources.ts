/**
 * resources.ts — expose witchSkill docs as MCP resources.
 *
 * Port of `mcp_gateway/resources.py`. Auto-discovers all `.md` files under
 * `.agents/skills/witchSkill/` in the workspace and registers each as an MCP
 * resource (resource://witchskill/{relative_path}). Silently skipped when the
 * skill directory is absent.
 */
import { readdirSync, existsSync, readFileSync } from "node:fs";
import { join, sep } from "node:path";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";

export interface RegisterResourcesOptions {
  /** Workspace root that contains `.agents/skills/witchSkill`. */
  workspaceDir: string;
  /** SDK McpServer instance. */
  server: McpServer;
}

function listMdFiles(root: string): string[] {
  const out: string[] = [];
  const entries = readdirSync(root, { withFileTypes: true });
  for (const e of entries) {
    const full = join(root, e.name);
    if (e.isDirectory()) {
      out.push(...listMdFiles(full));
    } else if (e.isFile() && e.name.endsWith(".md")) {
      out.push(full);
    }
  }
  return out;
}

function makeName(rel: string): string {
  const parts = rel.split(sep);
  const stem = parts.at(-1)?.replace(/\.md$/, "") ?? "";
  const parents = parts.slice(0, -1);
  const title = (s: string) => s.replace(/[-_]/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());

  if (stem.toLowerCase() === "skill") {
    if (parents.length === 0) return "WitchModMCP — Root Index";
    return parents.map(title).join(" — ");
  }
  const prefix = parents.map(title).join(" — ");
  return prefix ? `${prefix} — ${title(stem)}` : title(stem);
}

export function registerResources(opts: RegisterResourcesOptions): number {
  const skillRoot = join(opts.workspaceDir, ".agents", "skills", "witchSkill");
  if (!existsSync(skillRoot)) return 0;

  const files = listMdFiles(skillRoot).sort();
  let count = 0;
  for (const file of files) {
    const rel = file.slice(skillRoot.length).replace(/^[\\/]/, "") || "SKILL.md";
    const uri = `resource://witchskill/${rel.split(sep).join("/")}`;
    const name = makeName(rel);
    opts.server.registerResource(
      name,
      uri,
      { mimeType: "text/markdown", description: "" },
      () => {
        let text: string;
        try {
          text = readFileSync(file, "utf-8");
        } catch {
          text = `# Resource Unavailable\n\nFailed to read \`${file}\`.`;
        }
        return { contents: [{ uri, text }] };
      },
    );
    count += 1;
  }
  return count;
}
