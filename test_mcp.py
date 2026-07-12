"""
运行: python test_mcp.py [--port 3100] [--token <auth_token>]

  指定 --token 以通过工具调用的认证（默认从游戏 ModConfig.json 读取）。
  不指定则工具调用会报 401。
"""

import json
import sys
import os
import http.client
from datetime import datetime
from pathlib import Path

PORT = 3100
TOKEN = ""
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
OK = 0
FAIL = 0
LOG_LINES = []


def resolve_token() -> str:
    """Auto-discover the MCP auth token from common ModConfig.json paths."""
    # Command line takes precedence
    for i, a in enumerate(sys.argv[1:], 1):
        if a == "--token" and i < len(sys.argv):
            return sys.argv[i + 1]

    candidates = [
        os.environ.get("MCP_MOD_TOKEN", ""),
        r"F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey_Data\Mods\WitchModMCP\ModConfig.json",
        r"F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey_Data\Mods\WitchModMCP.DeveloperTools\ModConfig.json",
        str(Path(out_dir := os.path.dirname(os.path.abspath(__file__))) / "【MOD文件夹】" / "ModConfig.json"),
        str(Path(out_dir).parent / "【MOD文件夹】" / "ModConfig.json"),
    ]
    for c in candidates:
        if not c or c.startswith("F:"):
            continue
        p = Path(c)
        if p.exists():
            try:
                cfg = json.loads(p.read_text(encoding="utf-8"))
                t = cfg.get("MCPAuthToken", "")
                if t:
                    return t
            except (json.JSONDecodeError, OSError):
                pass
    return ""


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
    headers = {"Content-Type": "application/json"}
    if TOKEN:
        headers["Authorization"] = f"Bearer {TOKEN}"
    conn.request("POST", "/", json.dumps(body), headers)
    resp = conn.getresponse()
    raw = resp.read().decode()
    conn.close()
    data = json.loads(raw)
    return data, raw


def heartbeat(workspace_path="/test/workspace", pid=9999):
    body = json.dumps({"workspacePath": workspace_path, "pid": pid, "keepalive": True})
    conn = http.client.HTTPConnection("localhost", PORT, timeout=10)
    conn.request("POST", "/heartbeat", body, {"Content-Type": "application/json"})
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


def test_heartbeat(name, print_body=True):
    """Test the POST /heartbeat endpoint."""
    global OK, FAIL
    try:
        data, raw = heartbeat()
        if data.get("status") != "ok":
            log(f"  FAIL  {name}: {json.dumps(data, ensure_ascii=False)}")
            FAIL += 1
            return None

        if print_body:
            log(f"  OK    {name}")
            log(json.dumps(data, ensure_ascii=False, indent=2))
            log("")
        else:
            log(f"  OK    {name}")

        for field in ["sessionId", "isFirstHeartbeat", "toolCount", "activeModules"]:
            if field not in data:
                log(f"  WARN  {name}: expected field '{field}' not found")
        OK += 1
        return data
    except Exception as e:
        log(f"  FAIL  {name}: {e}")
        FAIL += 1
        return None


