# -*- coding: utf-8 -*-
"""只读分析：找出「战斗触发器(BattleTrigger) 与 敌人/NPC 同格」的格子，
并打印触发器的 spawnPositions、战斗门的 requiredEnemyPositions。"""
import io, json, os, re, glob

ROOT = r"D:/zzzMyWork/Game/unity/Tower of the Endless"
SCENE = os.path.join(ROOT, "Assets/Scenes/Game.unity")
RES = os.path.join(ROOT, "Assets/Resources")

def read(p):
    with io.open(p, encoding="utf-8", errors="replace") as f:
        return f.read()

def guid_to_path():
    m = {}
    for meta in glob.glob(os.path.join(ROOT, "Assets", "**", "*.meta"), recursive=True):
        t = read(meta)
        g = re.search(r"^guid:\s*([0-9a-f]+)", t, re.M)
        if g:
            m[g.group(1)] = meta[:-5]
    return m

G2P = guid_to_path()

def table(name, scene):
    """抽出 scene 里 name: 数组的 (id, guid, displayName) 列表。
    结束判据：缩进 2 且首字符是 \\w 的字段行。"""
    lines = scene.split("\n")
    start = None
    for i, l in enumerate(lines):
        if l.startswith("  " + name + ":"):
            start = i + 1
            break
    if start is None:
        return []
    out = []
    cur = None
    for l in lines[start:]:
        if l.strip() == "":
            continue
        if re.match(r"^  [A-Za-z_]\w*:", l):   # 下一个同缩进字段行（列表项是 "  - "，不算）
            break
        s = l.strip()
        if s.startswith("- id:"):
            if cur:
                out.append(cur)
            cur = {"id": int(s.split(":")[1].strip())}
        elif s.startswith("prefab:") and cur is not None:
            g = re.search(r"guid:\s*([0-9a-f]+)", s)
            cur["guid"] = g.group(1) if g else None
        elif s.startswith("displayName:") and cur is not None:
            cur["name"] = json.loads('"' + s.split(":", 1)[1].strip().strip('"') + '"')
    if cur:
        out.append(cur)
    for e in out:
        e["path"] = G2P.get(e.get("guid"), "?")
    return out

def parse_floor(path):
    txt = read(path)
    # 去 // 注释（跳过字符串内）
    lines = []
    for l in txt.split("\n"):
        inq = False
        res = []
        i = 0
        while i < len(l):
            ch = l[i]
            if ch == '"':
                inq = not inq
                res.append(ch)
            elif not inq and ch == "/" and i + 1 < len(l) and l[i + 1] == "/":
                break
            else:
                res.append(ch)
            i += 1
        lines.append("".join(res))
    t = "\n".join(lines)
    t = re.sub(r",(\s*[}\]])", r"\1", t)
    return json.loads(t)

def arr_block(text, field):
    """取 prefab/mono 文档里某字段的数组块，返回 [(x,y), ...]"""
    lines = text.split("\n")
    for i, l in enumerate(lines):
        if l.strip().startswith(field + ":"):
            if "[]" in l:
                return []
            out = []
            j = i + 1
            while j < len(lines) and lines[j].strip().startswith("- {x:"):
                s = lines[j].strip()
                a = re.search(r"x:\s*(-?\d+)", s)
                b = re.search(r"y:\s*(-?\d+)", s)
                out.append((int(a.group(1)), int(b.group(1))))
                j += 1
            return out
    return None

def main():
    scene = read(SCENE)
    objs = {e["id"]: e for e in table("objectPrefabs", scene)}
    print("== objectPrefabs ==")
    for i in sorted(objs):
        print(f"  id {i:<3} {objs[i]['name']:<12} {os.path.basename(objs[i]['path'])}")

    # 找出带 BattleTrigger 的 prefab
    trig_ids = []
    for i, e in objs.items():
        p = e["path"]
        if p == "?" or not os.path.exists(p):
            continue
        t = read(p)
        if "BattleTrigger" in t or "BattleDoorController" in t:
            trig_ids.append(i)
    print("\n== 带 BattleTrigger/BattleDoor 的物体 id ==", trig_ids)

    detail = {}
    for i in trig_ids:
        t = read(objs[i]["path"])
        sp = arr_block(t, "spawnPositions")
        # doorPrefab guid
        m = re.search(r"doorPrefab:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]+)", t)
        door = G2P.get(m.group(2)) if m else None
        rep = arr_block(t, "requiredEnemyPositions")
        detail[i] = {"name": objs[i]["name"], "spawnPositions": sp, "doorPrefab": door,
                     "requiredEnemyPositions": rep, "path": objs[i]["path"]}
        print(f"\n-- obj id {i} {objs[i]['name']} ({os.path.basename(objs[i]['path'])})")
        print(f"   spawnPositions = {sp}")
        print(f"   doorPrefab = {os.path.basename(door) if door else None}")
        if door and os.path.exists(door):
            dt = read(door)
            print(f"   门的 requiredEnemyPositions = {arr_block(dt, 'requiredEnemyPositions')}")

    print("\n== 触发器所在格 & 与其它层的重叠 ==")
    hits = []
    for f in sorted(glob.glob(os.path.join(RES, "floor_*.json"))):
        try:
            m = parse_floor(f)
        except Exception as ex:
            print("  ! 解析失败", os.path.basename(f), ex)
            continue
        ob = m.get("objects") or []
        en = m.get("enemies") or []
        np_ = m.get("npcs") or []
        it = m.get("items") or []
        sp = m.get("player_spawn") or {}
        spawn = (sp.get("x"), sp.get("y"))
        for y, row in enumerate(ob):
            for x, v in enumerate(row):
                if v not in trig_ids:
                    continue
                e = en[y][x] if y < len(en) and x < len(en[y]) else 0
                n = np_[y][x] if y < len(np_) and x < len(np_[y]) else 0
                i2 = it[y][x] if y < len(it) and x < len(it[y]) else 0
                d = detail.get(v, {})
                flag = []
                if e or n:
                    flag.append(f"敌人={e} NPC={n}")
                if i2:
                    flag.append(f"道具={i2}")
                if spawn == (x, y):
                    flag.append("★玩家出生点")
                line = (f"  {os.path.basename(f)} {m.get('name')}: 触发器 id={v} @({x},{y}) "
                        f"生成门@{(d.get('spawnPositions') or ['?'])[0]} "
                        f"{' | '.join(flag) if flag else '(无重叠)'}")
                print(line)
                if e or n:
                    hits.append(line)
    print(f"\n触发器和敌人/NPC 同格：{len(hits)} 处")

if __name__ == "__main__":
    main()
