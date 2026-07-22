#!/usr/bin/env python3
"""Find event option display text from EventUI."""

import sys
import os
sys.path.insert(0, r"C:\Users\halas\.config\opencode\skills\witchSkill\testing")
from witch_mcp import WitchMcp

client = WitchMcp(port=3100)

def try_scan_ui():
    print("=== scan_ui (EventUI panel) ===")
    try:
        r = client.call("scan_ui", {"panel": "EventUI", "includeInactive": True, "interactableOnly": False})
        print(r)
    except Exception as e:
        print(f"ERROR: {e}")

def try_inspect_eventui():
    print("\n=== inspect EventUI ===")
    try:
        r = client.call("inspect", {"typeName": "EventUI", "maxDepth": 3, "maxItems": 30})
        print(r)
    except Exception as e:
        print(f"ERROR: {e}")

def try_scene_tree():
    print("\n=== get_scene_tree: Canvas/EventUI ===")
    try:
        r = client.call("get_scene_tree", {"rootName": "Canvas", "maxDepth": 8, "includeComponents": True})
        print(r)
    except Exception as e:
        print(f"ERROR: {e}")

def try_inspect_buttons():
    print("\n=== inspect ButtonManager components on option buttons ===")
    # Try to inspect through EventUI static instance
    for path in [
        "Instance.Windows[0].Content.Selector.option1",
        "Instance.Windows[0].Content.Selector.option1.Normal.Description",
    ]:
        print(f"\n--- inspect EventUI.{path} ---")
        try:
            r = client.call("inspect", {"typeName": "EventUI", "memberPath": path, "maxDepth": 2, "maxItems": 10})
            print(r)
        except Exception as e:
            print(f"ERROR: {e}")

def try_get_scene_state():
    print("\n=== get_scene_state ===")
    try:
        r = client.call("get_scene_state")
        print(r)
    except Exception as e:
        print(f"ERROR: {e}")


if __name__ == "__main__":
    try_get_scene_state()
    try_scan_ui()
    try_inspect_eventui()
    try_scene_tree()
    try_inspect_buttons()
