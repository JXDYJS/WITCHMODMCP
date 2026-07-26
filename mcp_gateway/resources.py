"""
resources — Expose witchSkill documentation as MCP Resources.

Auto-discovers all .md files under .agents/skills/witchSkill/ and registers
each as an MCP Resource (resource://witchskill/{relative_path}).

If the skill directory does not exist, registration is silently skipped.
"""

from pathlib import Path
from mcp_gateway.mcp_transport import SimpleMCP


def register_resources(mcp: SimpleMCP, workspace_dir: str) -> int:
    skill_root = Path(workspace_dir) / ".agents" / "skills" / "witchSkill"
    if not skill_root.is_dir():
        return 0

    count = 0
    for md_file in sorted(skill_root.rglob("*.md")):
        rel = md_file.relative_to(skill_root)
        uri = f"resource://witchskill/{rel.as_posix()}"
        name = _make_name(rel)

        @mcp.resource(uri, name=name, description="")
        def _handler(_path=md_file):
            try:
                return _path.read_text(encoding="utf-8")
            except Exception:
                return f"# Resource Unavailable\n\nFailed to read `{_path}`."
        count += 1

    return count


def _make_name(rel: Path) -> str:
    stem = rel.stem.replace("-", " ").replace("_", " ").title()
    parent_parts = list(rel.parts[:-1])
    if stem.lower() == "skill":
        if not parent_parts:
            return "WitchModMCP — Root Index"
        return " — ".join(p.replace("-", " ").replace("_", " ").title() for p in parent_parts)
    prefix = " — ".join(p.replace("-", " ").replace("_", " ").title() for p in parent_parts)
    return f"{prefix} — {stem}" if prefix else stem
