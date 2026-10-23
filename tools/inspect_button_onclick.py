# -*- coding: utf-8 -*-
"""打印 btn_ActivateQuest 上 Button.m_OnClick 的持久化调用。

克隆按钮时 UnityEvent.RemoveAllListeners 只会清掉运行时监听器，
预制体里序列化的持久化调用仍会保留 —— 必须确认原按钮没有绑定确认逻辑。
"""
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

    button = None
    for pair in target.m_Component:
        ptr = pair.component if hasattr(pair, "component") else pair
        reader = readers.get(ptr.path_id)
        if reader is not None and reader.type.name == "Button":
            button = reader
            break

    if button is None:
        print("btn_ActivateQuest 上没有 Button 组件")
        return

    data = button.read()
    click = data.m_OnClick
    calls = click.m_PersistentCalls.m_Calls
    print(f"Button.m_OnClick 持久化调用数量：{len(calls)}")
    for call in calls:
        target_obj = call.m_Target
        target_path = getattr(target_obj, "path_id", target_obj)
        if hasattr(target_obj, "deref_parse_as_object"):
            try:
                target_type = target_obj.deref_parse_as_object().m_ClassName
            except Exception:
                target_type = "?"
        else:
            target_type = "?"
        print(
            f"  target={target_path} ({target_type}) "
            f"type={getattr(call, 'm_TargetAssemblyTypeName', '?')} "
            f"method={getattr(call, 'm_MethodName', '?')} "
            f"mode={getattr(call, 'm_Mode', '?')} "
            f"state={getattr(call, 'm_CallState', '?')}"
        )
    try:
        print(f"  m_MethodName 字段值 = {click.m_MethodName!r}")
    except Exception:
        pass


if __name__ == "__main__":
    main()
