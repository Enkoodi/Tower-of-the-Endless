# -*- coding: utf-8 -*-
"""
逐层资源统计 v2 —— 只读。

相比 v1 的变化：
  1. 加「金币」列（敌人 / 可战斗 NPC 的 EnemyStats.goldReward 之和）；
  2. 道具统计并入敌人与 BOSS 的 ItemDrop 掉落物；
  3. 表 1 直接记录「数值」而不是数量，属性数值含装备（剑 / 盾 / 神圣剑 / 神圣盾）。

数据源：floor_XX.json + Game.unity(PrefabEntry 映射) + Prefab 上的
        StatBoostPickup / MagicAmplifierPickup / AegisAmuletPickup / ItemDrop / EnemyStats
"""
import json
import os
import re
import sys
from collections import OrderedDict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from floor_resource_tally import (strip_json_comments, build_guid_map,  # noqa: E402
                                  parse_prefab_entries, parse_yaml_script_components,
                                  RES, ROOT, SCENE)

OUT = os.path.join(os.path.dirname(HERE), "reports")

# StatBoostType 枚举下标 → 属性列
CAT = {0: "atk", 1: "def", 2: "mm", 3: "mc", 4: "spd", 5: "hp"}
CAT_NAME = {"atk": "攻击", "def": "防御", "mc": "魔力充能", "mm": "魔力输出",
            "spd": "速度", "hp": "生命值"}


def script_guid(rel):
    with open(os.path.join(ROOT, "Assets", rel + ".meta"), "r", encoding="utf-8") as fh:
        return re.search(r"^guid:\s*([0-9a-fA-F]+)", fh.read(400), re.M).group(1)


