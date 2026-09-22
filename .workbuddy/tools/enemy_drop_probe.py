# -*- coding: utf-8 -*-
"""只读：抽取敌人 / NPC 的 EnemyStats(金币) 与 ItemDrop 掉落配置。"""
import json
import os
import re

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless"
SCENE = os.path.join(ROOT, "Assets", "Scenes", "Game.unity")
HERE = os.path.dirname(os.path.abspath(__file__))

import sys
sys.path.insert(0, HERE)
from floor_resource_tally import build_guid_map, parse_prefab_entries, parse_yaml_script_components, strip_json_comments, RES


def script_guid(rel):
    p = os.path.join(ROOT, "Assets", rel + ".meta")
    with open(p, "r", encoding="utf-8") as fh:
        return re.search(r"^guid:\s*([0-9a-fA-F]+)", fh.read(400), re.M).group(1)


def main():
    g2p = build_guid_map()
    with open(SCENE, "r", encoding="utf-8") as fh:
        scene = fh.read()

    items = parse_prefab_entries(scene, "itemPrefabs")
    enemies = parse_prefab_entries(scene, "enemyPrefabs")
    npcs = parse_prefab_entries(scene, "npcPrefabs")

    guid_itemid = {e["guid"]: e["id"] for e in items if e["guid"]}

    sg_enemy = script_guid("Scripts/Enemies/EnemyController.cs")
    sg_npcb = script_guid("Scripts/Dialogue/NpcBattler.cs")
    sg_drop = script_guid("Scripts/Drop/ItemDrop.cs")

    def prefab_probe(path):
        out = {"stats": None, "statsName": None, "gold": None, "drops": [], "hasNpcBattler": False}
        if not path or not os.path.exists(path):
            return out
        for sg, fields in parse_yaml_script_components(path):
            if sg == sg_enemy:
                m = re.search(r"guid:\s*([0-9a-fA-F]+)", fields.get("stats", ""))
                if m:
                    out["stats"] = m.group(1)
            elif sg == sg_npcb:
                out["hasNpcBattler"] = True
                m = re.search(r"guid:\s*([0-9a-fA-F]+)", fields.get("enemyStats", ""))
                if m and not out["stats"]:
                    out["stats"] = m.group(1)
            elif sg == sg_drop:
                pass  # drops 是数组，下面单独解析
        # drops 数组：找 ItemDrop 那个 document 块
        with open(path, "r", encoding="utf-8") as fh:
            text = fh.read()
        for block in re.split(r"^--- !u!", text, flags=re.M)[1:]:
            if sg_drop not in block:
                continue
            for m in re.finditer(r"prefab:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]+)", block):
                out["drops"].append(m.group(1))
        # EnemyStats 资产
        if out["stats"] and out["stats"] in g2p:
            sp = g2p[out["stats"]]
            out["statsPath"] = sp
            if os.path.exists(sp):
                with open(sp, "r", encoding="utf-8") as fh:
                    st = fh.read()
                nm = re.search(r"^  enemyName:\s*(.*)$", st, re.M)
                gr = re.search(r"^  goldReward:\s*(-?\d+)", st, re.M)
                out["statsName"] = nm.group(1).strip() if nm else None
                out["gold"] = int(gr.group(1)) if gr else 0
        return out

    report = {"enemies": {}, "npcs": {}, "guid_itemid": guid_itemid}
    print("======== 普通敌人 ========")
    for e in enemies:
        p = g2p.get(e["guid"])
        info = prefab_probe(p)
        info["id"] = e["id"]
        info["name"] = e["name"]
        info["prefab"] = p
        info["dropIds"] = [guid_itemid.get(g, f"?{g[:8]}") for g in info["drops"]]
        report["enemies"][e["id"]] = info
        print(f"  id={e['id']:>3} {e['name'].strip():<12} 金币={info['gold']} 掉落={info['dropIds']} "
              f"stats={info.get('statsName')}")

    print("======== NPC ========")
    for e in npcs:
        p = g2p.get(e["guid"])
        info = prefab_probe(p)
        info["id"] = e["id"]
        info["name"] = e["name"]
        info["prefab"] = p
        info["dropIds"] = [guid_itemid.get(g, f"?{g[:8]}") for g in info["drops"]]
        report["npcs"][e["id"]] = info
        if info["hasNpcBattler"] or info["drops"] or info["gold"]:
            print(f"  id={e['id']:>3} {e['name']:<12} 可战斗={info['hasNpcBattler']} 金币={info['gold']} "
                  f"掉落={info['dropIds']} stats={info.get('statsName')}  ({os.path.basename(p or '')})")

    with open(os.path.join(HERE, "enemy_drop_raw.json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
