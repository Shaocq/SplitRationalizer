# -*- coding: utf-8 -*-
"""静态导出《逃离鸭科夫》本体全部物品的默认叠加上限。

数据源：
  Duckov_Data/resources.assets 中的 ItemAssetsCollection（MonoBehaviour, m_Name=ItemAssetsCollection）
  Duckov_Data/StreamingAssets/Localization/ChineseSimplified.csv （中文名）

依赖：UnityPy 1.25.4 + TypeTreeGeneratorAPI 0.0.10
用法：py tools/extract_stack_limits.py
输出：拆分合理化/vanilla_stack_limits.csv
"""
import csv
import os
import sys

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

GAME_DIR = r"E:\Steam\steamapps\common\Escape from Duckov"
DATA_DIR = os.path.join(GAME_DIR, "Duckov_Data")
ASSETS_FILE = os.path.join(DATA_DIR, "resources.assets")
LOCALIZATION_FILE = os.path.join(DATA_DIR, "StreamingAssets", "Localization", "ChineseSimplified.csv")
OUTPUT_FILE = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "vanilla_stack_limits.csv")


def load_localization(path):
    mapping = {}
    with open(path, "r", encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            key = (row.get("key") or "").strip()
            if key:
                mapping[key] = row.get("value") or ""
    return mapping


def find_collection(env):
    for f in env.files.values():
        objects = f.objects.values() if isinstance(f.objects, dict) else f.objects
        for obj in objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                name = obj.peek_name()
            except Exception:
                continue
            if name == "ItemAssetsCollection":
                return obj
    return None


def main():
    if not os.path.isfile(ASSETS_FILE):
        sys.exit(f"找不到 {ASSETS_FILE}")

    env = UnityPy.load(ASSETS_FILE)
    version = next(iter(env.files.values())).unity_version
    generator = TypeTreeGenerator(version)
    generator.load_local_game(GAME_DIR)
    env.typetree_generator = generator

    obj = find_collection(env)
    if obj is None:
        sys.exit("resources.assets 中未找到 ItemAssetsCollection")

    collection = obj.read()
    entries = collection.entries
    localization = load_localization(LOCALIZATION_FILE)

    rows = []
    for entry in entries:
        meta = entry.metaData
        key = meta.displayName or ""
        display = localization.get(key) or localization.get(key + "_Name") or key
        rows.append({
            "typeID": entry.typeID,
            "name": meta.name or "",
            "displayName": display,
            "displayNameKey": key,
            "maxStackCount": meta.maxStackCount,
            "defaultStackCount": meta.defaultStackCount,
            "stackable": "True" if meta.maxStackCount > 1 else "False",
        })

    rows.sort(key=lambda r: r["typeID"])

    with open(OUTPUT_FILE, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)

    stackable = [r for r in rows if r["maxStackCount"] > 1]
    print(f"条目总数: {len(rows)}")
    print(f"可堆叠: {len(stackable)}  不可堆叠: {len(rows) - len(stackable)}")
    print(f"输出: {OUTPUT_FILE}")

    distribution = {}
    for r in rows:
        distribution[r["maxStackCount"]] = distribution.get(r["maxStackCount"], 0) + 1
    print("堆叠上限分布（上限: 物品数）:")
    for limit in sorted(distribution):
        print(f"  {limit:>5}: {distribution[limit]}")


if __name__ == "__main__":
    main()
