# -*- coding: utf-8 -*-
"""门动画细节 + 引用关系（只读）。"""
import os
import re
import glob

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets"
ANIM = os.path.join(ROOT, "Animations")


def read(p):
    with open(p, encoding="utf-8") as fh:
        return fh.read().replace("\r\n", "\n")


print("=== 动画关键帧（精确） ===")
for key in ("YellowDoor", "BlueDoor", "RedDoor", "BattleDoor"):
    t = read(os.path.join(ANIM, key + ".anim"))
    pairs = re.findall(r"- time: ([\d.]+)\n\s+value: \{fileID: (-?\d+), guid: ([0-9a-f]+)", t)
    sheets = {}
    for tm, fid, g in pairs:
        sheets.setdefault(g, []).append(fid)
    print("%-11s 关键帧=%d 时长=%s 循环=%s" % (
        key, len(pairs),
        re.search(r"m_StopTime: ([\d.]+)", t).group(1),
        re.search(r"m_LoopTime: (\d)", t).group(1)))
    for g, ids in sheets.items():
        print("            表格 %s : %d 帧  fileID=%s" % (g[:8], len(ids), ids[:10]))

print("\n=== 图集目录 ===")
for sub in ("LCG", "GROUND"):
    d = os.path.join(ROOT, "Sprites", "魔塔", sub)
    if not os.path.isdir(d):
        print("  %s <无此目录>" % sub)
        continue
    for f in sorted(os.listdir(d)):
        if f.lower().endswith(".png"):
            m = d + os.sep + f + ".meta"
            mode = "?"
            cnt = 0
            if os.path.exists(m):
                mt = read(m)
                sm = re.search(r"spriteMode: (\d)", mt)
                mode = sm.group(1) if sm else "?"
                cnt = len(re.findall(r"internalID: \d+", mt)) - 1
            size = os.path.getsize(os.path.join(d, f))
            print("  %-6s %-10s spriteMode=%s 子图=%d  %d bytes" % (sub, f, mode, cnt, size))

print("\n=== 战斗门预制体引用 ===")
gmap = {}
for dp, _dn, fn in os.walk(ROOT):
    for f in fn:
        if f.endswith(".meta"):
            t = open(os.path.join(dp, f), encoding="utf-8", errors="ignore").read(300)
            g = re.search(r"^guid: ([0-9a-f]+)", t, re.M)
            if g:
                gmap[os.path.join(dp, f)[:-5]] = g.group(1)

doors = sorted(glob.glob(os.path.join(ROOT, "Prefabs", "DoorAndStair", "BattleDoor", "*BattleDoor.prefab")))
guids = {gmap[p]: os.path.basename(p) for p in doors if p in gmap}
for g, n in sorted(guids.items(), key=lambda kv: kv[1]):
    hits = []
    for dp, _dn, fn in os.walk(ROOT):
        for f in fn:
            if not f.endswith((".asset", ".prefab", ".unity", ".json")):
                continue
            fp = os.path.join(dp, f)
            try:
                if g in open(fp, encoding="utf-8", errors="ignore").read():
                    hits.append(os.path.relpath(fp, ROOT))
            except OSError:
                pass
    me = os.path.relpath([p for p in doors if gmap.get(p) == g][0], ROOT)
    print("%-24s guid=%s" % (n, g[:8]))
    print("   被引用: %s" % (sorted(set(h for h in hits if h != me)) or "无"))
