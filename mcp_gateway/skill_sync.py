#!/usr/bin/env python3
"""
skill_sync — Sync all mod skill docs into a single unified skill tree.

All mods' mcp_skills/ contents are merged into `.agents/skills/witchskill/`.
Each mod contributes subdirectories (base/, devtools/, insights/, patterns/)
that merge into one unified tree. The root SKILL.md from the primary mod
(WitchModMCP) ties everything together.
"""

import json
import os
import shutil
from pathlib import Path


UNIFIED_NAME = "witchskill"
INDEX_NAME = "MASTER_INDEX.md"


def sync_directory(source_path: str, target_dir: str) -> bool:
    """Copy all contents of source_path into target_dir (overwriting)."""
    src = Path(source_path)
    dst = Path(target_dir)

    if not src.is_dir():
        return False

    dst.mkdir(parents=True, exist_ok=True)

    for item in src.iterdir():
        s = str(item)
        d = str(dst / item.name)
        if item.is_dir():
            if os.path.exists(d):
                shutil.rmtree(d)
            shutil.copytree(s, d)
        else:
            shutil.copy2(s, d)
    return True


def collect_expected_items(active_modules: list) -> set[str]:
    """Return the union of all top-level item names across all mod skill paths."""
    expected: set[str] = set()
    for mod in active_modules:
        skill_path = mod.get("skillPath")
        if not skill_path:
            continue
        src = Path(skill_path)
        if src.is_dir():
            for item in src.iterdir():
                expected.add(item.name)
    return expected


def clean_orphans(target_dir: str, keep_names: set[str]):
    """Remove items in target_dir that are not in keep_names."""
    dst = Path(target_dir)
    if not dst.is_dir():
        return
    for item in list(dst.iterdir()):
        if item.name not in keep_names:
            if item.is_dir():
                shutil.rmtree(str(item))
            else:
                item.unlink()


def generate_master_index(cache_dir: str):
    """Generate MASTER_INDEX.md for the unified witchskill/ tree."""
    witchskill_dir = Path(cache_dir) / UNIFIED_NAME
    lines = [
        "# MASTER_INDEX — Unified Mod Skill Documentation",
        "",
        "All MCP tools are documented under the [witchskill](witchskill/SKILL.md) unified skill tree.",
        "",
    ]

    if witchskill_dir.is_dir():
        md_files = sorted(witchskill_dir.rglob("*.md"))
        for md in md_files:
            rel = str(md.relative_to(cache_dir))
            title = md.stem
            try:
                content = md.read_text(encoding="utf-8", errors="replace")
                for line in content.splitlines():
                    s = line.strip()
                    if s.startswith("# "):
                        title = s[2:].strip()
                        break
            except OSError:
                pass
            lines.append(f"- [{title}]({rel})")

    idx_path = Path(cache_dir) / INDEX_NAME
    idx_path.write_text("\n".join(lines), encoding="utf-8")


def sync_skill_docs(
    heartbeat_response: dict,
    local_skills_dir: str,
    global_skills_dir: str | None = None,
) -> dict:
    """
    Merge all mod skill docs into a single unified skill tree.

    Each mod's mcp_skills/ contents are merged into `.agents/skills/witchskill/`.
    The primary mod (WitchModMCP) provides the root SKILL.md and base/ subfolder;
    DeveloperTools contributes devtools/ subfolder.

    Args:
        heartbeat_response: /heartbeat response with 'activeModules' array.
        local_skills_dir:   Workspace .agents/skills/ directory.
        global_skills_dir:  Optional ~/.config/opencode/skills/ directory.

    Returns:
        { 'synced': {assembly_name: count}, 'errors': [...] }
    """
    active_modules = heartbeat_response.get("activeModules") or []
    result = {"synced": {}, "errors": []}

    if not active_modules:
        result["errors"].append("No active modules in heartbeat response")
        return result

    targets = [(local_skills_dir, "local")]
    if global_skills_dir:
        targets.append((global_skills_dir, "global"))

    # Phase 1: collect union of all top-level items across mods
    expected = collect_expected_items(active_modules)

    # Phase 2: sync each mod into targets (primary mod last so its root SKILL.md wins)
    sorted_modules = sorted(active_modules, key=lambda m: (
        1 if m.get("assemblyName") == "WitchModMCP" else 0
    ))
    for mod in sorted_modules:
        asm_name = mod.get("assemblyName")
        skill_path = mod.get("skillPath")
        if not asm_name or not skill_path:
            continue
        if not os.path.isdir(skill_path):
            result["errors"].append(f"{asm_name}: skillPath not found ({skill_path})")
            continue

        for root_dir, label in targets:
            target = os.path.join(root_dir, UNIFIED_NAME)
            sync_directory(skill_path, target)

        md_count = len(list(Path(skill_path).rglob("*.md")))
        result["synced"][asm_name] = md_count

    # Phase 3: clean orphans from all targets (removes stale dirs/files)
    for root_dir, label in targets:
        target = os.path.join(root_dir, UNIFIED_NAME)
        clean_orphans(target, expected)

    # Phase 4: regenerate master index
    generate_master_index(local_skills_dir)
    if global_skills_dir:
        generate_master_index(global_skills_dir)

    return result
