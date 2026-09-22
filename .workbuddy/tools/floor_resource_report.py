# -*- coding: utf-8 -*-
"""生成逐层资源统计表（CSV + HTML 报告），数据来自 floor_resource_tally.py。"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "reports")

COLS = [
    ("key_y", "黄钥匙"), ("key_b", "蓝钥匙"), ("key_r", "红钥匙"),
    ("door_y", "黄门"), ("door_b", "蓝门"), ("door_r", "红门"),
    ("g_atk", "攻击碎片"), ("g_def", "防御碎片"),
    ("g_mc", "魔力充能"), ("g_mm", "魔力输出"), ("g_spd", "速度"),
    ("hp_count", "生命药水(个)"), ("hp_total", "药水总生命值"),
]

GEMS = {
    "攻击力碎片": "g_atk", "防御力碎片": "g_def",
    "魔力充能碎片": "g_mc", "魔力输出碎片": "g_mm", "速度碎片": "g_spd",
}


def load():
    with open(os.path.join(HERE, "floor_tally_raw.json"), encoding="utf-8") as fh:
        raw = json.load(fh)
    item_info = {int(k): v for k, v in raw["items"].items()}
    rows = []
    for r in raw["rows"]:
        agg = r["agg"]
        rec = {k: r[k] for k, _ in COLS if k in r}
        for name, key in GEMS.items():
            rec[key] = agg.get(name, 0)
        # 移涌门：objects 7 + 21..27
        rec["door_aeon"] = r.get("_door_aeon", 0)
        rows.append((r["floor"], r["name"], rec))
    return item_info, rows


def main():
    # 重新扫一遍 objects 以补 移涌门（floor_tally_raw 没存）
    import re
    from floor_resource_tally import strip_json_comments, RES
    door_aeon = {}
    door_soul = {}
    obj_extra = []
    for fn in sorted(f for f in os.listdir(RES) if re.match(r"^floor_-?\d+\.json$", f)):
        d = json.loads(strip_json_comments(open(os.path.join(RES, fn), encoding="utf-8").read()))
        vals = [v for r in (d.get("objects") or []) for v in r if v]
        door_aeon[d["floor"]] = sum(1 for v in vals if v == 7 or 21 <= v <= 27)
        door_soul[d["floor"]] = sum(1 for v in vals if v in (4, 5, 6))
        obj_extra.append((d["floor"], d.get("name", ""), door_aeon[d["floor"]], door_soul[d["floor"]]))

    with open(os.path.join(HERE, "floor_tally_raw.json"), encoding="utf-8") as fh:
        raw = json.load(fh)

    rows = []
    for r in raw["rows"]:
        agg = r["agg"]
        rec = {"name": r["name"], "floor": r["floor"]}
        rec.update({k: r.get(k, 0) for k, _ in COLS if k in r})
        for name, key in GEMS.items():
            rec[key] = agg.get(name, 0)
        rec["door_aeon"] = door_aeon.get(r["floor"], 0)
        rec["door_soul"] = door_soul.get(r["floor"], 0)
        rows.append(rec)

    # 宝石属性总值（数量 x 单件数值）—— 只统计 5 种「属性碎片」，不含剑/盾等装备
    gem_value = {}
    for r in raw["rows"]:
        tot = {"g_atk": 0, "g_def": 0, "g_mc": 0, "g_mm": 0, "g_spd": 0}
        for (_x, _y, vid, _nm, _dn, val) in r["detail"]:
            it = raw["items"][str(vid)]
            if "碎片" not in _nm:  # 只算属性碎片，排除剑 / 盾 / 金币等
                continue
            t = it["type"]
            if t == 0:
                tot["g_atk"] += val
            elif t == 1:
                tot["g_def"] += val
            elif t == 3:
                tot["g_mc"] += val
            elif t == 2:
                tot["g_mm"] += val
            elif t == 4:
                tot["g_spd"] += val
        gem_value[r["floor"]] = tot

    ranges = [(1, 8, "1-8层"), (9, 18, "9-18层"), (19, 25, "19-25层"),
              (29, 31, "29-31层"), (1, 31, "1-31层")]
    sums = []
    for a, b, label in ranges:
        acc = {k: 0 for k, _ in COLS}
        for r in rows:
            if a <= r["floor"] <= b:
                for k in acc:
                    acc[k] += r.get(k, 0)
        acc["label"] = label
        acc["floor"] = label
        sums.append(acc)

    # ---------- CSV ----------
    os.makedirs(OUT, exist_ok=True)
    csv_path = os.path.join(OUT, "逐层资源统计.csv")
    header = ["楼层"] + [t for _, t in COLS] + ["魂之门"]
    lines = [",".join(header)]
    for r in rows:
        if r["floor"] < 1:
            continue
        lines.append(",".join([r["name"]] + [str(r.get(k, 0)) for k, _ in COLS]
                              + [str(r["door_soul"])]))
    for s in sums:
        lines.append(",".join([s["floor"]] + [str(s[k]) for k, _ in COLS]
                              + [str(sum(r.get("door_soul", 0) for r in rows
                                         if int(s["floor"].split("-")[0]) <= r["floor"]
                                         <= int(s["floor"].split("-")[1][:-1])))]))

    # 重新算魂门小计（上面的表达式太绕，直接重算）
    lines = lines[:1 + sum(1 for r in rows if r["floor"] >= 1)]
    soul_sum = {}
    for a, b, label in ranges:
        soul_sum[label] = sum(r.get("door_soul", 0) for r in rows if a <= r["floor"] <= b)
    for s in sums:
        lines.append(",".join([s["floor"]] + [str(s[k]) for k, _ in COLS]
                              + [str(soul_sum[s["floor"]])]))
    with open(csv_path, "w", encoding="utf-8-sig") as fh:
        fh.write("\n".join(lines))

    # ---------- HTML ----------
    def cls(v):
        return "z" if v == 0 else ""

    th = "".join(f"<th>{t}</th>" for _, t in COLS)
    body = []
    for r in rows:
        if r["floor"] < 1:
            continue
        tds = "".join(f'<td class="{cls(r.get(k, 0))}">{r.get(k, 0)}</td>' for k, _ in COLS)
        body.append(f'<tr><th class="fh">{r["name"]}</th>{tds}</tr>')
    for s in sums:
        tds = "".join(f'<td class="sv">{s[k]}</td>' for k, _ in COLS)
        body.append(f'<tr class="sum"><th class="fh">{s["label"]}</th>{tds}</tr>')

    # 宝石总值表
    vh = "".join(f"<th>{t}</th>" for t in ["攻击", "防御", "魔力充能", "魔力输出", "速度"])
    vbody = []
    for r in rows:
        if r["floor"] < 1:
            continue
        g = gem_value[r["floor"]]
        tds = "".join(f'<td class="{cls(g[k])}">{g[k]}</td>'
                      for k in ["g_atk", "g_def", "g_mc", "g_mm", "g_spd"])
        vbody.append(f'<tr><th class="fh">{r["name"]}</th>{tds}</tr>')

    html = f"""<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8">
