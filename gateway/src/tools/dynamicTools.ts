/**
 * dynamicTools.ts — dynamic registration of C# mod tools onto the MCP server.
 *
 * Port of `mcp_gateway/tools.py` (the dynamic-discovery half). The game mod
 * exposes its real tool set via a JSON-RPC `list_tools` call; each returned
 * tool has a native `inputSchema` which we re-expose through the SDK
 * `registerTool` so the MCP `tools/list` payload matches the C# tool and
 * arguments are type-checked. Handlers simply forward the call to the mod.
 */
import type { McpServer, RegisteredTool } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ModConnection } from "../modClient.js";
import { toCamel } from "./camelCase.js";
import { buildZodSchema, type CSharpToolSchema } from "./schema.js";

export interface CSharpToolInfo {
  name: string;
  description?: string;
  inputSchema?: CSharpToolSchema;
}

// Tools that survive `unregisterDynamicTools` (always-available core set).
const CORE_TOOL_NAMES = new Set(["ping", "reload_tools", "deploy_mod"]);

export class DynamicToolRegistrar {
  private readonly server: McpServer;
  private readonly mod: ModConnection;
  /** Currently-registered non-core tool handles (to remove on refresh). */
  private registered = new Map<string, RegisteredTool>();

  constructor(server: McpServer, mod: ModConnection) {
    this.server = server;
    this.mod = mod;
  }

  /** Number of non-core tools currently registered. */
  get count(): number {
    return this.registered.size;
  }

  /**
   * Fetch the tool list from the C# mod and register each as an MCP tool.
   * Idempotent: already-registered names are skipped.
   * Returns the number of NEW tools registered.
   */
  async registerDynamicTools(): Promise<number> {
    let resp: import("../modClient.js").ModToolResponse;
    try {
      resp = await this.mod.callTool("list_tools", {});
    } catch (err) {
      console.error(`[tools] register_dynamic_tools: list_tools call failed: ${(err as Error).message}`);
      return 0;
    }
    if (resp.error) {
      console.error(`[tools] register_dynamic_tools: list_tools error: ${resp.error.message}`);
      return 0;
    }
    const result = resp.result as { tools?: CSharpToolInfo[] } | undefined;
    if (!result || !Array.isArray(result.tools) || result.tools.length === 0) {
      console.error("[tools] register_dynamic_tools: mod returned empty tool list");
      return 0;
    }
    return this.registerToolList(result.tools);
  }

  /** Register a pre-fetched tool list (used at startup and by reload_tools). */
  registerToolList(tools: CSharpToolInfo[]): number {
    let count = 0;
    for (const t of tools) {
      const name = t.name;
      if (!name) continue;
      if (this.registered.has(name) || CORE_TOOL_NAMES.has(name)) continue;
      const desc = t.description ?? "";
      const schema = t.inputSchema ?? { type: "object" };

      const handle = this.server.registerTool(
        name,
        { description: desc, inputSchema: buildZodSchema(schema) },
        (args) => this.forward(name, (args ?? {}) as Record<string, unknown>),
      );
      this.registered.set(name, handle);
      count += 1;
    }
    if (count) {
      console.error(`[tools] Registered ${count} C# tools (total: ${this.registered.size})`);
    }
    return count;
  }

  /** Forward a tool call to the game mod with a connection check. */
  async forward(toolName: string, arguments_: Record<string, unknown>) {
    return {
      content: [{ type: "text" as const, text: await this.forwardText(toolName, arguments_) }],
    };
  }

  /**
   * Shared forwarding used by core tools (reload_tools, deploy_mod) too.
   * Returns a JSON string suitable for MCP text content.
   */
  async forwardText(toolName: string, arguments_: Record<string, unknown>): Promise<string> {
    const camel = toCamel(arguments_) as Record<string, unknown> | undefined;
    let body: Record<string, unknown> | null = null;
    if (camel !== undefined) {
      body = {};
      for (const [k, v] of Object.entries(camel)) {
        if (v !== undefined && v !== null) body[k] = v;
      }
    }
    const resp = await this.mod.callTool(toolName, body);
    if (resp.error) {
      return JSON.stringify(resp.error);
    }
    return JSON.stringify(resp.result ?? resp, null, 2);
  }

  /** Remove all dynamically-registered tools, keeping the core set. */
  unregisterDynamicTools(): void {
    for (const [name, handle] of [...this.registered]) {
      if (CORE_TOOL_NAMES.has(name)) continue;
      try {
        handle.remove();
      } catch (err) {
        console.error(`[tools] failed to remove tool ${name}: ${(err as Error).message}`);
      }
      this.registered.delete(name);
    }
  }
}
