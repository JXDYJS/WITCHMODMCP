import { describe, it, expect } from "vitest";
import { HeartbeatManager } from "../src/heartbeat.js";

const delay = (ms: number) => new Promise((r) => setTimeout(r, ms));

describe("HeartbeatManager", () => {
  it("fires onFirstHeartbeat once on first success, then normal ticks", async () => {
    const first: unknown[] = [];
    const normal: unknown[] = [];
    const beats: Array<[boolean, Record<string, unknown>]> = [
      [true, { sessionId: "s1" }],
      [true, { sessionId: "s1" }],
      [true, { sessionId: "s1" }],
    ];
    let sendCount = 0;
    const sendHeartbeat = async () => {
      sendCount += 1;
      return beats[Math.min(sendCount - 1, beats.length - 1)];
    };

    const hb = new HeartbeatManager({
      sendHeartbeat,
      workspaceDir: "/tmp",
      interval: 0.05,
      maxFailures: 2,
      onFirstHeartbeat: (r) => first.push(r),
      onHeartbeat: (r) => normal.push(r),
    });
    hb.start();
    await delay(300);
    hb.stop();

    expect(first.length).toBe(1);
    expect(sendCount).toBeGreaterThanOrEqual(3);
  });

  it("drops to disconnected after maxFailures consecutive failures", async () => {
    const beats: Array<[boolean, Record<string, unknown>]> = [
      [true, { status: "ok" }],
      [false, { error: "down" }],
      [false, { error: "down" }],
    ];
    let i = 0;
    const sendHeartbeat = async (): Promise<[boolean, Record<string, unknown>]> => {
      const b = beats[Math.min(i, beats.length - 1)];
      i += 1;
      return b;
    };

    const hb = new HeartbeatManager({
      sendHeartbeat,
      workspaceDir: "/tmp",
      interval: 0.05,
      maxFailures: 2,
    });
    hb.start();
    await delay(400);
    hb.stop();

    expect(hb.firstHeartbeatDone).toBe(true);
    expect(hb.connected).toBe(false);
  });
});
