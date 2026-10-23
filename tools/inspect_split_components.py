# -*- coding: utf-8 -*-
"""列出 SplitDialogue 界面关键节点上的所有组件（含脚本类名）与有效尺寸。

用法：py tools/inspect_split_components.py
"""
import os
import re

import UnityPy

GAME_DIR = r"E:\Steam\steamapps\common\Escape from Duckov"
DATA_DIR = os.path.join(GAME_DIR, "Duckov_Data")
RES = os.path.join(DATA_DIR, "resources.assets")
GGM = os.path.join(DATA_DIR, "globalgamemanagers.assets")

TARGET_PATHS = [
    "ItemSplitDialogue",
    "ItemSplitDialogue/Content",
    "ItemSplitDialogue/Content/BtnContainer",
    "ItemSplitDialogue/Content/BtnContainer/btn_ActivateQuest",
    "ItemSplitDialogue/Content/BtnContainer/btn_ActivateQuest/Normal",
    "ItemSplitDialogue/Content/BtnContainer/btn_ActivateQuest/Busy",
    "ItemSplitDialogue/Content/BtnContainer/btn_ActivateQuest/Busy/Image",
    "ItemSplitDialogue/Content/Text (TMP)",
    "ItemSplitDialogue/Content/SliderContainer",
]


def script_map():
    env = UnityPy.load(GGM)
    mapping = {}
    for obj in env.objects:
        if obj.type.name != "MonoScript":
            continue
        try:
            script = obj.read()
        except Exception:
            continue
        mapping[obj.path_id] = getattr(script, "m_ClassName", "?")
    return mapping


def comp_name(reader, scripts):
    if reader.type.name != "MonoBehaviour":
        return reader.type.name
    try:
        head = reader.parse_monobehaviour_head()
        return f"{scripts.get(head.m_Script.path_id, '?')} (MB)"
    except Exception as exc:
        return f"MonoBehaviour (head failed: {exc})"


def dump(go, path, readers, scripts, depth=0, seen=None):
    if seen is None:
        seen = set()
    if id(go) in seen:
        return
    seen.add(id(go))

    names = []
    for pair in go.m_Component:
        ptr = pair.component if hasattr(pair, "component") else pair
        reader = readers.get(ptr.path_id)
        if reader is None:
            names.append(f"<missing {ptr.path_id}>")
            continue
        names.append(comp_name(reader, scripts))

    print(f"{'  ' * depth}[{path}] {go.m_Name}")
    print(f"{'  ' * depth}  components: {', '.join(names)}")

    # 找 RectTransform 并打印有效宽高（基于父节点宽度）
    rect = None
    for pair in go.m_Component:
        ptr = pair.component if hasattr(pair, "component") else pair
        reader = readers.get(ptr.path_id)
        if reader is not None and reader.type.name == "RectTransform":
            try:
                rect = reader.read()
            except Exception:
                pass
    if rect is not None:
        try:
            print(f"{'  ' * depth}  sizeDelta=({rect.m_SizeDelta.x}, {rect.m_SizeDelta.y}) "
                  f"anchors=({rect.m_AnchorMin.x},{rect.m_AnchorMin.y})-({rect.m_AnchorMax.x},{rect.m_AnchorMax.y}) "
                  f"pos=({rect.m_AnchoredPosition.x},{rect.m_AnchoredPosition.y}) "
                  f"pivot=({rect.m_Pivot.x},{rect.m_Pivot.y})")
        except Exception:
            pass

    if rect is not None:
        for child_ptr in getattr(rect, "m_Children", []) or []:
            try:
                child = child_ptr.deref_parse_as_object()
                child_go = child.m_GameObject.deref_parse_as_object()
            except Exception:
                continue
            child_path = f"{path}/{child_go.m_Name}"
            if any(t == child_path or t.startswith(child_path + "/") for t in TARGET_PATHS):
                dump(child_go, child_path, readers, scripts, depth + 1, seen)


def main():
    scripts = script_map()
    env = UnityPy.load(RES)
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    version = next(iter(env.files.values())).unity_version
    generator = TypeTreeGenerator(version)
    generator.load_local_game(GAME_DIR)
    env.typetree_generator = generator
    readers = {obj.path_id: obj for obj in env.objects}

    # 定位 SplitDialogue 的 GameObject（已知 path_id=89206 是组件；重新按脚本找）
    script_id = None
    for pid, name in scripts.items():
        if name == "SplitDialogue":
            script_id = pid
            break
    if script_id is None:
        raise SystemExit("SplitDialogue 脚本未找到")

    comp_reader = readers[89206] if 89206 in readers else None
    if comp_reader is None:
        # 退路：扫描
        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                head = obj.parse_monobehaviour_head()
            except Exception:
                continue
            if head.m_Script.path_id == script_id:
                comp_reader = obj
                break
    data = comp_reader.read()
    root_go = data.m_GameObject.deref_parse_as_object()
    dump(root_go, "ItemSplitDialogue", readers, scripts)


if __name__ == "__main__":
    main()
