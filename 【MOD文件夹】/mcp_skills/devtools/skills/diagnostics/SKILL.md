---
name: witch-mod-mcp-developertools-diagnostics
description: "DeveloperTools diagnostics tools: screenshot capture, mouse raycasting, RNG seed control, and source code decompilation. Use when the user needs to visually verify game state, identify GameObjects under the mouse, control randomness for reproducible testing, or decompile game DLLs for source inspection. Triggers: diagnostics, debug, get_screenshot, raycast_mouse, set_rng_seed, decompile_source, 截图, 射线检测, 随机种子, 反编译."
---

# Diagnostics 模块 — 开发者诊断

专为 Mod 开发者设计的高级诊断和调试工具。与基座 WitchModMCP 的诊断工具（inspect、query_config、dump_mod_state、get_scene_tree、get_recent_logs、give_item）互补使用。

## 工具总览

| 工具 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `get_screenshot` | `{format?, quality?}` | `{mimeType, base64, width, height, size}` | 获取游戏画面截图 |
| `raycast_mouse` | `{screenX?, screenY?, maxResults?}` | `{hitCount, hits}` | 鼠标位置射线检测 |
| `set_rng_seed` | `{seed?, forceRng?}` | `{result, changes}` | 设置随机种子 |
| `decompile_source` | `{outputDir, force?}` | `{status, manifestPath, dlls}` | 反编译 Witch.dll / Witch.Core.dll |

---

## 工具详情

### get_screenshot

截取当前游戏画面，返回 base64 编码的图片。

| 参数 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `format` | string | `png` | `png` 或 `jpg` |
| `quality` | int | 75 | JPG 质量（1-100），仅 jpg 有效 |

**Python：**
```python
import base64

# 截图
screenshot = g.call("get_screenshot", {"format": "jpg", "quality": 80})
print(f"尺寸: {screenshot['width']}x{screenshot['height']}, {screenshot['size']} 字节")

# 保存到文件
with open("screenshot.jpg", "wb") as f:
    f.write(base64.b64decode(screenshot['base64']))
```

### raycast_mouse

从鼠标位置（或指定屏幕坐标）发射射线，检测悬停的 GameObject。同时检测 UI 元素（EventSystem）、3D 物理和 2D 物理。

| 参数 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `screenX` | number | (鼠标) | 屏幕 X 坐标 |
| `screenY` | number | (鼠标) | 屏幕 Y 坐标 |
| `maxResults` | int | 30 | 最大返回结果数 |

**返回的每个 hit 字段：**
| 字段 | 说明 |
|------|------|
| `gameObjectName` | GameObject 名称 |
| `hierarchyPath` | 完整层级路径（如 `Canvas/Panel/Button/Text`） |
| `instanceId` | Unity 实例 ID |
| `source` | 检测源：`ui` / `physics3d` / `physics2d` |
| `isUI` | 是否是 UI 元素 |
| `canvasName` | 所属 Canvas 名称 |
| `components` | 挂载的组件类型列表 |
| `activeSelf` / `activeInHierarchy` | 激活状态 |

**Python：**
```python
# 鼠标当前位置检测
hits = g.call("raycast_mouse")
print(f"命中: {hits['hitCount']} 个对象")
for h in hits['hits'][:5]:
    print(f"  {h['gameObjectName']} ({h['source']})")
    print(f"    路径: {h['hierarchyPath']}")
    if h['isUI']:
        print(f"    Canvas: {h.get('canvasName', '?')}")

# 指定坐标检测
hits = g.call("raycast_mouse", {"screenX": 960, "screenY": 540})
```

### set_rng_seed

控制游戏的随机数生成器，用于可复现的测试。

| 参数 | 类型 | 说明 |
|------|------|------|
| `seed` | int | 设置 TempDataManager 随机种子 |
| `forceRng` | number | 强制下一次 Dice 结果（0.0~1.0） |

**Python：**
```python
# 设置种子，使下一步随机结果可复现
g.call("set_rng_seed", {"seed": 12345})

# 强制下一次随机结果为 0.5
g.call("set_rng_seed", {"forceRng": 0.5})
```

### decompile_source

反编译游戏程序集 Witch.dll 和 Witch.Core.dll 到指定目录。按 DLL 哈希分目录缓存，已缓存的不会重复反编译。

| 参数 | 类型 | 必填 | 默认 | 说明 |
|------|------|------|------|------|
| `outputDir` | string | 是 | — | 反编译缓存根目录 |
| `force` | bool | 否 | false | 强制重新反编译 |

