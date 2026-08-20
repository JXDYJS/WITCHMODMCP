import { describe, it, expect } from "vitest";
import { buildZodSchema, type CSharpToolSchema } from "../src/tools/schema.js";

describe("buildZodSchema", () => {
  it("maps required/optional and types", () => {
    const schema: CSharpToolSchema = {
      type: "object",
      properties: {
        targetIndex: { type: "integer", description: "target index" },
        cards: { type: "array" },
        flag: { type: "boolean", default: false },
      },
      required: ["targetIndex"],
    };
    const zod = buildZodSchema(schema);
    const ok = zod.safeParse({ targetIndex: 2, cards: ["a"], flag: true });
    expect(ok.success).toBe(true);
    // required field is enforced
    expect(zod.safeParse({}).success).toBe(false);
    // optional honoured
    expect(zod.safeParse({ targetIndex: 1 }).success).toBe(true);
  });

  it("empty schema -> empty object (zero-arg tool)", () => {
    const zod = buildZodSchema({ type: "object" });
    expect(zod.safeParse({}).success).toBe(true);
  });
});
