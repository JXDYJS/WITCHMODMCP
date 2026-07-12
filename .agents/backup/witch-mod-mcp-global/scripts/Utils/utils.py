import os
import json
import hashlib
import shutil
import subprocess
from datetime import datetime
from pathlib import Path
from typing import Optional

CONFIG_DIR = Path.home() / ".config" / "opencode" / "skills" / "witch-mod-mcp"
CACHE_DIR = CONFIG_DIR / "cache"
SRC_DIR = CACHE_DIR / "game_src"
CONFIG_FILE = CONFIG_DIR / "config.json"
DLL_NAMES = ["Witch.dll", "Witch.Core.dll"]

_DEFAULT_CONFIG = {
    "game_path": None,
    "dll_hashes": {},
    "last_decompile": None,
    "skip_decompile": False,
    "ilspy_version": None,
}


def _ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


# ── Config ──────────────────────────────────────────────────────────


def load_config() -> dict:
    cfg = dict(_DEFAULT_CONFIG)
    if CONFIG_FILE.exists():
        try:
            stored = json.loads(CONFIG_FILE.read_text(encoding="utf-8"))
            cfg.update(stored)
        except (json.JSONDecodeError, OSError):
            pass
    return cfg


def save_config(cfg: dict) -> None:
    _ensure_dir(CONFIG_DIR)
    CONFIG_FILE.write_text(
        json.dumps(cfg, indent=2, ensure_ascii=False), encoding="utf-8"
    )


# ── Game path ────────────────────────────────────────────────────────


def get_configured_path() -> Optional[str]:
    """Return game_path from config if the directory still exists."""
    cfg = load_config()
    path = cfg.get("game_path")
    if path and Path(path).is_dir():
        return str(Path(path).resolve())
    return None


def set_game_path(path: str) -> bool:
    """Save game_path and invalidate stale hashes. Returns True if valid."""
    p = Path(path).resolve()
    if not p.is_dir():
        return False
    cfg = load_config()
    cfg["game_path"] = str(p)
    cfg["dll_hashes"] = {}
    cfg["last_decompile"] = None
    save_config(cfg)
    return True


def clear_config() -> None:
    save_config(dict(_DEFAULT_CONFIG))


# ── Decompile skip flag ──────────────────────────────────────────────


def is_decompile_enabled() -> bool:
    return not load_config().get("skip_decompile", False)


def set_decompile_enabled(enabled: bool = True) -> None:
    cfg = load_config()
    cfg["skip_decompile"] = not enabled
    save_config(cfg)


# ── DLL hashing ──────────────────────────────────────────────────────

_DLL_LOG_PREFIX = "[witch-mod-mcp/dll]"


def _find_dlls(game_path: str) -> list[Path]:
    root = Path(game_path)
    found: list[Path] = []
    seen_names: set[str] = set()
    for name in DLL_NAMES:
        # 1. Root directory
        candidate = root / name
        if candidate.is_file():
            found.append(candidate)
            seen_names.add(name)
            continue
        # 2. Any *_Data/Managed/ subdirectory — pick the first that has ALL expected DLLs
        data_dirs = sorted(root.glob("*_Data"))
        if not data_dirs:
            continue
        # Prefer the first matching data dir, but warn if multiple exist
        best: Path | None = None
        for data_dir in data_dirs:
            candidate = data_dir / "Managed" / name
            if candidate.is_file():
                best = candidate
                break
        if best is None:
            continue
        if len(data_dirs) > 1:
            print(f"{_DLL_LOG_PREFIX} warning: multiple *_Data dirs found, using {best.parent.parent.name}/{best.parent.name}/{best.name}")
        found.append(best)
        seen_names.add(name)

    # Deduplicate (sanity)
    unique = list(dict.fromkeys(found))
    if len(unique) < len(found):
        print(f"{_DLL_LOG_PREFIX} warning: removed {len(found) - len(unique)} duplicate DLL path(s)")

    return unique


def _hash_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hash_dlls(game_path: str) -> dict[str, str]:
    """Return {dll_name: sha256} for Witch.dll and Witch.Core.dll."""
    dlls = _find_dlls(game_path)
    found_names = {dll.name for dll in dlls}
    missing = [n for n in DLL_NAMES if n not in found_names]
    if missing:
        raise FileNotFoundError(
            f"{_DLL_LOG_PREFIX} expected {len(DLL_NAMES)} DLL(s) but only found {len(dlls)}: "
            f"missing {missing}"
        )
    return {dll.name: _hash_file(dll) for dll in dlls}


def _hashes_changed(game_path: str, cfg: dict) -> bool:
    return hash_dlls(game_path) != cfg.get("dll_hashes", {})


