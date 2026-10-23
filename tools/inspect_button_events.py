# -*- coding: utf-8 -*-
"""打印 btn_ActivateQuest 上 PointerDownUpEvents / SizeDeltaToggle 的序列化字段。"""
import os

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

GAME_DIR = r"E:\Steam\steamapps\common\Escape from Duckov"
DATA_DIR = os.path.join(GAME_DIR, "Duckov_Data")
RES = os.path.join(DATA_DIR, "resources.assets")
GGM = os.path.join(DATA_DIR, "globalgamemanagers.assets")


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


def main():
    scripts = script_map()
    env = UnityPy.load(RES)
    version = next(iter(env.files.values())).unity_version
    generator = TypeTreeGenerator(version)
    generator.load_local_game(GAME_DIR)
    env.typetree_generator = generator
    readers = {obj.path_id: obj for obj in env.objects}

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

    target = None
    queue = [root_go]
    while queue:
        go = queue.pop(0)
        if go.m_Name == "btn_ActivateQuest":
            target = go
            break
        for pair in go.m_Component:
            ptr = pair.component if hasattr(pair, "component") else pair
            reader = readers.get(ptr.path_id)
            if reader is None or reader.type.name != "RectTransform":
                continue
            try:
                rect = reader.read()
                for child_ptr in getattr(rect, "m_Children", []) or []:
                    child = child_ptr.deref_parse_as_object()
                    queue.append(child.m_GameObject.deref_parse_as_object())
            except Exception:
                pass

    if target is None:
        print("未找到 btn_ActivateQuest")
        return

    print("btn_ActivateQuest 组件：")
    for pair in target.m_Component:
        ptr = pair.component if hasattr(pair, "component") else pair
        reader = readers.get(ptr.path_id)
        if reader is None:
            continue
        name = reader.type.name
        if name == "MonoBehaviour":
            try:
                name = scripts.get(reader.parse_monobehaviour_head().m_Script.path_id, "?")
            except Exception:
                name = "?"
        print(f"  {name} (path_id={reader.path_id})")
        if name not in ("PointerDownUpEvents", "SizeDeltaToggle"):
            continue
        try:
            data = reader.read()
            for key, value in vars(data).items():
                if key.startswith("_"):
                    continue
                if key in ("onPointerDown", "onPointerUp"):
                    try:
                        calls = value.m_PersistentCalls.m_Calls
                        print(f"     {key}: {len(calls)} 个持久化调用")
                        for call in calls:
                            target_path = getattr(getattr(call.m_Target, "path_id", None) or call.m_Target, "path_id", call.m_Target)
                            print(
                                f"       target={target_path} type={getattr(call, 'm_TargetAssemblyTypeName', '?')} "
                                f"method={getattr(call, 'm_MethodName', '?')} mode={getattr(call, 'm_Mode', '?')} "
                                f"callState={getattr(call, 'm_CallState', '?')}"
                            )
                    except Exception as exc:
                        print(f"     {key}: 解析失败 {exc}")
                    continue
                print(f"     {key} = {value}")
        except Exception as exc:
            print(f"     读取失败: {exc}")


if __name__ == "__main__":
    main()
