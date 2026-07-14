#!/usr/bin/env python3
"""
Mod 自动化测试脚本 — 模板文件。

每次写完成 Mod 后，AI 应复制此脚本并填入实际数值。
玩家重启游戏后执行：python tests/test_<ModName>.py

依赖：pip install mcp
运行要求：游戏已启动，WitchModMCP 已加载
"""

import json
import os
import sys
import time
from pathlib import Path


# ═══════════════════════════════════════════════════════════════
# 配置区 — AI 每次写入实际 Mod 信息
# ═══════════════════════════════════════════════════════════════

MOD_NAME = "YourModName"

# 卡牌配置（query_config 验证用）
CARD_IDS = [1001, 1002]          # 本 Mod 添加的卡牌 ID
BUFF_IDS = [1001]                 # 本 Mod 添加的 Buff ID
PACK_ID = "pack_yourpack"         # 所属卡包 ID
CAREER_ID = "Career_1001"         # 职业 ID（如果是职业 Mod）

# 期待的战斗数值
EXPECTED_HAND_SIZE = 1            # 出牌后手牌数量
EXPECTED_ENERGY_COST = 1          # 卡牌费用
EXPECTED_DAMAGE = 6               # 卡牌伤害
EXPECTED_BUFF_ID = "buff_bleeding" # 期待添加的 Buff
EXPECTED_BUFF_STACKS = 3          # Buff 层数

# 连接配置
MCP_PORT = 3100
RUN_CONFIG = {
    "mode": "Standard",           # 游戏模式
    "career": "Witch",            # 测试用职业
}


# ═══════════════════════════════════════════════════════════════
# MCP 连接器
# ═══════════════════════════════════════════════════════════════

class ModClient:
    """通过 MCP 协议连接游戏 Mod。"""

    def __init__(self, port=3100):
        self.port = port
        self._conn = None

    def _ensure_conn(self):
        import http.client
        if self._conn is None:
            self._conn = http.client.HTTPConnection("127.0.0.1", self.port, timeout=10)
        return self._conn

    def call(self, method, params=None):
        body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": method, "params": params or {}})
        try:
            conn = self._ensure_conn()
            conn.request("POST", "/", body, {"Content-Type": "application/json"})
            resp = conn.getresponse()
            data = json.loads(resp.read().decode("utf-8"))
            err = data.get("error")
            if err:
                return {"error": err}
            return data.get("result", data)
        except Exception as e:
            return {"error": str(e)}

    def ping(self):
        try:
            conn = self._ensure_conn()
            conn.request("GET", "/ping")
            resp = conn.getresponse()
            return {"status": "ok"} if resp.status == 200 else {"status": "error"}
        except Exception as e:
            return {"status": "error", "message": str(e)}


# ═══════════════════════════════════════════════════════════════
# 测试框架
# ═══════════════════════════════════════════════════════════════

PASS = 0
FAIL = 0
LOGS = []


def log(msg):
    print(f"  {msg}")
    LOGS.append(msg)


def check(step, ok, detail=""):
    global PASS, FAIL
    if ok:
        log(f"  ✅ {step}")
        PASS += 1
    else:
        log(f"  ❌ {step}: {detail}")
        FAIL += 1


# ═══════════════════════════════════════════════════════════════
# 测试用例
# ═══════════════════════════════════════════════════════════════

def test_phase_1_connection(g):
    """验证 MCP 网关和游戏 Mod 已连接。"""
    pong = g.ping()
    check("MCP 网关可访问", pong.get("status") == "ok", str(pong))

    scene = g.call("get_scene_state")
    check("get_scene_state 可调用", scene is not None and "page" in scene)


def test_phase_2_mod_loaded(g):
    """验证 Mod 是否被游戏加载。"""
    state = g.call("dump_mod_state")
    check("dump_mod_state 可调用", state is not None)

    mods = state.get("mods", [])
    found = any(MOD_NAME.lower() in m.get("assemblyName", "").lower() for m in mods)
    check(f"Mod {MOD_NAME} 已加载", found,
          f"已加载 Mod: {[m['assemblyName'] for m in mods]}")


def test_phase_3_no_errors(g):
    """确认游戏日志中没有 Mod 加载错误。"""
    logs = g.call("get_recent_logs", {"count": 50})
    if not logs or isinstance(logs, dict) and "error" in logs:
        log("  ⚠️ 无法获取日志，跳过检查")
        return

    errors = [l for l in (logs if isinstance(logs, list) else [])
              if isinstance(l, str) and "Error" in l]
    check("无加载错误", len(errors) == 0,
          f"发现 {len(errors)} 条错误（前 3 条: {errors[:3]}）")


def test_phase_4_config_registered(g):
    """验证配置表中已注册本 Mod 的内容。"""
    for cid in CARD_IDS:
        cfg = g.call("query_config", {"tableName": "CardConfig", "id": cid})
        exists = cfg is not None and isinstance(cfg, dict) and "item" in cfg
        check(f"卡牌 {cid} 已注册", exists, str(cfg))

    for bid in BUFF_IDS:
        cfg = g.call("query_config", {"tableName": "BuffConfig", "id": bid})
        exists = cfg is not None and isinstance(cfg, dict) and "item" in cfg
        check(f"Buff {bid} 已注册", exists, str(cfg))