<title>逐层资源统计</title>
<style>
*{{box-sizing:border-box}}
body{{margin:0;padding:28px 32px 60px;background:#f7f8fa;color:#1f2329;
 font-family:"Microsoft YaHei","PingFang SC",system-ui,sans-serif}}
h1{{font-size:20px;margin:0 0 6px}}
.sub{{color:#6b7280;font-size:12px;margin-bottom:20px}}
h2{{font-size:15px;margin:34px 0 10px;padding-left:9px;border-left:3px solid #3b6ef6}}
table{{border-collapse:separate;border-spacing:1px;background:#e3e6eb;font-size:12.5px;
 width:100%;table-layout:fixed}}
th,td{{padding:5px 4px;text-align:center;background:#fff;font-variant-numeric:tabular-nums}}
thead th{{background:#eef1f6;color:#3f4a5a;font-weight:600;font-size:12px}}
tbody th.fh{{background:#fafbfc;font-weight:600;text-align:left;padding-left:10px}}
td.z{{color:#c8ccd4}}
tr.sum th,tr.sum td{{background:#fff7e6;font-weight:700;color:#8a5a00}}
tr.sum td.sv{{border-top:1px solid #f0d9a8;border-bottom:1px solid #f0d9a8}}
tr:hover td,tr:hover th.fh{{background:#f2f6ff}}
tr.sum:hover td,tr.sum:hover th{{background:#fff2d6}}
.grp{{border-left:2px solid #dfe3ea}}
table.narrow{{max-width:820px}}
.note{{font-size:12px;color:#6b7280;line-height:1.75;margin-top:14px}}
code{{background:#eef1f6;padding:1px 5px;border-radius:3px;font-size:11.5px}}
</style></head><body>
<h1>Tower of the Endless · 逐层资源统计</h1>
<div class="sub">数据源：<code>Assets/Resources/floor_XX.json</code> 的 objects / items 层，
经 <code>Game.unity</code> 的 Prefab 映射表还原为实际物品；色块行 1-8 / 9-18 / 19-25 / 29-31 / 1-31 为区间总和。</div>

<h2>① 数量统计</h2>
<table><thead><tr><th style="width:84px">楼层</th>{th}</tr></thead>
<tbody>{''.join(body)}</tbody></table>

<h2>② 属性碎片带来的属性总值</h2>
<table class="narrow"><thead><tr><th style="width:84px">楼层</th>{vh}</tr></thead>
<tbody>{''.join(vbody)}</tbody></table>

<div class="note">
说明：<br>
· 「生命药水总生命值」= 该层全部生命恢复道具的回复量之和（100/200/300/400/600/800 分档）。<br>
· 属性碎片分三档（攻击 1/2/4、防御 2/4/8、魔力充能 10/20/40、魔力输出 10/20/40、速度 1/2/4），
表中 ① 是数量、② 是折算后的属性总值（② 只统计「碎片」，不含生息/魂流/悲焰/灵知剑盾与幸运金币等装备）。<br>
· 黄/蓝/红门来自 objects 层的 1/2/3；魂之门(4/5/6)、移涌之门(7 与 21-27)、上/下楼梯(8/9)
与各战斗门(10-20)不计入这三类。<br>
· 1-31 层区间总和已包含全部 31 层（含未归入任何子区间的第 26、27、28 层）。<br>
· 未列入本表但存在于地图上的资源：魂之门(objects 4/5/6，共 19 扇，分布在第 3/4/14/23/24/26/28/29 层)、
移涌之门(objects 7 与 21-27，1-31 层内只有第 30 层 1 扇，其余 7 扇分布在 -7 ~ -1 层)、
上/下楼梯(objects 8/9)、各战斗门与触发器(objects 10-20)。<br>
· <b>「移涌之钥」(items id 4) 在所有楼层均未放置</b>，只有移涌之门。<br>
· 第 0 层只有 1 个幸运金币，-1 ~ -7 层的 objects/items 里没有任何钥匙 / 门 / 碎片 / 药水。
</div>
</body></html>"""
    html_path = os.path.join(OUT, "逐层资源统计.html")
    with open(html_path, "w", encoding="utf-8") as fh:
        fh.write(html)

    # ---------- 控制台 ----------
    print("| 楼层 | " + " | ".join(t for _, t in COLS) + " |")
    print("| --- |" + " --- |" * len(COLS))
    for r in rows:
        if r["floor"] < 1:
            continue
        print(f"| {r['name']} | " + " | ".join(str(r.get(k, 0)) for k, _ in COLS) + " |")
    print()
    for s in sums:
        print(f"| {s['label']} | " + " | ".join(str(s[k]) for k, _ in COLS) + " |")
    print()
    print("CSV  ->", csv_path)
    print("HTML ->", html_path)


if __name__ == "__main__":
    main()
