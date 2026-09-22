# -*- coding: utf-8 -*-
"""
只读统计：逐层清点钥匙 / 门 / 基础属性碎片 / 生命恢复量。

数据来源：
  Assets/Resources/floor_XX.json        地图层（objects / items）
  Assets/Scenes/Game.unity              MapGenerator 的 objectPrefabs / itemPrefabs 映射（id -> prefab guid + 名称）
  Assets/**/*.meta                      guid -> 资源路径
  Prefab 内 StatBoostPickup.data        -> Assets/Data/Emanation/*.asset 的 boostType / value
不写入 Assets/，只输出报告。
"""
import json
import os
import re
import sys
from collections import OrderedDict

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless"
RES = os.path.join(ROOT, "Assets", "Resources")
SCENE = os.path.join(ROOT, "Assets", "Scenes", "Game.unity")


def strip_json_comments(text):
    """去掉 // 行注释与尾随逗号（Unity Newtonsoft 允许，标准 json 不允许）。"""
    out = []
    for line in text.splitlines():
        # 逐行找 //，但要避开字符串内的 //
        res = []
        i = 0
        in_str = False
        while i < len(line):
            c = line[i]
            if in_str:
                if c == "\\":
                    res.append(line[i:i + 2])
                    i += 2
                    continue
                if c == '"':
                    in_str = False
                res.append(c)
                i += 1
                continue
            if c == '"':
                in_str = True
                res.append(c)
                i += 1
                continue
            if c == "/" and i + 1 < len(line) and line[i + 1] == "/":
                break
            res.append(c)
            i += 1
        out.append("".join(res))
    text = "\n".join(out)
    text = re.sub(r",\s*([}\]])", r"\1", text)
    return text


def build_guid_map():
    """guid -> 资源绝对路径"""
    m = {}
    for dirpath, _dirs, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                continue
            p = os.path.join(dirpath, f)
            try:
                with open(p, "r", encoding="utf-8", errors="ignore") as fh:
                    head = fh.read(400)
            except OSError:
                continue
            mm = re.search(r"^guid:\s*([0-9a-fA-F]+)", head, re.M)
            if mm:
                target = p[:-5]  # 去掉 .meta
                m[mm.group(1)] = target
    return m


def parse_prefab_entries(scene_text, field):
    """从场景里抽取某个 PrefabEntry[] 字段 -> [(id, guid, displayName)]"""
    lines = scene_text.splitlines()
    start = None
    for i, ln in enumerate(lines):
        if ln.strip() == field + ":":
            start = i
            break
    if start is None:
        return []
    entries = []
    cur = None
    i = start + 1
    while i < len(lines):
        ln = lines[i]
        if ln and not ln.startswith("  "):  # 回到缩进 0，说明下一个字段开始
            break
        if re.match(r"^  \w", ln):  # 缩进 2 的字段行（非 "- " 列表项）→ 数组结束
            break
        s = ln.strip()
        if s.startswith("- id:"):
            if cur:
                entries.append(cur)
            cur = {"id": int(s.split(":", 1)[1].strip()), "guid": None, "name": ""}
        elif s.startswith("prefab:") and cur is not None:
            mm = re.search(r"guid:\s*([0-9a-fA-F]+)", s)
            cur["guid"] = mm.group(1) if mm else None
        elif s.startswith("displayName:") and cur is not None:
            raw = s.split(":", 1)[1].strip()
            try:
                cur["name"] = json.loads('"' + raw.strip('"') + '"')
            except Exception:
                cur["name"] = raw
        i += 1
    if cur:
        entries.append(cur)
    return entries


def parse_yaml_script_components(path):
    """返回 [(script_guid, {字段名: 原始值})]，仅顶层字段。"""
    with open(path, "r", encoding="utf-8", errors="ignore") as fh:
        text = fh.read()
    blocks = re.split(r"^--- !u!", text, flags=re.M)[1:]
    comps = []
    for b in blocks:
        m_script = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)", b)
        if not m_script:
            continue
        fields = {}
        for ln in b.splitlines():
            mm = re.match(r"^  (\w+):\s*(.*)$", ln)
            if mm:
                fields[mm.group(1)] = mm.group(2).strip()
        comps.append((m_script.group(1), fields))
    return comps