def test_phase_5_fakefight(g):
    """在假战斗中验证卡牌效果。"""

    # 如果不在游戏内，尝试进入
    scene = g.call("get_scene_state")
    page = scene.get("page", "") if scene else ""

    if page == "MAIN_MENU":
        log("  从主菜单进入游戏...")
        g.call("enter_game")
        time.sleep(2)
        g.call("start_new_game", {"mode": RUN_CONFIG["mode"]})
        time.sleep(1)

    if page in ("HUB", "LOBBY"):
        log("  设置大厅并启程...")
        g.call("set_lobby_state", {
            "career": RUN_CONFIG["career"],
            "confirm": True,
        })
        g.call("start_run")
        time.sleep(2)

    # 跳入假战斗
    log("  跳入假战斗...")
    result = g.call("load_scene", {"type": "fakefight"})
    check("跳入假战斗成功",
          result is not None and isinstance(result, dict) and "type" in result)

    # 等待战斗初始化
    time.sleep(1)

    fight = g.call("get_fight_state")
    check("get_fight_state 返回战斗状态", fight is not None and "inFight" in fight)
    if fight and fight.get("inFight"):
        log(f"  战斗阶段: {fight.get('phase')}")
        log(f"  手牌数: {len(fight.get('hand', []))}")
        log(f"  能量: {fight.get('player', {}).get('power')}")

    # 注入测试卡牌
    for cid in CARD_IDS:
        result = g.call("give_item", {"item_type": "card", "value": str(cid)})
        check(f"注入卡牌 {cid} 成功",
              result is not None and "error" not in str(result))

    # 重新读取战斗状态
    time.sleep(0.5)
    fight = g.call("get_fight_state")
    hand = fight.get("hand", []) if fight else []
    check(f"手牌中有测试卡牌（目标 {EXPECTED_HAND_SIZE} 张以上）",
          len(hand) >= EXPECTED_HAND_SIZE,
          f"手牌数: {len(hand)}")

    # 出牌测试
    if hand:
        test_card = hand[0]
        card_index = test_card.get("index", 0)
        log(f"  准备出牌: index={card_index}, cost={test_card.get('cost')}")
        check("卡牌费用正确", test_card.get("cost") == EXPECTED_ENERGY_COST,
              f"费用: {test_card.get('cost')}, 期望: {EXPECTED_ENERGY_COST}")

        result = g.call("play_card", {"card_index": card_index, "target_index": 0})
        played_ok = result is not None and "error" not in str(result)
        check("出牌成功", played_ok, str(result))

        if played_ok:
            # 验证战斗状态
            time.sleep(0.5)
            fight = g.call("get_fight_state")
            if fight and fight.get("inFight"):
                enemies = fight.get("enemies", [])
                enemy = enemies[0] if enemies else {}

                # 验证伤害
                hp_before = test_card.get("targetHpBefore")
                hp_after = result.get("targetHpAfter")
                if hp_before is not None and hp_after is not None:
                    damage_dealt = hp_before - hp_after
                    check(f"伤害数值正确（目标 {EXPECTED_DAMAGE}）",
                          damage_dealt == EXPECTED_DAMAGE,
                          f"实际: {damage_dealt}")

                # 验证 Buff
                enemy_buffs = enemy.get("buffs", [])
                has_buff = any(
                    b.get("id") == EXPECTED_BUFF_ID
                    and b.get("stack", 0) >= EXPECTED_BUFF_STACKS
                    for b in enemy_buffs
                )
                check(f"Buff {EXPECTED_BUFF_ID} 已添加 {EXPECTED_BUFF_STACKS} 层",
                      has_buff,
                      f"敌人 Buff: {enemy_buffs}")

                log(f"  玩家 HP: {fight.get('player', {}).get('hp')}"
                    f" 盾: {fight.get('player', {}).get('shield')}")
                log(f"  回合: {fight.get('phase')}")

    # 结束回合
    result = g.call("end_turn")
    check("结束回合", result is not None and "error" not in str(result))
    time.sleep(0.5)


# ═══════════════════════════════════════════════════════════════
# 主流程
# ═══════════════════════════════════════════════════════════════

def main():
    global PASS, FAIL
    print(f"\n{'=' * 56}")
    print(f"  {MOD_NAME} — Mod 自动化测试")
    print(f"{'=' * 56}\n")

    g = ModClient(MCP_PORT)

    pong = g.ping()
    if pong.get("status") != "ok":
        print("  ❌ 游戏 Mod 不可达。请确认：")
        print("     1. 游戏已启动")
        print("     2. WitchModMCP Mod 已加载")
        print(f"     3. 端口 {MCP_PORT} 正确")
        sys.exit(1)
    print(f"  ✅ MCP 网关连接成功 (port {MCP_PORT})\n")

    # 执行测试
    test_phase_1_connection(g)
    test_phase_2_mod_loaded(g)
    test_phase_3_no_errors(g)
    test_phase_4_config_registered(g)
    test_phase_5_fakefight(g)

    # 结果汇总
    print(f"\n{'=' * 56}")
    print(f"  结果: {PASS} 通过, {FAIL} 失败")
    if FAIL > 0:
        print(f"  ⚠️ 有 {FAIL} 项未通过，请检查上方 ❌ 标记的步骤")
    else:
        print(f"  🎉 全部通过！")
    print(f"{'=' * 56}\n")

    return 1 if FAIL > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
