# -*- coding: utf-8 -*-
"""打印 SplitDialogue 界面里布局相关组件的具体参数。

用法：py tools/inspect_split_layout.py
"""
import os

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

GAME_DIR = r"E:\Steam\steamapps\common\Escape from Duckov"
DATA_DIR = os.path.join(GAME_DIR, "Duckov_Data")
RES = os.path.join(DATA_DIR, "resources.assets")
GGM = os.path.join(DATA_DIR, "globalgamemanagers.assets")

WANTED = {
    "Content": {"VerticalLayoutGroup", "ContentSizeFitter", "RectTransform"},
    "BtnContainer": {"LayoutElement", "RectTransform"},
    "btn_ActivateQuest": {"LayoutElement", "RectTransform", "Button"},
    "SliderContainer": {"LayoutElement", "RectTransform"},
    "Normal": {"TextMeshProUGUI", "TextLocalizor", "RectTransform"},
    "Text (TMP)": {"TextLocalizor", "RectTransform"},
}


def script_map():
    env = UnityPy.load(GGM)
    mapping = {}
    for obj in env.objects:
        if obj.type.name != "MonoScript":
            continue
        try:
            mapping[obj.path_id] = getattr(obj.read(), "m_ClassName", "?")
        except Exception:
            pass
    return mapping


def comp_class(reader, scripts):
    if reader.type.name != "MonoBehaviour":
        return reader.type.name
    try:
        head = reader.parse_monobehaviour_head()
        return scripts.get(head.m_Script.path_id, "?")
    except Exception:
        return "?"


def main():
    scripts = script_map()
    env = UnityPy.load(RES)
    version = next(iter(env.files.values())).unity_version
    generator = TypeTreeGenerator(version)
    generator.load_local_game(GAME_DIR)
    env.typetree_generator = generator
    readers = {obj.path_id: obj for obj in env.objects}

    # SplitDialogue 组件的 GameObject
    script_id = next(pid for pid, name in scripts.items() if name == "SplitDialogue")
    split_comp = None
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            head = obj.parse_monobehaviour_head()
        except Exception:
            continue
        if head.m_Script.path_id == script_id:
            split_comp = obj
            break
    root_go = split_comp.read().m_GameObject.deref_parse_as_object()

    # 广度优先找目标 GameObject
    queue = [(root_go, 0)]
    found = {}
    while queue:
        go, depth = queue.pop(0)
        if go.m_Name in WANTED and go.m_Name not in found:
            found[go.m_Name] = go
        for pair in go.m_Component:
            ptr = pair.component if hasattr(pair, "component") else pair
            reader = readers.get(ptr.path_id)
            if reader is None or reader.type.name != "RectTransform":
                continue
            try:
                rect = reader.read()
                for child_ptr in getattr(rect, "m_Children", []) or []:
                    child = child_ptr.deref_parse_as_object()
                    child_go = child.m_GameObject.deref_parse_as_object()
                    queue.append((child_go, depth + 1))
            except Exception:
                pass

    for name, wanted in WANTED.items():
        go = found.get(name)
        if go is None:
            print(f"!! 未找到 {name}")
            continue
        print(f"\n=== {name} ===")
        for pair in go.m_Component:
            ptr = pair.component if hasattr(pair, "component") else pair
            reader = readers.get(ptr.path_id)
            if reader is None:
                continue
            cls = comp_class(reader, scripts)
            if cls not in wanted:
                continue
            print(f"-- {cls} (path_id={reader.path_id})")
            try:
                data = reader.read()
                for key, value in vars(data).items():
                    if key.startswith("_"):
                        continue
                    if isinstance(value, (int, float, bool, str)):
                        print(f"   {key} = {value!r}")
                    elif value is None:
                        print(f"   {key} = None")
                    else:
                        print(f"   {key} = {value}")
            except Exception as exc:
                print(f"   读取失败: {exc}")


if __name__ == "__main__":
    main()
