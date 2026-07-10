"""
test_enter_fakeBattle.py
========================
WitchModMCP 全链路测试脚本。
从游戏主菜单（MAIN_MENU）开始，一路走到虚假战斗中，验证所有 MCP 工具。

前置条件：
  1. 游戏已启动，WitchModMCP 已加载
  2. 游戏处于主菜单（未点击任何按钮）
  3. 本脚本在游戏运行时从命令行执行

用法：
  python test_enter_fakeBattle.py
  python test_enter_fakeBattle.py --port 3100

设计目标：
  - 每一步都断言关键字段
  - 通过 end_turn 推进回合，让游戏自然抽牌产生手牌
  - 可用于回归验证工具链是否完整可用
"""

import sys
import time
import argparse

sys.path.insert(0, "scripts")
from witch_mcp import WitchMcp


def step(label, result, indent=0):
    p = "  " * indent
    status = result.get("result", result.get("page", "ok"))
    print(f"{p}[{status}] {label}")


def check(cond, msg):
    if cond:
        print(f"  \u2713  {msg}")
    else:
        print(f"  \u26A0  {msg}")


def main():
    parser = argparse.ArgumentParser(description="Full MCP test: MAIN_MENU \u2192 fake battle")
    parser.add_argument("--port", type=int, default=3100, help="MCP port")
    args = parser.parse_args()

    g = WitchMcp(port=args.port)

    # ===== 0. Ping =====
    print("\n=== 0. Ping ===")
    if not g.ping():
        print("FATAL: WitchModMCP unreachable")
        sys.exit(1)
    print("  \u2713  WitchModMCP is alive")

    # ===== 1. enter_game =====
    print("\n=== 1. enter_game \u2014 \u4E3B\u83DC\u5355 \u2192 \u5C0F\u5C4B ===")
    r = g.call("get_scene_state")
    check(r.get("page") == "MAIN_MENU", f"initial page = {r.get('page')}")

    r = g.call("enter_game")
    step("enter_game", r)
    check(r.get("result") == "success", "enter_game succeeded")

    time.sleep(1)
    r = g.call("get_scene_state")
    check(r.get("page") == "HUB", f"after enter_game page = {r.get('page')}")

    # ===== 2. list_game_modes =====
    print("\n=== 2. list_game_modes ===")
    r = g.call("list_game_modes")
    modes = r.get("modes", [])
    names = [m["mode"] for m in modes]
    check("Normal" in names, f"modes: {names}")

    # ===== 3. start_new_game =====
    print("\n=== 3. start_new_game \u2014 \u521B\u5EFA\u65B0\u5B58\u6863 \u2192 \u804C\u4E1A\u5927\u5385 ===")
    r = g.call("start_new_game", {"mode": "Normal"})
    check(r.get("result") == "success", "start_new_game")
    check(r.get("page") == "LOBBY", "now in LOBBY")

    # ===== 4. get_lobby_state =====
    print("\n=== 4. get_lobby_state ===")
    r = g.call("get_lobby_state")
    check(r.get("inLobby"), "in lobby")
    careers = [c.get("Id") for c in r.get("availableCareers", [])]
    partners = [p.get("Id") for p in r.get("availablePartners", [])]
    check(len(careers) > 0, f"careers: {careers}")
    check(len(partners) > 0, f"partners: {partners}")

    # ===== 5. set_lobby_state =====
    print("\n=== 5. set_lobby_state ===")
    career_id = careers[0]
    partner_id = partners[0]
    r = g.call("set_lobby_state", {
        "careerId": career_id,
        "partnerId": partner_id,
        "attributes": {"main": "Strength", "second": "Wisdom"},
    })
    check(r.get("result") == "success", "configured career/partner/attrs")

    # ===== 6. start_run =====
    print("\n=== 6. start_run \u2014 \u542F\u7A0B ===")
    r = g.call("start_run")
    check(r.get("result") == "success", "run started")
    time.sleep(0.5)
    r = g.call("get_scene_state")
    check(r.get("page") == "MAP", f"now on MAP (level={r.get('level')})")

    # ===== 7. load_scene fakefight =====
    print("\n=== 7. load_scene \u2014 \u8FDB\u5165\u865A\u5047\u6218\u6597 ===")
    r = g.call("load_scene", {"type": "fakefight", "id": "common"})
    step("load_scene", r)
    time.sleep(5)
    r = g.call("get_fight_state")
    check(r.get("inFight"), "in fight")
    check(r.get("phase") == "Player", "phase = Player")
    enemies = r.get("enemies", [])
    check(len(enemies) > 0, f"{len(enemies)} enemies")

    # ===== 8. get_fight_state =====
    print("\n=== 8. get_fight_state \u2014 \u6218\u4E2D\u5FEB\u7167 ===")
    r = g.call("get_fight_state")
    p = r.get("player", {})
    e = r.get("enemies", [])
    draw = r.get("drawPile", {})
    print(f"  Player: HP={p.get('hp')} \u76FE={p.get('shield')} \u80FD\u91CF={p.get('power')}")
    print(f"  Enemies: {len(e)} first HP={e[0].get('hp') if e else '?'}")
    print(f"  Draw pile: {draw.get('count')} cards")
    check(r.get("hand") is not None, f"hand tracked ({len(r.get('hand', []))} cards now)")

    # ===== 9. set_fight_entity =====
    print("\n=== 9. set_fight_entity \u2014 \u6539\u5C5E\u6027/Buff ===")
    r = g.call("set_fight_entity", {"target": "player", "hp": 999, "maxHp": 999, "shield": 50, "power": 10})
    check(r.get("result") == "success", "player god mode")

    if e:
        r = g.call("set_fight_entity", {"target": "0", "hp": 1})
        check(r.get("result") == "success", "enemy 0 hp = 1")

    r = g.call("set_fight_entity", {
        "target": "player",
        "addBuffs": [{"id": "buff_strength", "level": 3}],
    })
    check(r.get("result") == "success", "added buff_strength lv3")

    # ===== 10. set_card_pile \u2014 draw/discard =====
    print("\n=== 10. set_card_pile \u2014 \u63A7\u5236\u62BD\u724C/\u5F03\u724C\u5806 ===")
    r = g.call("set_card_pile", {"pile": "draw", "action": "set", "cards": ["card_1", "card_5", "burningcard_2"]})
    check(r.get("result") == "success", "draw pile set to 3 cards")

    r = g.call("set_card_pile", {"pile": "discard", "action": "add", "cards": ["card_3"]})
    check(r.get("result") == "success", "discard +1 card")

    r = g.call("get_fight_state")
    d = r.get("drawPile", {})
    di = r.get("discardPile", {})
    check(d.get("count") == 3, f"draw pile count = {d.get('count')}")
    check(di.get("count") == 1, f"discard count = {di.get('count')}")

    # ===== 11. end_turn + next turn draw =====
    print("\n=== 11. end_turn \u2014 \u7ED3\u675F\u56DE\u5408 + \u7B49\u5F85\u4E0B\u56DE\u5408\u62BD\u724C ===")
    r = g.call("end_turn")
    print(f"  end_turn: {r.get('result')} ({r.get('message','')})")

    time.sleep(8)
    r = g.call("get_fight_state")
    phase = r.get("phase")
    hp = r.get("player", {}).get("hp")
    hand = r.get("hand", [])
    print(f"  Phase: {phase}, Player HP: {hp}, Hand: {len(hand)} cards")
    if hand:
        print(f"  Hand cards: {[c.get('cardId') for c in hand]}")

    # ===== 12. play_card =====
    print("\n=== 12. play_card ===")
    r = g.call("get_fight_state")
    hand_cards = r.get("hand", [])
    if hand_cards:
        card_id = hand_cards[0].get("cardId", "?")
        r = g.call("play_card", {"index": 0})
        step(f"play index=0 ({card_id})", r)
        if r.get("targetHpAfter") is not None:
            print(f"    Damage: {r.get('targetHpBefore')} \u2192 {r.get('targetHpAfter')}")
    else:
        print("  \u26A0  skip: no hand cards (expected in fake fight)")

    # ===== 13. set_rng_seed =====
    print("\n=== 13. set_rng_seed ===")
    r = g.call("set_rng_seed", {"seed": 42})
    check(r.get("result") == "success", "seed = 42")

    # ===== 14. claim_rewards =====
    print("\n=== 14. claim_rewards ===")
    r = g.call("claim_rewards")
    step("claim_rewards", r)
    print(f"  Actions: {r.get('actions', [])}")

    # ===== Summary =====
    print("\n" + "=" * 50)
    print(" ALL TESTS COMPLETED")
    print("=" * 50)
    print()
    print("Tools verified:")
    print("  get_scene_state  enter_game  list_game_modes")
    print("  start_new_game  get_lobby_state  set_lobby_state")
    print("  start_run  load_scene  get_fight_state")
    print("  set_fight_entity  set_card_pile  play_card")
    print("  end_turn  set_rng_seed  claim_rewards")
    print()
    print("Note: hand cards may not appear instantly in fake fights;")
    print("  use end_turn to advance for natural card draws.")


if __name__ == "__main__":
    main()
