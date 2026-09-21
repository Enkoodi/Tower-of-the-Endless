# -*- coding: utf-8 -*-
"""把 4 段开门动画的 Loop Time 关掉（幂等）。

原因：这些是「播一次就完」的开门动画，但 clip 上是 m_LoopTime: 1（循环）。
播到末尾会绕回首帧（= 关门图），在门收起前的最后一两帧闪一下关门图。
"""
import os
import shutil

ANIM = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Animations"
BACKUP = r"D:\zzzMyWork\Game\unity\Tower of the Endless\.workbuddy\backup\door_anim_20260921"
NAMES = ["YellowDoor.anim", "BlueDoor.anim", "RedDoor.anim", "BattleDoor.anim"]

os.makedirs(BACKUP, exist_ok=True)

for n in NAMES:
    p = os.path.join(ANIM, n)
    src = open(p, encoding="utf-8", newline="").read()
    cnt = src.count("m_LoopTime: 1")
    if cnt == 0:
        if "m_LoopTime: 0" in src:
            print("%-16s 已经是 m_LoopTime: 0，跳过" % n)
            continue
        print("%-16s !! 没找到 m_LoopTime 字段，人工确认" % n)
        continue
    if cnt != 1:
        print("%-16s !! m_LoopTime: 1 出现 %d 次，中止" % (n, cnt))
        continue

    bak = os.path.join(BACKUP, n + ".bak")
    if not os.path.exists(bak):
        shutil.copy2(p, bak)

    out = src.replace("m_LoopTime: 1", "m_LoopTime: 0")
    open(p, "w", encoding="utf-8", newline="").write(out)
    print("%-16s m_LoopTime: 1 -> 0   备份=%s" % (n, os.path.basename(bak)))

print()
print("=== 回读校验 ===")
for n in NAMES:
    p = os.path.join(ANIM, n)
    t = open(p, encoding="utf-8").read()
    loop = [l.strip() for l in t.splitlines() if "m_LoopTime" in l]
    stop = [l.strip() for l in t.splitlines() if "m_StopTime" in l]
    same_as_bak = None
    bak = os.path.join(BACKUP, n + ".bak")
    if os.path.exists(bak):
        same_as_bak = "只差 LoopTime" if (
            open(bak, encoding="utf-8", newline="").read().replace("m_LoopTime: 1", "m_LoopTime: 0")
            == open(p, encoding="utf-8", newline="").read()) else "!! 还有别的差异"
    print("%-16s %s / %s / %s" % (n, loop, stop, same_as_bak))