def main():
    global PORT, TOKEN
    args = sys.argv[1:]
    while args:
        if args[0] == "--port" and len(args) >= 2:
            PORT = int(args[1])
            args = args[2:]
        elif args[0] == "--token" and len(args) >= 2:
            TOKEN = args[1]
            args = args[2:]
        else:
            args = args[1:]

    if not TOKEN:
        TOKEN = resolve_token()

    log(f"=== WitchModMCP 测试 (localhost:{PORT})  {datetime.now():%H:%M:%S} ===\n")

    # 0. heartbeat
    log("[0] POST /heartbeat - 心跳端点")
    result = test_heartbeat("heartbeat (first)", print_body=False)
    if result:
        log(f"      sessionId: {result.get('sessionId', '?')}")
        log(f"      isFirstHeartbeat: {result.get('isFirstHeartbeat', '?')}")
        log(f"      toolCount: {result.get('toolCount', '?')}")
        modules = result.get("activeModules", [])
        log(f"      activeModules: {len(modules)}")
        for m in modules:
            log(f"        {m.get('assemblyName')}: skillPath={m.get('skillPath', '?')}")

    # 0b. Second heartbeat (should not be first)
    log("\n[0b] POST /heartbeat - 第二次心跳 (isFirstHeartbeat=False)")
    result2 = test_heartbeat("heartbeat (second)", print_body=False)
    if result2:
        is_first2 = result2.get("isFirstHeartbeat")
        if is_first2:
            log(f"  WARN  第二次心跳仍报告 isFirstHeartbeat=True (可能 mod 重启了)")
        else:
            log(f"      isFirstHeartbeat=False (符合预期)")

    # 1. list_tools
    log("\n[1] list_tools - 列出所有 MCP 工具")
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

    # 9. inspect - 反射读取 (列出 RoleTable 的静态/实例成员)
    log("\n[9] inspect - 列出 RoleTable 类型成员")
    result = test("inspect: RoleTable members", "inspect", {"typeName": "RoleTable"}, expect_field="members", print_body=False)
    if result and isinstance(result, dict):
        members = result.get("members", {})
        static_m = members.get("static", {})
        instance_m = members.get("instance", {})
        log(f"      找到类型: {result.get('foundType', '?')}")
        log(f"      静态成员: {len(static_m)} 个  (前5: {', '.join(list(static_m.keys())[:5])})")
        log(f"      实例成员: {len(instance_m)} 个  (前5: {', '.join(list(instance_m.keys())[:5])})")

    # 9b. inspect - 链式反射访问 RoleTable.Instance.San
    log("\n[9b] inspect - 链式访问 RoleTable.Instance.San")
    result = test("inspect: RoleTable.Instance.San", "inspect",
                  {"typeName": "RoleTable", "memberPath": "Instance.San"},
                  expect_field="value")
    if result and isinstance(result, dict):
        log(f"      成员类型: {result.get('memberType', '?')}")
        v = result.get("value")
        log(f"      值: {json.dumps(v, ensure_ascii=False)}")

    # 9c. inspect - 链式反射访问 (FightManager 状态)
    log("\n[9c] inspect - 链式访问 FightManager.Instance.fightType")
    result = test("inspect: FightManager.fightType", "inspect",
                  {"typeName": "FightManager", "memberPath": "Instance.fightType"},
                  expect_field="value")
    if result and isinstance(result, dict):
        log(f"      值: {json.dumps(result.get('value'), ensure_ascii=False)}")

    # 9d. inspect - 找不到类型
    log("\n[9d] inspect - 不存在的类型")
    test("inspect: nonexistent type", "inspect", {"typeName": "NonExistentType"})

    # 10. dump_mod_state - 列出已加载的 Mod
    log("\n[10] dump_mod_state - 已加载 Mod 列表")
    result = test("dump_mod_state", "dump_mod_state", expect_field="modCount", print_body=False)
    if result and isinstance(result, dict):
        log(f"      已加载 Mod 数: {result.get('modCount', 0)}")
        mods = result.get("mods", [])
        for m in mods:
            log(f"        {m.get('assemblyName')}  v{m.get('assemblyVersion')}")
        related = result.get("relatedAssemblies", [])
        if related:
            names = [a["name"] for a in related]
            log(f"       相关程序集: {', '.join(names)}")

    # 11. query_config - 列出可用配置表
    log("\n[11] query_config - 列出所有可用配置表")
    result = test("query_config: list tables", "query_config", {}, expect_field="availableTables", print_body=False)
    if result and isinstance(result, dict):
        tables = result.get("availableTables", [])
        log(f"      可用配置表: {len(tables)} 个")
        for t in tables[:12]:
            item_count = f" ({t.get('itemCount', '?')} 条)" if "itemCount" in t else ""
            log(f"        {t['name']}: {t.get('type', '?')}{item_count}")
        if len(tables) > 12:
            log(f"        ... 还有 {len(tables) - 12} 个表")
        if result.get("hint"):
            log(f"      提示: {result['hint']}")

    # 11b. query_config - 查询具体表预览
    result_tables = result
    if result_tables:
        tables = result_tables.get("availableTables", [])
        first_table = tables[0]["name"] if tables else None
    else:
        first_table = None

    if first_table:
        log(f"\n[11b] query_config - 预览表: {first_table}")
        result = test(f"query_config: {first_table}", "query_config",
                       {"tableName": first_table, "limit": 3},
                       expect_field="samples", print_body=False)
        if result and isinstance(result, dict):
            log(f"      表类型: {result.get('tableType', '?')}")
            log(f"      总条目: {result.get('totalCount', '?')}")
            samples = result.get("samples", [])
            for s in samples[:3]:
                keys = list(s.keys())[:6] if isinstance(s, dict) else []
                log(f"        条目: {', '.join(k for k in keys if k != '_type')}")
                if isinstance(s, dict) and "_type" in s:
                    log(f"          类型={s['_type']}")
    else:
        log("\n[11b] query_config - 跳过 (无可用表)")

    # 12. get_scene_tree - 场景层级树
    log("\n[12] get_scene_tree - 当前场景层级树")
    result = test("get_scene_tree", "get_scene_tree", {"maxDepth": 4, "maxChildren": 20}, expect_field="hierarchy", print_body=False)
    if result and isinstance(result, dict):
        log(f"      场景: {result.get('sceneName', '?')}")
        log(f"      根对象数: {result.get('rootCount', 0)}")
        hierarchy = result.get("hierarchy", [])
        for root in hierarchy[:5]:
            comps = root.get("components", [])
            comp_str = ", ".join(comps[:5]) if comps else ""
            if len(comps) > 5:
                comp_str += f" +{len(comps)-5}"
            log(f"        {root.get('name')}  [active={root.get('activeSelf')}]  tag={root.get('tag')}  comps=[{comp_str}]")
            children = root.get("children", [])
            for child in children[:5]:
                c_comps = child.get("components", [])
                c_comp_str = ", ".join(c_comps[:3]) if c_comps else ""
                log(f"          └─ {child.get('name')}  [active={child.get('activeSelf')}]  comps=[{c_comp_str}]")
        if root_count := result.get('rootCount', 0):
            if root_count > 5:
                log(f"        ... 还有 {root_count - 5} 个根对象")

    # 12b. get_scene_tree - 过滤根节点
    log("\n[12b] get_scene_tree - 按根节点名过滤")
    test("get_scene_tree: rootName filter", "get_scene_tree",
         {"maxDepth": 3, "maxChildren": 10, "includeComponents": False, "rootName": "Main Camera"},
         expect_field="hierarchy", print_body=False)

    # 13. get_screenshot - PNG 截图并保存
    log("\n[13] get_screenshot - 截图 (PNG)")
    result = test("get_screenshot: PNG", "get_screenshot", {"format": "png"}, print_body=False)
    if result and isinstance(result, dict) and result.get("base64"):
        import base64 as _b64
        img = _b64.b64decode(result["base64"])
        out = os.path.join(OUT_DIR, "screenshot_test.png")
        with open(out, "wb") as f:
            f.write(img)
        log(f"      尺寸: {result.get('width')}x{result.get('height')}  大小: {result.get('size')} bytes")
        log(f"      已保存: {out}")
    elif result:
        log(f"      返回结果但无 base64 字段: {list(result.keys())}")

    # 13b. get_screenshot - JPG 截图
    log("\n[13b] get_screenshot - 截图 (JPG quality=75)")
    result = test("get_screenshot: JPG", "get_screenshot", {"format": "jpg", "quality": 75}, print_body=False)
    if result and isinstance(result, dict) and result.get("base64"):
        import base64 as _b64
        img = _b64.b64decode(result["base64"])
        out = os.path.join(OUT_DIR, "screenshot_test.jpg")
        with open(out, "wb") as f:
            f.write(img)
        log(f"      尺寸: {result.get('width')}x{result.get('height')}  大小: {result.get('size')} bytes")
        log(f"      已保存: {out}")
    elif result:
        log(f"      返回结果但无 base64 字段: {list(result.keys())}")

    # 14. raycast_mouse - 默认参数（当前鼠标位置）
    log("\n[14] raycast_mouse - 当前鼠标位置射线检测")
    result = test("raycast_mouse: default", "raycast_mouse", {},
                  expect_field="hits", print_body=False)
    if result and isinstance(result, dict):
        sp = result.get("screenPosition", {})
        log(f"      屏幕坐标: ({sp.get('x')}, {sp.get('y')})")
        hits = result.get("hits", [])
        log(f"      命中数: {result.get('hitCount', 0)}")
        for h in hits[:5]:
            canvas = "Canvas" if h.get("isCanvas") else "NonCanvas"
            log(f"        [{h.get('source','?')}/{canvas}] {h.get('hierarchyPath','?')}")
            log(f"          components: {h.get('components','?')}")

    # 14b. raycast_mouse - 自定义坐标 (顶栏按钮区域)
    log("\n[14b] raycast_mouse - 自定义坐标 (1700, 1020) 顶栏按钮区")
    result = test("raycast_mouse: custom coords", "raycast_mouse",
                  {"screenX": 1700, "screenY": 1020},
                  expect_field="hits", print_body=False)
    if result and isinstance(result, dict):
        sp = result.get("screenPosition", {})
        log(f"      屏幕坐标: ({sp.get('x')}, {sp.get('y')})")
        hits = result.get("hits", [])
        log(f"      命中数: {result.get('hitCount', 0)}")
        for h in hits[:5]:
            canvas = "Canvas" if h.get("isCanvas") else "NonCanvas"
            log(f"        [{h.get('source','?')}/{canvas}] {h.get('hierarchyPath','?')}")
            log(f"          distance={h.get('distance','?')}  depth={h.get('depth','?')}  sortingOrder={h.get('sortingOrder','?')}")
        if len(hits) > 0:
            h0 = hits[0]
            assert_keys = ["gameObjectName", "hierarchyPath", "source", "isCanvas", "components"]
            missing = [k for k in assert_keys if k not in h0]
            if missing:
                log(f"      WARN  命中结果缺少字段: {missing}")
            else:
                log(f"      所有必填字段均存在: {', '.join(assert_keys)}")

    # 14c. raycast_mouse - maxResults 参数限制
    log("\n[14c] raycast_mouse - maxResults=1 限制返回数")
    result = test("raycast_mouse: maxResults=1", "raycast_mouse",
                  {"screenX": 1700, "screenY": 1020, "maxResults": 1},
                  expect_field="hits", print_body=False)
    if result and isinstance(result, dict):
        hits = result.get("hits", [])
        log(f"      命中数: {result.get('hitCount', 0)} (maxResults=1)")
        for h in hits:
            log(f"        [{h.get('source','?')}] {h.get('hierarchyPath','?')}")
        if result.get("hitCount", 0) > 1:
            log(f"      WARN  hitCount > maxResults, 未正确限制")

    # 14d. raycast_mouse - 空区域 (中心空白处)
    log("\n[14d] raycast_mouse - 空区域 (960, 540)")
    result = test("raycast_mouse: empty area", "raycast_mouse",
                  {"screenX": 960, "screenY": 540},
                  expect_field="hits", print_body=False)
    if result and isinstance(result, dict):
        log(f"      命中数: {result.get('hitCount', 0)}")
        if result.get("hitCount", 0) == 0:
            log(f"      空区域无命中，符合预期")

    log(f"\n=== 测试完毕: {OK} OK, {FAIL} FAIL ===")
    save_log()
    return FAIL == 0


if __name__ == "__main__":
    sys.exit(0 if main() else 1)
