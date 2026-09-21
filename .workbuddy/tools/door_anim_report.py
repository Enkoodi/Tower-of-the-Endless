# -*- coding: utf-8 -*-
"""门 / 战斗门 的动画接线体检（只读）。
用法: python door_anim_report.py
输出: 每个门预制体的 脚本 / Animator / 精灵 / 碰撞体 接线情况，以及动画素材的帧与时长。
"""
import os
import re
import glob

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets"
PREFAB_DIR = os.path.join(ROOT, "Prefabs", "DoorAndStair")
ANIM_DIR = os.path.join(ROOT, "Animations")


def build_guid_map():
    m = {}
    for dp, _dn, fn in os.walk(ROOT):
        for f in fn:
            if not f.endswith(".meta"):
                continue
            try:
                t = open(os.path.join(dp, f), encoding="utf-8", errors="ignore").read(400)
            except OSError:
                continue
            g = re.search(r"^guid: ([0-9a-f]+)", t, re.M)
            if g:
                m[g.group(1)] = os.path.join(dp, f)[:-5]
    return m


GUID2PATH = build_guid_map()


def name_of(guid):
    p = GUID2PATH.get(guid)
    return os.path.basename(p) if p else "<未找到:%s>" % guid


def rel_path(guid):
    p = GUID2PATH.get(guid)
    return os.path.relpath(p, ROOT) if p else "<未找到:%s>" % guid


def block(txt, header):
    """取出 YAML 中某个以 header 开头的块（到下一个 '--- ' 为止）。"""
    pat = re.compile(r"^%s$\n(.*?)(?=^--- |\Z)" % re.escape(header), re.M | re.S)
    m = pat.search(txt)
    return m.group(1) if m else None


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read().replace("\r\n", "\n")


def report_anims():
    print("=== 动画素材 ===")
    for key in ("YellowDoor", "BlueDoor", "RedDoor", "BattleDoor"):
        p = os.path.join(ANIM_DIR, key + ".anim")
        if not os.path.exists(p):
            print("%-12s <缺失>" % key)
            continue
        t = read(p)
        frames = re.findall(r"value: \{fileID: \d+, guid: ([0-9a-f]+), type: 3\}", t)
        loop = re.search(r"m_LoopTime: (\d)", t)
        stop = re.search(r"m_StopTime: ([\d.]+)", t)
        print("%-12s 帧数=%-3d 循环=%s 时长=%ss" % (
            key, len(frames),
            "是" if loop and loop.group(1) == "1" else "否",
            stop.group(1) if stop else "?"))
        print("             首帧=%s  末帧=%s" % (name_of(frames[0]), name_of(frames[-1])))
        for g in sorted({g for g in frames}):
            print("             图集 %s -> %s (%d 帧)" % (
                g[:8], rel_path(g), frames.count(g)))


def report_controllers():
    print("\n=== 动画状态机 ===")
    for key in ("YellowDoor", "BlueDoor", "RedDoor", "BattleDoor"):
        p = os.path.join(ANIM_DIR, key + ".controller")
        if not os.path.exists(p):
            print("%-12s <缺失>" % key)
            continue
        t = read(p)
        params = re.findall(r"m_AnimatorParameters: \[\]", t)
        names = re.findall(r"m_Name: (.+)", t)
        trans = len(re.findall(r"AnimatorStateTransition:", t))
        print("%-12s 参数=%s 状态=%s 过渡数=%d" % (
            key, "无" if params else "有", [n for n in names if n != key], trans))


def report_prefabs():
    print("\n=== 门预制体 ===")
    paths = sorted(glob.glob(os.path.join(PREFAB_DIR, "*.prefab")) +
                   glob.glob(os.path.join(PREFAB_DIR, "BattleDoor", "*.prefab")))
    for p in paths:
        t = read(p)
        rel = os.path.relpath(p, PREFAB_DIR)
        a = block(t, "Animator:")
        ctrl = "无 Animator"
        if a is not None:
            en = re.search(r"m_Enabled: (\d)", a)
            cg = re.search(r"m_Controller: \{fileID: \d+, guid: ([0-9a-f]+)", a)
            ctrl = "Animator(启用=%s, 控制器=%s)" % (
                en.group(1) if en else "?", name_of(cg.group(1)) if cg else "空")
        sp = re.findall(r"^  m_Sprite: \{fileID: \d+, guid: ([0-9a-f]+)", t, re.M)
        scripts = []
        if "DoorController" in "".join(re.findall(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]+)", t)) or True:
            for g in re.findall(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]+)", t):
                scripts.append(name_of(g))
        print("%-34s %s" % (rel, ctrl))
        print("%-34s   脚本=%s" % ("", scripts))
        print("%-34s   精灵=%s" % ("", [name_of(g) for g in sp]))


def report_wiring():
    """核对：预制体静止精灵 是否等于 对应动画的首帧（不等会在开播瞬间跳图）。"""
    print("\n=== 静止精灵 vs 动画首帧 ===")
    expect = {
        "YellowDoor.prefab": "YellowDoor",
        "BlueDoor.prefab": "BlueDoor",
        "RedDoor.prefab": "RedDoor",
    }
    first_frame = {}
    for key in ("YellowDoor", "BlueDoor", "RedDoor", "BattleDoor"):
        t = read(os.path.join(ANIM_DIR, key + ".anim"))
        fr = re.findall(r"value: \{fileID: \d+, guid: ([0-9a-f]+), type: 3\}", t)
        first_frame[key] = fr[0]
    paths = sorted(glob.glob(os.path.join(PREFAB_DIR, "*.prefab")) +
                   glob.glob(os.path.join(PREFAB_DIR, "BattleDoor", "*.prefab")))
    for p in paths:
        t = read(p)
        rel = os.path.relpath(p, PREFAB_DIR)
        a = block(t, "Animator:")
        key = expect.get(os.path.basename(rel))
        if key is None and a is not None:
            cg = re.search(r"m_Controller: \{fileID: \d+, guid: ([0-9a-f]+)", a)
            if cg:
                key = os.path.basename(name_of(cg.group(1))).replace(".controller", "")
        sp = re.findall(r"^  m_Sprite: \{fileID: \d+, guid: ([0-9a-f]+)", t, re.M)
        if not sp:
            continue
        if key is None:
            print("%-34s 静态=%-16s (无动画，跳过)" % (rel, rel_path(sp[0])))
            continue
        same = "一致" if sp[0] == first_frame[key] else "不一致 -> 动画首帧=%s" % rel_path(first_frame[key])
        print("%-34s 静态=%-40s %s" % (rel, rel_path(sp[0]), same))


if __name__ == "__main__":
    report_anims()
    report_controllers()
    report_prefabs()
    report_wiring()
