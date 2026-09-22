# -*- coding: utf-8 -*-
"""只读：把 Game.unity 的 5 张 PrefabEntry 映射表 + 敌人/NPC 战斗数据导成 JSON，供报表使用。"""
import json
import os
import re
import sys
from collections import Counter, OrderedDict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from floor_resource_tally import (build_guid_map, parse_prefab_entries,  # noqa: E402
                                  strip_json_comments, RES, ROOT, SCENE)

STAT_NAMES = {0: "攻击", 1: "防御", 2: "魔力输出", 3: "魔力充能", 4: "速度",
              5: "生命值", 6: "减伤", 7: "攻击倍率", 8: "防御倍率", 9: "生命倍率",
              10: "金币倍率"}


def script_guid(rel):
    with open(os.path.join(ROOT, "Assets", rel + ".meta"), "r", encoding="utf-8") as fh:
        return re.search(r"^guid:\s*([0-9a-fA-F]+)", fh.read(400), re.M).group(1)


def rel(p):
    return os.path.relpath(p, os.path.join(ROOT, "Assets")).replace("\\", "/") if p else ""


def main():
    g2p = build_guid_map()
    with open(SCENE, "r", encoding="utf-8") as fh:
        scene = fh.read()

    tables = {f: parse_prefab_entries(scene, f) for f in
              ("terrainPrefabs", "objectPrefabs", "enemyPrefabs", "itemPrefabs", "npcPrefabs")}

    sg_stat = script_guid("Scripts/Pickups/StatBoostPickup.cs")
    sg_amp = script_guid("Scripts/Pickups/MagicAmplifierPickup.cs")
    sg_aegis = script_guid("Scripts/Pickups/AegisAmuletPickup.cs")
    sg_enemy = script_guid("Scripts/Enemies/EnemyController.cs")
    sg_npcb = script_guid("Scripts/Dialogue/NpcBattler.cs")
    sg_drop = script_guid("Scripts/Drop/ItemDrop.cs")

    def docs(path):
        with open(path, "r", encoding="utf-8") as fh:
            text = fh.read()
        out = []
        for block in re.split(r"^--- !u!", text, flags=re.M)[1:]:
            fields = {}
            for ln in block.splitlines():
                mm = re.match(r"^  (\w+):\s*(.*)$", ln)
                if mm:
                    fields[mm.group(1)] = mm.group(2).strip()
            m = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", block)
            out.append((m.group(1) if m else None, fields, block))
        return out

    def read_stats(guid):
        p = g2p.get(guid) if guid else None
        if not p or not os.path.exists(p):
            return None
        with open(p, "r", encoding="utf-8") as fh:
            t = fh.read()
        g = lambda k, d=0: (int(m.group(1)) if (m := re.search(rf"^  {k}:\s*(-?\d+)", t, re.M)) else d)
        nm = re.search(r"^  enemyName:\s*(.*)$", t, re.M)
        se = re.search(r"^  specialEnemyId:\s*(.*)$", t, re.M)
        return {
            "资产": os.path.basename(p).replace(".asset", ""),
            "名称": (nm.group(1).strip().strip('"') if nm else ""),
            "HP": g("hp"), "攻击": g("attack"), "防御": g("defense"),
            "攻击次数": g("attackCount", 1), "吸血%": g("lifeSteal"),
            "反伤%": g("reflectDamage"), "减伤%": g("damageReduction"),
            "魔力充能": g("manaCharge"), "魔力上限": g("manaMax", 100),
            "速度": g("speed"), "金币": g("goldReward"),
            "特殊敌人信号": (se.group(1).strip().strip('"') if se else ""),
        }

    def probe(guid, with_stats=True):
        p = g2p.get(guid)
        out = {"prefab": rel(p), "battler": False, "dropIds": [], "stats": None}
        if not p or not os.path.exists(p):
            out["prefab"] = "【找不到该 prefab（死引用）】"
            return out
        stats_guid = None
        for sg, fields, block in docs(p):
            if sg == sg_enemy:
                m = re.search(r"stats:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", block)
                if m:
                    stats_guid = m.group(1)
                out["battler"] = True
            elif sg == sg_npcb:
                out["battler"] = True
                m = re.search(r"enemyStats:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", block)
                if m and not stats_guid:
                    stats_guid = m.group(1)
            elif sg == sg_drop:
                for m in re.finditer(r"prefab:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]+)",
                                     block):
                    out["dropIds"].append(m.group(1))
        if with_stats:
            out["stats"] = read_stats(stats_guid) if stats_guid else None
            if stats_guid and out["stats"] is None:
                out["stats"] = {"资产": "【引用的 asset 不存在】"}
        return out

    # ---------- 道具：数值 + 出现层 ----------
    guid_itemid = {e["guid"]: e["id"] for e in tables["itemPrefabs"] if e["guid"]}
    item_meta = {}
    for e in tables["itemPrefabs"]:
        p = g2p.get(e["guid"])
        m = {"id": e["id"], "name": e["name"], "prefab": rel(p),
             "效果": "", "数值": "", "附加": ""}
        if p and os.path.exists(p):
            for sg, f, block in docs(p):
                if sg == sg_stat:
                    dg = re.search(r"guid:\s*([0-9a-fA-F]+)", f.get("data", ""))
                    if dg and dg.group(1) in g2p:
                        dp = g2p[dg.group(1)]
                        with open(dp, "r", encoding="utf-8") as fh:
                            dt = fh.read()
                        bt = re.search(r"^  boostType:\s*(\d+)", dt, re.M)
                        vl = re.search(r"^  value:\s*(-?\d+)", dt, re.M)
                        if bt:
                            m["效果"] = STAT_NAMES.get(int(bt.group(1)), f"类型{bt.group(1)}")
                        if vl:
                            m["数值"] = "+" + vl.group(1)
                        m["附加"] = os.path.basename(dp).replace(".asset", "")
                elif sg == sg_amp:
                    if f.get("applyStatBoost") == "1":
                        m["效果"] = STAT_NAMES.get(int(f.get("boostType", "-1")), "")
                        m["数值"] = "+" + str(f.get("boostValue", "0"))
                    m["附加"] = f"魔力伤害×{int(f.get('multiplierPercent', '100')) / 100:g}"
                elif sg == sg_aegis:
                    if f.get("applyStatBoost") == "1":
                        m["效果"] = STAT_NAMES.get(int(f.get("boostType", "-1")), "")
                        m["数值"] = "+" + str(f.get("boostValue", "0"))
                    m["附加"] = "免疫魔力光环 / 夹击"
            if m["效果"] == "" and "碎片" not in m["name"]:
                pass
        item_meta[e["id"]] = m

    # ---------- 敌人 / NPC ----------
    enemy_rows = []
    for e in tables["enemyPrefabs"]:
        info = probe(e["guid"])
        info["dropIds"] = [guid_itemid.get(g, f"未知({g[:8]})") for g in info["dropIds"]]
        info["id"] = e["id"]
        info["name"] = e["name"].strip()
        enemy_rows.append(info)

    npc_rows = []
    for e in tables["npcPrefabs"]:
        info = probe(e["guid"])
        info["dropIds"] = [guid_itemid.get(g, f"未知({g[:8]})") for g in info["dropIds"]]
        info["id"] = e["id"]
        info["name"] = e["name"].strip()
        npc_rows.append(info)

    # ---------- 各层出现情况 ----------
    files = sorted(f for f in os.listdir(RES) if re.match(r"^floor_-?\d+\.json$", f))
    appear = {"enemy": {}, "npc": {}, "item": {}, "object": {}}
    floor_names = {}
    for fn in files:
        d = json.loads(strip_json_comments(open(os.path.join(RES, fn), encoding="utf-8").read()))
        fl = d["floor"]
        floor_names[fl] = d.get("name", f"第{fl}层")
        for layer, key in (("enemies", "enemy"), ("npcs", "npc"),
                           ("items", "item"), ("objects", "object")):
            c = Counter(v for r in (d.get(layer) or []) for v in r if v)
            for k, n in c.items():
                appear[key].setdefault(k, {})[fl] = n

    out = {
        "tables": {k: [{"id": e["id"], "name": e["name"], "prefab": rel(g2p.get(e["guid"]))}
                       for e in v] for k, v in tables.items()},
        "items": item_meta,
        "enemies": enemy_rows,
        "npcs": npc_rows,
        "appear": appear,
        "floorNames": floor_names,
    }
    with open(os.path.join(HERE, "unity_tables.json"), "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)

    print("terrainPrefabs", len(out["tables"]["terrainPrefabs"]))
    print("objectPrefabs ", len(out["tables"]["objectPrefabs"]))
    print("enemyPrefabs  ", len(out["tables"]["enemyPrefabs"]))
    print("itemPrefabs   ", len(out["tables"]["itemPrefabs"]))
    print("npcPrefabs    ", len(out["tables"]["npcPrefabs"]))
    print()
    print("敌人（含战斗NPC 汇总）：")
    for r in enemy_rows + npc_rows:
        st = r["stats"] or {}
        print(f"  {'敌' if r in enemy_rows else 'NPC'}{r['id']:>3} {r['name']:<10} "
              f"battler={int(r['battler'])} HP={st.get('HP','-')} ATK={st.get('攻击','-')} "
              f"DEF={st.get('防御','-')} 金币={st.get('金币','-')} 掉落={r['dropIds']}")


if __name__ == "__main__":
    main()
