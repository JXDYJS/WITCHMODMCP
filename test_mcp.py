"""
运行: python test_mcp.py [--port 3100]
"""

import json
import sys
import http.client
from datetime import datetime

PORT = 3100
OK = 0
FAIL = 0
LOG_LINES = []


def log(s):
    print(s)
    LOG_LINES.append(s)


def save_log():
    with open("test_log.txt", "w", encoding="utf-8") as f:
        f.write("\n".join(LOG_LINES))
    print(f"\n日志已保存到 test_log.txt ({len(LOG_LINES)} 行)")


def rpc(method, params=None, rid=1):
    body = {"jsonrpc": "2.0", "id": rid, "method": method}
    if params is not None:
        body["params"] = params
    conn = http.client.HTTPConnection("localhost", PORT, timeout=10)
    conn.request("POST", "/", json.dumps(body), {"Content-Type": "application/json"})
    resp = conn.getresponse()
    raw = resp.read().decode()
    conn.close()
    data = json.loads(raw)
    return data, raw


# Newtonsoft 序列化用的是 PascalCase (Result, Error, Id, JsonRpc)
def get_result(data):
    return data.get("Result") or data.get("result")


def get_error(data):
    return data.get("Error") or data.get("error")


def test(name, method, params=None, expect_field=None, print_body=True):
    global OK, FAIL
    try:
        data, raw = rpc(method, params)
        err = get_error(data)
        if err:
            log(f"  FAIL  {name}: {json.dumps(err, ensure_ascii=False)}")
            FAIL += 1
            return None

        result = get_result(data)
        if print_body:
            log(f"  OK    {name}")
            log(json.dumps(result, ensure_ascii=False, indent=2))
            log("")
        else:
            log(f"  OK    {name}")

        if expect_field and isinstance(result, dict) and expect_field not in result:
            log(f"  WARN  {name}: expected field '{expect_field}' not found")
        OK += 1
        return result
    except Exception as e:
        log(f"  FAIL  {name}: {e}")
        FAIL += 1
        return None


def main():
    global PORT
    if len(sys.argv) > 1 and sys.argv[1] == "--port":
        PORT = int(sys.argv[2])

    log(f"=== WitchModMCP 测试 (localhost:{PORT})  {datetime.now():%H:%M:%S} ===\n")

    # 1. list_tools
    log("[1] list_tools - 列出所有 MCP 工具")
    result = test("list_tools", "list_tools", expect_field="tools")
    if result:
        tools = result.get("tools", [])
        if tools:
            names = [t["name"] for t in tools]
            log(f"      已注册 {len(tools)} 个工具: {', '.join(names)}")

    # 2. list_commands
    log("\n[2] list_commands - 列出所有游戏命令")
    result = test("list_commands", "list_commands", expect_field="commands", print_body=False)
    if result:
        cmds = result.get("commands", [])
        log(f"      共 {len(cmds)} 条命令")
        for c in cmds[:10]:
            params = ", ".join(p["name"] for p in c.get("parameters", []))
            desc = c.get("description", "")
            log(f"        /{c['name']} {params}")
            if desc:
                log(f"          {desc}")
        if len(cmds) > 10:
            log(f"        ... 还有 {len(cmds) - 10} 条")

    # 3. eval_command - help
    log("\n[3] eval_command - 执行 help give")
    test("eval_command: help give", "eval_command", {"command": "help give"})

    # 4. eval_command - cls
    log("\n[4] eval_command - 执行 cls")
    test("eval_command: eval_command: cls", "eval_command", {"command": "cls"})

    # 5. get_game_data
    log("\n[5] get_game_data - 游戏状态快照")
    result = test("get_game_data", "get_game_data", expect_field="player")
    if result and isinstance(result, dict):
        p = result.get("player", {})
        f = result.get("fight", {})
        r = result.get("runtime", {})
        if p:
            log(f"      玩家: HP={p.get('hp')}/{p.get('maxHp')}  SAN={p.get('san')}/{p.get('maxSan')}  金钱={p.get('money')}")
            log(f"      背包: 牌{p.get('cardCount')}张  遗物{p.get('relicCount')}个  祝福{p.get('blessCount')}个")
        else:
            log(f"      玩家数据为空 (可能未加载存档)")
        if f:
            log(f"      战斗: {'战斗中' if f.get('inFight') else '未战斗'}  能量={f.get('playerPower')}  护盾={f.get('playerShield')}  类型={f.get('fightType')}")
        if r:
            log(f"      运行: 层数={r.get('level')}  时间流={r.get('time')}  真理={r.get('truth')}  经验={r.get('exp')}")

    # 6. give_item
    log("\n[6] give_item - 给 100 金币")
    test("give_item: money 100", "give_item", {"type": "money", "value": "100"})

    # 7. load_scene 缺参数校验
    log("\n[7] load_scene - 参数校验 (不传 type)")
    test("load_scene: missing type", "load_scene", {})

    # 8. eval_command - set enemy debug
    log("\n[8] eval_command - 设置敌人倍率 (测试用, 立即 reset)")
    test("enemyall hp 0.5", "eval_command", {"command": "enemyall hp 0.5"})
    test("debugreset", "eval_command", {"command": "debugreset"})

    log(f"\n=== 测试完毕: {OK} OK, {FAIL} FAIL ===")
    save_log()
    return FAIL == 0


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
