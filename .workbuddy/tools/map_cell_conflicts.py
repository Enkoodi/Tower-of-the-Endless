# -*- coding: utf-8 -*-
"""只读普查：五层数据里同一格被放了几样东西（objects / enemies / npcs / items）。
用途：判断「同格叠加」这类隐藏冲突还有多少处，以及哪些格会走 TryMove 的早退分支。"""
import io, json, os, re, glob

RES = r"D:/zzzMyWork/Game/unity/Tower of the Endless/Assets/Resources"

NAMES = {1: "黄门", 2: "蓝门", 3: "红门", 4: "魂门", 5: "魂门", 6: "魂门", 7: "移涌门",
         8: "上楼梯", 9: "下楼梯"}

def parse_floor(path):
    txt = io.open(path, encoding="utf-8").read()
    out = []
    for l in txt.split("\n"):
        inq = False
        buf = []
        i = 0
        while i < len(l):
            ch = l[i]
            if ch == '"':
                inq = not inq
                buf.append(ch)
            elif not inq and ch == "/" and i + 1 < len(l) and l[i + 1] == "/":
                break
            else:
                buf.append(ch)
            i += 1
        out.append("".join(buf))
    t = re.sub(r",(\s*[}\]])", r"\1", "\n".join(out))
    return json.loads(t)

def main():
    rows = []
    for f in sorted(glob.glob(os.path.join(RES, "floor_*.json"))):
        m = parse_floor(f)
        ob, en, np_, it = (m.get(k) or [] for k in ("objects", "enemies", "npcs", "items"))
        for y in range(len(ob)):
            for x in range(len(ob[y])):
                parts = []
                if ob[y][x]:
                    parts.append(f"物体{ob[y][x]}({NAMES.get(ob[y][x], '门/触发器/其它')})")
                if en[y][x]:
                    parts.append(f"敌人{en[y][x]}")
                if np_[y][x]:
                    parts.append(f"NPC{np_[y][x]}")
                if it[y][x]:
                    parts.append(f"道具{it[y][x]}")
                if len(parts) > 1:
                    rows.append((os.path.basename(f), m.get("name"), x, y, parts))
    print(f"同格叠加：共 {len(rows)} 处\n")
    for r in rows:
        print(f"  {r[0]:<16} {r[1]:<8} ({r[2]:>2},{r[3]:>2})  " + " + ".join(r[4]))
    # 分类统计
    from collections import Counter
    c = Counter()
    for r in rows:
        kinds = tuple(sorted(p[:2] for p in r[4]))
        c[kinds] += 1
    print("\n按组合分类：")
    for k, v in c.most_common():
        print(f"  {' + '.join(k):<20} {v} 处")

if __name__ == "__main__":
    main()
