/**
 * camelCase.ts — key case conversion helpers.
 *
 * Mirrors the behaviour of the Python gateway (`mcp_gateway/tools.py::_to_camel`
 * and `mcp_gateway/mod_client.py::_lower_keys`).
 *
 * - `toCamel`      : snake_case → camelCase, recursively. Used for parameters
 *                    sent to the C# mod (Newtonsoft expects camelCase names).
 * - `lowerFirstKey`: PascalCase → camelCase on the first letter only,
 *                    recursively. Used to normalise C# response keys.
 */

export function toCamel(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((v) => toCamel(v));
  }
  if (value !== null && typeof value === "object") {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value)) {
      out[toCamelKey(k)] = toCamel(v);
    }
    return out;
  }
  return value;
}

/** snake_case → camelCase. Keys without '_' are passed through unchanged. */
export function toCamelKey(key: string): string {
  if (!key.includes("_")) {
    return key;
  }
  const parts = key.split("_");
  return (
    parts[0].toLowerCase() +
    parts
      .slice(1)
      .filter((p) => p !== "")
      .map((p) => p[0].toUpperCase() + p.slice(1))
      .join("")
  );
}

/** PascalCase → camelCase (lowercase first character), recursively. */
export function lowerFirstKey(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((v) => lowerFirstKey(v));
  }
  if (value !== null && typeof value === "object") {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value)) {
      const key = k && /^[A-Z]/.test(k) ? k[0].toLowerCase() + k.slice(1) : k;
      out[key] = lowerFirstKey(v);
    }
    return out;
  }
  return value;
}