def main():
    g2p = build_guid_map()
    with open(SCENE, "r", encoding="utf-8") as fh:
        scene = fh.read()

    items = parse_prefab_entries(scene, "itemPrefabs")
    enemies = parse_prefab_entries(scene, "enemyPrefabs")
    npcs = parse_prefab_entries(scene, "npcPrefabs")

    sg_stat = script_guid("Scripts/Pickups/StatBoostPickup.cs")
    sg_amp = script_guid("Scripts/Pickups/MagicAmplifierPickup.cs")
    sg_aegis = script_guid("Scripts/Pickups/AegisAmuletPickup.cs")
    sg_enemy = script_guid("Scripts/Enemies/EnemyController.cs")
    sg_npcb = script_guid("Scripts/Dialogue/NpcBattler.cs")
    sg_drop = script_guid("Scripts/Drop/ItemDrop.cs")

    # ---------- 1. item id -> 属性类别 / 数值 ----------
    item_meta = {}
    for e in items:
        p = g2p.get(e["guid"])
        meta = {"id": e["id"], "name": e["name"], "cat": None, "value": 0, "extra": ""}
        if p and os.path.exists(p):
            with open(p, "r", encoding="utf-8") as fh:
                text = fh.read()
            for block in re.split(r"^--- !u!", text, flags=re.M)[1:]:
                m = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", block)
                if not m:
                    continue
                sg = m.group(1)
                fields = {}
                for ln in block.splitlines():
                    mm = re.match(r"^  (\w+):\s*(.*)$", ln)
                    if mm:
                        fields[mm.group(1)] = mm.group(2).strip()
                if sg == sg_stat:
                    dg = re.search(r"guid:\s*([0-9a-fA-F]+)", fields.get("data", ""))
                    if dg and dg.group(1) in g2p:
                        dp = g2p[dg.group(1)]
                        with open(dp, "r", encoding="utf-8") as fh:
                            dt = fh.read()
                        bt = re.search(r"^  boostType:\s*(\d+)", dt, re.M)
                        vl = re.search(r"^  value:\s*(-?\d+)", dt, re.M)
                        if bt:
                            meta["cat"] = CAT.get(int(bt.group(1)))
                        if vl:
                            meta["value"] = int(vl.group(1))
                elif sg == sg_amp:
                    # 神圣剑：boostValue 是属性加成，multiplierPercent 是魔力倍率
                    if fields.get("applyStatBoost") == "1":
                        meta["cat"] = CAT.get(int(fields.get("boostType", "-1")))
                        meta["value"] = int(fields.get("boostValue", "0"))
                    meta["extra"] = f"魔力伤害×{int(fields.get('multiplierPercent', '100')) / 100:g}"
                elif sg == sg_aegis:
                    if fields.get("applyStatBoost") == "1":
                        meta["cat"] = CAT.get(int(fields.get("boostType", "-1")))
                        meta["value"] = int(fields.get("boostValue", "0"))
                    meta["extra"] = "免疫魔力光环/夹击"
        item_meta[e["id"]] = meta

    guid_itemid = {e["guid"]: e["id"] for e in items if e["guid"]}

    # ---------- 2. 敌人 / NPC -> 金币 + 掉落 ----------
    def probe_battler(guid):
        p = g2p.get(guid)
        out = {"prefab": p, "gold": 0, "drops": [], "battle": False}
        if not p or not os.path.exists(p):
            return out
        stats_guid = None
        with open(p, "r", encoding="utf-8") as fh:
            text = fh.read()
        for block in re.split(r"^--- !u!", text, flags=re.M)[1:]:
            m = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", block)
            if not m:
                continue
            sg = m.group(1)
            if sg == sg_enemy:
                mm = re.search(r"stats:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", block)
                if mm:
                    stats_guid = mm.group(1)
                    out["battle"] = True
            elif sg == sg_npcb:
                out["battle"] = True
                mm = re.search(r"enemyStats:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", block)
                if mm and not stats_guid:
                    stats_guid = mm.group(1)
            elif sg == sg_drop:
                for mm in re.finditer(r"prefab:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]+)",
                                      block):
                    out["drops"].append(guid_itemid.get(mm.group(1)))
        if stats_guid and stats_guid in g2p:
            sp = g2p[stats_guid]
            if os.path.exists(sp):
                with open(sp, "r", encoding="utf-8") as fh:
                    st = fh.read()
                gr = re.search(r"^  goldReward:\s*(-?\d+)", st, re.M)
                out["gold"] = int(gr.group(1)) if gr else 0
        return out

    enemy_info = {e["id"]: dict(probe_battler(e["guid"]), name=e["name"].strip())
                  for e in enemies}
    npc_info = {e["id"]: dict(probe_battler(e["guid"]), name=e["name"].strip())
                for e in npcs}

    # ---------- 3. 逐层汇总 ----------
    ZERO = {"key_y": 0, "key_b": 0, "key_r": 0, "key_aeon": 0,
            "door_y": 0, "door_b": 0, "door_r": 0, "gold": 0,
            "atk": 0, "def": 0, "mc": 0, "mm": 0, "spd": 0, "hp": 0,
            "bless": 0, "frag_src": 0, "drop_src": 0}

    KEY_ID = {1: "key_y", 2: "key_b", 3: "key_r", 4: "key_aeon"}
    DOOR_ID = {1: "door_y", 2: "door_b", 3: "door_r"}

    files = sorted(f for f in os.listdir(RES) if re.match(r"^floor_-?\d+\.json$", f))
    rows = []
    detail_drop = []
    for fn in files:
        with open(os.path.join(RES, fn), "r", encoding="utf-8") as fh:
            data = json.loads(strip_json_comments(fh.read()))
        floor = data["floor"]
        rec = dict(ZERO)
        rec["floor"] = floor
        rec["name"] = data.get("name", "")

        # 地图自带物品
        for r in data.get("items") or []:
            for v in r:
                if v == 0:
                    continue
                if v in KEY_ID:
                    rec[KEY_ID[v]] += 1
                if v in (20, 40):
                    rec["bless"] += 1
                m = item_meta.get(v)
                if m and m["cat"]:
                    rec[m["cat"]] += m["value"]
                    rec["frag_src"] += 1

        # 门
        for r in data.get("objects") or []:
            for v in r:
                if v in DOOR_ID:
                    rec[DOOR_ID[v]] += 1

        # 敌人 + NPC 战斗：金币与掉落
        for layer, info in (("enemies", enemy_info), ("npcs", npc_info)):
            for r in data.get(layer) or []:
                for v in r:
                    if v == 0:
                        continue
                    inf = info.get(v)
                    if not inf:
                        continue
                    if inf["battle"]:
                        rec["gold"] += inf["gold"]
                    for di in inf["drops"]:
                        if di is None:
                            continue
                        if di in KEY_ID:
                            rec[KEY_ID[di]] += 1
                        if di in (20, 40):
                            rec["bless"] += 1
                        m = item_meta.get(di)
                        if m and m["cat"]:
                            rec[m["cat"]] += m["value"]
                            rec["drop_src"] += 1
                        detail_drop.append((floor, layer, v, inf["name"], di))

        rows.append(rec)

    # ---------- 4. 区间合计 ----------
    ranges = [(1, 8, "1-8层"), (9, 18, "9-18层"), (19, 25, "19-25层"),
              (26, 31, "26-31层"), (1, 31, "1-31层")]
    COLS = [("key_y", "黄钥匙"), ("key_b", "蓝钥匙"), ("key_r", "红钥匙"), ("key_aeon", "移涌之钥"),
            ("door_y", "黄门"), ("door_b", "蓝门"), ("door_r", "红门"), ("gold", "金币"),
            ("atk", "攻击"), ("def", "防御"), ("mc", "魔力充能"), ("mm", "魔力输出"),
            ("spd", "速度"), ("hp", "生命值")]
    sums = []
    for a, b, label in ranges:
        acc = {k: 0 for k, _ in COLS}
        for r in rows:
            if a <= r["floor"] <= b:
                for k in acc:
                    acc[k] += r[k]
        acc["label"] = label
        sums.append(acc)

    os.makedirs(OUT, exist_ok=True)

    # ---------- 5. CSV ----------
    header = ["楼层"] + [t for _, t in COLS]
    lines = [",".join(header)]
    for r in rows:
        if r["floor"] < 1:
            continue
        lines.append(",".join([r["name"]] + [str(r[k]) for k, _ in COLS]))
    for s in sums:
        lines.append(",".join([s["label"]] + [str(s[k]) for k, _ in COLS]))
    csv_path = os.path.join(OUT, "逐层资源统计.csv")
    with open(csv_path, "w", encoding="utf-8-sig") as fh:
        fh.write("\n".join(lines))

    # ---------- 6. HTML ----------
    def cls(v, k):
        if v == 0:
            return "z"
        return "hl" if k == "gold" else ""

    th = "".join(f"<th>{t}</th>" for _, t in COLS)
    body = []
    for r in rows:
        if r["floor"] < 1:
            continue
        tds = "".join(f'<td class="{cls(r[k], k)}">{r[k]}</td>' for k, _ in COLS)
        body.append(f'<tr><th class="fh">{r["name"]}</th>{tds}</tr>')
    for s in sums:
        tds = "".join(f'<td class="sv">{s[k]}</td>' for k, _ in COLS)
        body.append(f'<tr class="sum"><th class="fh">{s["label"]}</th>{tds}</tr>')

    html = f"""<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8"><title>逐层资源统计</title>
<style>
*{{box-sizing:border-box}}
body{{margin:0;padding:26px 30px 60px;background:#f7f8fa;color:#1f2329;
 font-family:"Microsoft YaHei","PingFang SC",system-ui,sans-serif}}
h1{{font-size:20px;margin:0 0 6px}}
.sub{{color:#6b7280;font-size:12px;margin-bottom:18px;line-height:1.7}}
table{{border-collapse:separate;border-spacing:1px;background:#e3e6eb;font-size:12.5px;
 width:100%;table-layout:fixed}}
th,td{{padding:5px 3px;text-align:center;background:#fff;font-variant-numeric:tabular-nums}}
thead th{{background:#eef1f6;color:#3f4a5a;font-weight:600;font-size:11.5px}}
tbody th.fh{{background:#fafbfc;font-weight:600;text-align:left;padding-left:10px}}
td.z{{color:#c8ccd4}}
td.hl{{background:#fffdf5;color:#8a5a00;font-weight:600}}
tr.sum th,tr.sum td{{background:#fff7e6;font-weight:700;color:#8a5a00}}
tr:hover td,tr:hover th.fh{{background:#f2f6ff}}
tr.sum:hover td,tr.sum:hover th{{background:#fff2d6}}
.note{{font-size:12px;color:#6b7280;line-height:1.8;margin-top:16px}}
code{{background:#eef1f6;padding:1px 5px;border-radius:3px;font-size:11.5px}}
</style></head><body>
<h1>Tower of the Endless · 逐层资源统计</h1>
<div class="sub">来源：<code>Assets/Resources/floor_XX.json</code> 的 objects / items / enemies / npcs 四层，
经 <code>Game.unity</code> 映射表还原为真实物品与敌人，再读 Prefab 上的
<code>StatBoostPickup</code> / <code>MagicAmplifierPickup</code> / <code>AegisAmuletPickup</code> /
<code>ItemDrop</code> / <code>EnemyStats</code>。<br>
橙色行 = 区间合计。属性列全部是「数值」而非件数。</div>
<table><thead><tr><th style="width:76px">楼层</th>{th}</tr></thead>
<tbody>{''.join(body)}</tbody></table>
<div class="note">
· <b>钥匙 / 门</b>为件数（本身没有可折算的数值）；黄 / 蓝 / 红门只可能来自 map 的 objects 层。<br>
· <b>移涌之钥</b>仅第 30 层魔王掉落 1 把，地图上从未直接放置。<br>
· <b>金币</b> = 该层全部敌人与可战斗 NPC 的 <code>goldReward</code> 之和（全歼一次的收入），
未扣商店消费；幸运金币（GoldMultiplier）不算金币。<br>
· <b>攻击 / 防御 / 魔力充能 / 魔力输出 / 速度 / 生命值</b> = 属性碎片 + 装备（生息/魂流/悲焰/灵知剑盾）+
  神圣剑(+100 攻击, 附带魔力伤害×2) / 神圣盾(+200 防御) 的数值总和，<u>已含 BOSS 掉落物</u>。<br>
· 第 20 层大魔导师、第 8 层史莱姆领主、第 25 层不死图腾、第 6 层吸血鬼领主、第 12 层史莱姆、第 30 层魔王
  的掉落已计入；「祝福」（items 20/40）共 21 个，未单列成列。
</div>
</body></html>"""
    html_path = os.path.join(OUT, "逐层资源统计.html")
    with open(html_path, "w", encoding="utf-8") as fh:
        fh.write(html)

    # ---------- 7. 控制台 ----------
    hdr = "楼层".ljust(8) + "".join(t.rjust(9) for _, t in COLS)
    print(hdr)
    for r in rows:
        if r["floor"] < 1:
            continue
        print(r["name"].ljust(8) + "".join(str(r[k]).rjust(7) + "  " for k, _ in COLS))
    print()
    for s in sums:
        print(s["label"].ljust(8) + "".join(str(s[k]).rjust(7) + "  " for k, _ in COLS))
    print()
    print("「祝福」(items 20/40，含掉落) 逐层：",
          {r["floor"]: r["bless"] for r in rows if r["floor"] >= 1 and r["bless"]})
    print("掉落明细（层 / 来源 / 物品ID）：")
    for d in detail_drop:
        print("   ", d, item_meta.get(d[4], {}).get("name"))
    print()
    print("CSV ->", csv_path)
    print("HTML->", html_path)


if __name__ == "__main__":
    main()
