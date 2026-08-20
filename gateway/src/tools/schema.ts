/**
 * schema.ts — convert a C# JSON-Schema-style inputSchema into a Zod schema
 * for the MCP SDK.
 *
 * Mirrors `mcp_gateway/tools.py::_build_signature_from_schema`:
 *   - required properties → required zod field
 *   - optional properties → optional field (defaults to undefined, or the JSON
 *     Schema "default" when present)
 *   - JSON Schema "type" mapped to the closest zod type.
 *
 * The resulting shape is passed to `registerTool(..., { inputSchema })`, so the
 * MCP `tools/list` response exposes the C# tool's native parameter schema and
 * `tools/call` receives type-checked arguments.
 */
import { z, type ZodType } from "zod";

export interface CSharpProperty {
  type?: string;
  default?: unknown;
  description?: string;
  [key: string]: unknown;
}

export interface CSharpToolSchema {
  type?: string;
  properties?: Record<string, CSharpProperty>;
  required?: string[];
  [key: string]: unknown;
}

function fromJsonType(jsonType: string | undefined): ZodType {
  switch (jsonType) {
    case "string":
      return z.string();
    case "integer":
      return z.number().int();
    case "number":
      return z.number();
    case "boolean":
      return z.boolean();
    case "array":
      return z.array(z.any());
    case "object":
      return z.record(z.any());
    default:
      return z.any();
  }
}

/**
 * Build a Zod object schema from a C# tool's `inputSchema`.
 */
export function buildZodSchema(schema: CSharpToolSchema): z.ZodObject<Record<string, ZodType>> {
  const shape: Record<string, ZodType> = {};
  const props = schema.properties ?? {};
  const required = new Set(schema.required ?? []);

  for (const [name, propRaw] of Object.entries(props) as [string, CSharpProperty][]) {
    const base = fromJsonType(propRaw.type).describe(propRaw.description ?? "");
    if (required.has(name)) {
      shape[name] = base;
    } else {
      shape[name] = propRaw.default !== undefined ? base.default(propRaw.default) : base.optional();
    }
  }
  return z.object(shape);
}