# ── Decompilation (via local C# project using ICSharpCode.Decompiler) ──

DECOMPILER_PROJECT = Path(__file__).resolve().parent.parent / "Decompiler" / "Decompile.csproj"

_DECOMPILE_LOG_PREFIX = "[witch-mod-mcp/decompile]"
_DECOMPILE_TIMEOUT: int | None = None  # seconds, None = no limit


def ensure_dotnet() -> bool:
    try:
        subprocess.run(
            ["dotnet", "--info"],
            capture_output=True, check=True, timeout=15,
        )
        return True
    except (FileNotFoundError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
        return False


def decompile_all(game_path: str, out_dir: Path = SRC_DIR) -> None:
    dlls = _find_dlls(game_path)
    if not dlls:
        raise FileNotFoundError(
            f"None of {DLL_NAMES} found under {game_path}\n"
            f"Searched: root dir and Witch_Data/Managed/"
        )

    if not DECOMPILER_PROJECT.is_file():
        raise FileNotFoundError(
            f"Decompiler project not found: {DECOMPILER_PROJECT}"
        )

    # Restore once at the start to avoid per-DLL restore
    print(f"{_DECOMPILE_LOG_PREFIX} restoring NuGet packages…")
    subprocess.run(
        ["dotnet", "restore", str(DECOMPILER_PROJECT)],
        check=True, timeout=120,
    )

    _ensure_dir(out_dir)
    succeeded: list[Path] = []
    try:
        for dll in dlls:
            dll_out = out_dir / dll.stem
            if dll_out.is_dir():
                print(f"{_DECOMPILE_LOG_PREFIX} cleaning existing output for {dll.name}")
                import shutil
                shutil.rmtree(str(dll_out))
            _ensure_dir(dll_out)
            print(f"{_DECOMPILE_LOG_PREFIX} decompiling {dll.name} → {dll_out} …")
            subprocess.run(
                ["dotnet", "run", "--project", str(DECOMPILER_PROJECT),
                 "--", str(dll), str(dll_out)],
                check=True, timeout=_DECOMPILE_TIMEOUT,
            )
            succeeded.append(dll)
            print(f"{_DECOMPILE_LOG_PREFIX} finished {dll.name}")
    except Exception:
        # Clean up any partial output so stale files don't fool hash checks
        for dll in dlls:
            if dll not in succeeded:
                dll_out = out_dir / dll.stem
                if dll_out.is_dir():
                    import shutil
                    shutil.rmtree(str(dll_out))
                    print(f"{_DECOMPILE_LOG_PREFIX} cleaned partial output: {dll_out}")
        raise


# ── Orchestration ────────────────────────────────────────────────────


def ensure_src_updated(game_path: str) -> Path:
    """Hash-check DLLs and re-decompile if changed. Returns src directory."""
    cfg = load_config()

    try:
        needs_rebuild = _hashes_changed(game_path, cfg)
    except FileNotFoundError as e:
        print(f"[witch-mod-mcp] {e}")
        needs_rebuild = True

    if not needs_rebuild and SRC_DIR.is_dir():
        print("[witch-mod-mcp] DLL hashes unchanged, skipping decompilation")
        return SRC_DIR

    if not ensure_dotnet():
        raise RuntimeError("dotnet is not available — cannot decompile")

    if SRC_DIR.is_dir():
        shutil.rmtree(str(SRC_DIR))

    decompile_all(game_path, SRC_DIR)

    cfg["dll_hashes"] = hash_dlls(game_path)
    cfg["last_decompile"] = datetime.now().isoformat()
    cfg["ilspy_version"] = None
    save_config(cfg)

    meta = {
        "dll_hashes": cfg["dll_hashes"],
        "decompile_time": cfg["last_decompile"],
    }
    (SRC_DIR / ".source_meta.json").write_text(
        json.dumps(meta, indent=2, ensure_ascii=False), encoding="utf-8"
    )

    return SRC_DIR


def verify_source_fresh(game_path: str) -> tuple[bool, str]:
    """Quick freshness check without triggering decompilation.

    Returns (is_fresh, reason_string).
    """
    meta_path = SRC_DIR / ".source_meta.json"
    if not meta_path.is_file():
        return False, ".source_meta.json not found — source may be missing or stale"

    try:
        meta = json.loads(meta_path.read_text(encoding="utf-8"))
        stored = meta.get("dll_hashes", {})
        current = hash_dlls(game_path)
        if stored == current:
            ts = meta.get("decompile_time", "unknown")
            return True, f"source is fresh (last decompiled {ts})"
        changed = [k for k in stored if stored.get(k) != current.get(k)]
        return False, f"DLL(s) changed since last decompile: {changed}"
    except Exception as e:
        return False, f"freshness check failed: {e}"