def main():
    guid2path = build_guid_map()
    with open(SCENE, "r", encoding="utf-8") as fh:
        scene = fh.read()

    items = parse_prefab_entries(scene, "itemPrefabs")
    objects = parse_prefab_entries(scene, "objectPrefabs")

    # ---- item id -> 语义 ----
    # 读 prefab 上的 StatBoostPickup(script guid) / KeyPickup，再解析 data 指向的资产
    stat_script_guid = None
    key_script_guid = None
    for p in (os.path.join(ROOT, "Assets", "Scripts", "Pickups", "StatBoostPickup.cs.meta"),
              os.path.join(ROOT, "Assets", "Scripts", "Pickups", "KeyPickup.cs.meta")):
        with open(p, "r", encoding="utf-8") as fh:
            g = re.search(r"^guid:\s*([0-9a-fA-F]+)", fh.read(400), re.M).group(1)
        if "StatBoost" in p:
            stat_script_guid = g
        else:
            key_script_guid = g

    item_info = OrderedDict()
    for e in items:
        prefab_path = guid2path.get(e["guid"])
        info = {"id": e["id"], "name": e["name"], "kind": None, "type": None,
                "value": None, "prefab": prefab_path}
        if prefab_path and os.path.exists(prefab_path):
            for sg, fields in parse_yaml_script_components(prefab_path):
                if sg == stat_script_guid:
                    m = re.search(r"guid:\s*([0-9a-fA-F]+)", fields.get("data", ""))
                    if m:
                        dpath = guid2path.get(m.group(1))
                        info["data"] = dpath
                        if dpath and os.path.exists(dpath):
                            with open(dpath, "r", encoding="utf-8") as fh:
                                dtext = fh.read()
                            bt = re.search(r"^  boostType:\s*(\d+)", dtext, re.M)
                            vl = re.search(r"^  value:\s*(-?\d+)", dtext, re.M)
                            dn = re.search(r"^  displayName:\s*(.*)$", dtext, re.M)
                            info["kind"] = "StatBoost"
                            info["type"] = int(bt.group(1)) if bt else None
                            info["value"] = int(vl.group(1)) if vl else None
                            if dn:
                                try:
                                    info["dataName"] = json.loads(dn.group(1).strip())
                                except Exception:
                                    info["dataName"] = dn.group(1).strip()
                elif sg == key_script_guid:
                    info["kind"] = "Key"
        item_info[e["id"]] = info

    obj_info = OrderedDict((e["id"], e) for e in objects)

    # ---- 逐层统计 ----
    files = sorted(f for f in os.listdir(RES) if re.match(r"^floor_-?\d+\.json$", f))
    rows = []
    for fn in files:
        with open(os.path.join(RES, fn), "r", encoding="utf-8") as fh:
            data = json.loads(strip_json_comments(fh.read()))
        floor = data["floor"]
        row = OrderedDict()
        row["floor"] = floor
        row["name"] = data.get("name", "")

        # 门（objects 1/2/3）
        objcount = {}
        for r in data.get("objects") or []:
            for v in r:
                if v:
                    objcount[v] = objcount.get(v, 0) + 1
        row["door_y"] = objcount.get(1, 0)
        row["door_b"] = objcount.get(2, 0)
        row["door_r"] = objcount.get(3, 0)

        # 道具
        agg = {}
        hp_total = 0
        hp_count = 0
        detail = []
        for y, r in enumerate(data.get("items") or []):
            for x, v in enumerate(r):
                if not v:
                    continue
                it = item_info.get(v)
                key = it["name"] if it else f"未注册ID{v}"
                agg[key] = agg.get(key, 0) + 1
                if it and it.get("kind") == "StatBoost":
                    tname = it.get("dataName") or ""
                    detail.append((x, y, v, key, tname, it["value"]))
                    if it["type"] == 5:  # HP
                        hp_total += it["value"] or 0
                        hp_count += 1
        row["key_y"] = agg.get("黄之钥", 0)
        row["key_b"] = agg.get("蓝之钥", 0)
        row["key_r"] = agg.get("红之钥", 0)
        row["key_aeon"] = agg.get("移涌之钥", 0)
        row["hp_total"] = hp_total
        row["hp_count"] = hp_count
        row["agg"] = agg
        row["detail"] = detail
        rows.append(row)

    out = {"items": item_info, "rows": rows}
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "floor_tally_raw.json"),
              "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)

    # ---- 控制台速览 ----
    print("=== item ID 映射（来自 Game.unity itemPrefabs + Prefab 上的 StatBoostData）===")
    for i, it in item_info.items():
        print(f"  id={i:>3} kind={str(it['kind']):<9} type={str(it['type']):<5} "
              f"value={str(it['value']):<6} 名={it['name']}  数据={it.get('dataName','')}")
    print()
    print("=== 逐层 ===")
    print(f"{'层':>4} {'名':<10} {'黄钥':>4} {'蓝钥':>4} {'红钥':>4} {'移涌钥':>5} "
          f"{'黄门':>4} {'蓝门':>4} {'红门':>4} {'HP瓶数':>6} {'HP总量':>6}")
    for r in rows:
        print(f"{r['floor']:>4} {r['name']:<10} {r['key_y']:>4} {r['key_b']:>4} {r['key_r']:>4} "
              f"{r['key_aeon']:>5} {r['door_y']:>4} {r['door_b']:>4} {r['door_r']:>4} "
              f"{r['hp_count']:>6} {r['hp_total']:>6}")

    all_names = sorted({n for r in rows for n in r["agg"]})
    print()
    print("=== 每层道具种类全集 ===")
    print(all_names)


if __name__ == "__main__":
    main()