**异步模式（重要）：** 反编译在独立的 .NET 8 子进程中运行，工具会**立即返回**并告知进程 ID。AI 应按以下流程操作：

**调用流程（三次调用模式）：**

| 步骤 | 操作 | 典型耗时 |
|------|------|---------|
| 1️⃣ | 调用 `decompile_source` → 返回 `status: "started"` + `processIds` | < 1 秒 |
| 2️⃣ | **轮询进程是否存活**（用 `Get-Process -Id $pid` 或 `kill -0 $pid`），直到进程退出 | ~30 秒 |
| 3️⃣ | 再次调用 `decompile_source`（同 `outputDir`） → 返回 `status: "fresh"` + `dlls` | < 1 秒 |

**返回：**
| 字段 | 说明 |
|------|------|
| `status` | `started`（子进程已启动）/ `running`（已有进程在跑）/ `fresh`（缓存有效） |
| `processIds` | dotnet 子进程的 PID 数组（仅 `started` / `running` 时） |
| `outputDir` | 输出目录（仅 `started` / `running` 时） |
| `manifestPath` | 清单文件路径（仅 `fresh` 时） |
| `dlls.Witch.dll` | `{hash, dir}` — Witch.dll 的反编译目录（仅 `fresh` 时） |
| `dlls.Witch.Core.dll` | `{hash, dir}` — Witch.Core.dll 的反编译目录（仅 `fresh` 时） |

**缓存目录结构：**
```
{outputDir}/
├── .decompile_manifest.json   ← 跟踪哈希
├── .decompile_{hash}.lock     ← 运行中进程的 PID 锁（自动清理）
├── 8d876.../                  ← Witch.dll 当前哈希
│   └── Witch.*.cs ...
└── ca6e9.../                  ← Witch.Core.dll 当前哈希
    └── Witch.Core.*.cs ...
```

**Python（异步流程）：**
```python
import time, os, signal

# 1. 启动反编译
r = g.call("decompile_source", {"outputDir": "./game_src"})
if r['status'] == 'started':
    pids = r['processIds']
    # 2. 等待所有子进程退出
    for pid in pids:
        while True:
            try:
                os.kill(pid, 0)  # 信号 0 只检查存在性
                time.sleep(3)
            except OSError:
                break  # 进程已退出
    # 3. 获取缓存结果
    r = g.call("decompile_source", {"outputDir": "./game_src"})

# 此时 r['status'] 应为 'fresh'
witch_dir = "./game_src/" + r['dlls']['Witch.dll']['dir']
core_dir = "./game_src/" + r['dlls']['Witch.Core.dll']['dir']

# 强制重新反编译
r = g.call("decompile_source", {"outputDir": "./game_src", "force": True})
```

---

## 基座诊断工具参考

DeveloperTools 的扩展诊断工具 + 基座 WitchModMCP 的诊断工具（inspect、query_config、dump_mod_state、get_scene_tree、get_recent_logs、give_item）构成完整诊断能力：

| 场景 | 使用的工具 | 来源 |
|------|-----------|------|
| "这个 UI 是什么？" | `raycast_mouse` | DeveloperTools |
| "游戏画面现在长什么样？" | `get_screenshot` | DeveloperTools |
| "让随机结果可复现" | `set_rng_seed` | DeveloperTools |
| "查看游戏 C# 源码" | `decompile_source` | DeveloperTools |
| "查 CardConfig 表" | `query_config` | 基座 |
| "反射查看 C# 对象" | `inspect` | 基座 |
| "Mod 加载了吗？" | `dump_mod_state` | 基座 |
| "给我 100 金" | `give_item` | 基座 |
| "场景树怎么组织的？" | `get_scene_tree` | 基座 |

## 最佳实践

1. **截图 vs 结构化数据** — `get_screenshot` 用于视觉确认；结构化数据用 `get_fight_state` / `get_game_data`
2. **射线检测诊断 UI** — 当需要知道鼠标悬停在哪个 UI 元素上时，用 `raycast_mouse`
3. **RNG 种子用于 Bug 复现** — 设置种子后相同操作序列产生相同随机结果，便于复现和调试 Bug
4. **反编译注意事项** — 反编译在独立 .NET 8 子进程中运行，首次约 30 秒。工具立即返回 PID，轮询进程退出后再次调用获取缓存结果。仅反编译 Witch.dll 和 Witch.Core.dll。
