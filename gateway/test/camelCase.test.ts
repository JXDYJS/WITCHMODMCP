import { describe, it, expect } from "vitest";
import { toCamel, toCamelKey, lowerFirstKey } from "../src/tools/camelCase.js";

describe("toCamelKey", () => {
  it("converts snake_case to camelCase", () => {
    expect(toCamelKey("max_depth")).toBe("maxDepth");
    expect(toCamelKey("root_name")).toBe("rootName");
  });
  it("passes through keys without underscore", () => {
    expect(toCamelKey("targetIndex")).toBe("targetIndex");
    expect(toCamelKey("cardId")).toBe("cardId");
  });
});

describe("toCamel (recursive)", () => {
  it("converts nested object keys and arrays", () => {
    const input = { max_depth: 1, nested: { root_name: 2 }, list: [{ item_id: 3 }] };
    expect(toCamel(input)).toEqual({
      maxDepth: 1,
      nested: { rootName: 2 },
      list: [{ itemId: 3 }],
    });
  });
});

describe("lowerFirstKey", () => {
  it("lowercases first letter recursively (PascalCase -> camelCase)", () => {
    const input = { JsonRpc: "2.0", ToolCount: { FooBar: 1 }, List: [{ AbCd: 2 }] };
    expect(lowerFirstKey(input)).toEqual({
      jsonRpc: "2.0",
      toolCount: { fooBar: 1 },
      list: [{ abCd: 2 }],
    });
  });
});
