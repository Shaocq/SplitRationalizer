# -*- coding: utf-8 -*-
"""在 Unity 资源里定位 SplitDialogue，并打印它的 UI 层级与布局参数。

用途：确认「物品防吞」按钮应该插在哪里、面板高度由谁控制。
用法：py tools/inspect_split_dialogue.py
"""
import os
import re
import sys

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

GAME_DIR = r"E:\Steam\steamapps\common\Escape from Duckov"
DATA_DIR = os.path.join(GAME_DIR, "Duckov_Data")


def serialized_files():
    for name in sorted(os.listdir(DATA_DIR)):
        path = os.path.join(DATA_DIR, name)
        if not os.path.isfile(path):
            continue
        if name.endswith(".assets") or name == "globalgamemanagers" or re.fullmatch(r"level\d+", name):
            yield path


def find_script_id():
    path = os.path.join(DATA_DIR, "globalgamemanagers.assets")
    env = UnityPy.load(path)
    for obj in env.objects:
        if obj.type.name != "MonoScript":
            continue
        try:
            script = obj.read()
            cls = getattr(script, "m_ClassName", "")
        except Exception:
            continue
        if cls == "SplitDialogue":
            return path, obj.path_id
    return None, None


def scan_instances(script_id):
    """用最小头部解析（不需要 TypeTree）扫描所有序列化文件里的 MonoBehaviour。"""
    for path in serialized_files():
        try:
            env = UnityPy.load(path)
        except Exception as exc:
            print(f"跳过 {os.path.basename(path)}: {exc}")
            continue
        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                head = obj.parse_monobehaviour_head()
                ptr = head.m_Script
            except Exception:
                continue
            if getattr(ptr, "path_id", None) == script_id:
                yield path, env, obj


def attach_typetree(env):
    version = next(iter(env.files.values())).unity_version
    generator = TypeTreeGenerator(version)
    generator.load_local_game(GAME_DIR)
    env.typetree_generator = generator


def get_transform(go):
    for comp in go.m_Component:
        try:
            c = comp.component if hasattr(comp, "component") else comp
            target = c.deref_parse_as_object()
        except Exception:
            continue
        if target.__class__.__name__ == "RectTransform":
            return target
    return None


def dump_tree(go, depth, seen=None):
    if seen is None:
        seen = set()
    if id(go) in seen or depth > 20:
        return
    seen.add(id(go))

    rect = get_transform(go)
    comp_names = []
    text = None
    for comp in go.m_Component:
        try:
            c = comp.component if hasattr(comp, "component") else comp
            target = c.deref_parse_as_object()
        except Exception:
            continue
        cls = target.__class__.__name__
        comp_names.append(cls)
        if cls in ("TextMeshProUGUI", "Text"):
            for attr in ("m_text", "m_Text", "text"):
                if hasattr(target, attr):
                    text = getattr(target, attr)
                    break

    info = f"{'  ' * depth}{go.m_Name}"
    if rect is not None:
        try:
            info += (f"  [rect {rect.m_SizeDelta.x}x{rect.m_SizeDelta.y}"
                     f" pos=({rect.m_AnchoredPosition.x},{rect.m_AnchoredPosition.y})"
                     f" anchors=({rect.m_AnchorMin.x},{rect.m_AnchorMin.y})-({rect.m_AnchorMax.x},{rect.m_AnchorMax.y})"
                     f" pivot=({rect.m_Pivot.x},{rect.m_Pivot.y})]")
        except Exception:
            pass
    if comp_names:
        info += "  <" + ", ".join(comp_names) + ">"
    if text is not None:
        info += f"  text={text!r}"
    print(info)

    if rect is not None:
        for child_ptr in getattr(rect, "m_Children", []) or []:
            try:
                child = child_ptr.deref_parse_as_object()
                child_go = child.m_GameObject.deref_parse_as_object()
            except Exception:
                continue
            dump_tree(child_go, depth + 1, seen)


def main():
    script_path, script_id = find_script_id()
    if not script_path:
        sys.exit("未找到 SplitDialogue 的 MonoScript")
    print(f"找到脚本: {script_path}  MonoScript path_id={script_id}")

    hit = None
    for path, env, obj in scan_instances(script_id):
        print(f"找到实例: {path}  path_id={obj.path_id}")
        if hit is None:
            hit = (path, env, obj)
    if hit is None:
        sys.exit("未找到挂载 SplitDialogue 的 MonoBehaviour")

    path, env, obj = hit
    attach_typetree(env)
    data = obj.read()

    print(f"\nSplitDialogue MonoBehaviour path_id={obj.path_id}")
    for name in ("fadeGroup", "confirmButton", "countText", "normalIndicator", "slider"):
        value = getattr(data, name, None)
        if value is None:
            continue
        try:
            target = value.deref_parse_as_object()
            if hasattr(target, "m_GameObject"):
                go = target.m_GameObject.deref_parse_as_object()
                print(f"  {name}: {target.__class__.__name__} -> GameObject {go.m_Name}")
            else:
                print(f"  {name}: {target.__class__.__name__} {getattr(target, 'm_Name', '')}")
        except Exception as exc:
            print(f"  {name}: 解析失败 {exc}")

    root_go = data.m_GameObject.deref_parse_as_object()
    print(f"\n根对象: {root_go.m_Name}")
    dump_tree(root_go, 0)


if __name__ == "__main__":
    main()
