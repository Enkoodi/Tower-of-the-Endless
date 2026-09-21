# -*- coding: utf-8 -*-
"""离线自检：括号配对 + 关键 API 用法 + 遗留调用。csc 被安全策略拦，只能做这些。"""
import os
import re

MAP = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Scripts\Map"
FILES = ["DoorController.cs", "BattleDoorController.cs", "DoorOpenAnimation.cs"]


def strip_code(src):
    """去掉注释与字符串，避免括号误判。"""
    src = re.sub(r"//[^\n]*", "", src)
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    src = re.sub(r'@"(?:[^"]|"")*"', '""', src)
    src = re.sub(r'"(?:\\.|[^"\\])*"', '""', src)
    src = re.sub(r"'(?:\\.|[^'\\])'", "''", src)
    src = re.sub(r"\$@?(?=\")", "", src)
    return src


print("=== 括号配对 ===")
ok = True
for f in FILES:
    p = os.path.join(MAP, f)
    src = strip_code(open(p, encoding="utf-8").read())
    pairs = {"{": "}", "(": ")", "[": "]"}
    stack = []
    for ch in src:
        if ch in pairs:
            stack.append(ch)
        elif ch in pairs.values():
            if not stack or pairs[stack.pop()] != ch:
                ok = False
                print("  %-26s !! 括号不匹配" % f)
                break
    else:
        if stack:
            ok = False
            print("  %-26s !! 有 %d 个未闭合" % (f, len(stack)))
        else:
            print("  %-26s 配对正常" % f)

print("\n=== 关键 API 用法核对 ===")
checks = {
    "Animator.enabled": r"animator\.enabled\s*=",
    "Animator.runtimeAnimatorController": r"runtimeAnimatorController\s*==",
    "Animator.Play(int,int,float)": r"\.Play\(0,\s*0,\s*0f\)",
    "GetCurrentAnimatorStateInfo(0).length": r"GetCurrentAnimatorStateInfo\(0\)\.length",
    "GetCurrentAnimatorStateInfo(0).normalizedTime": r"GetCurrentAnimatorStateInfo\(0\)\.normalizedTime",
    "StartCoroutine": r"StartCoroutine\(",
    "isActiveAndEnabled": r"isActiveAndEnabled",
}
src_all = "".join(open(os.path.join(MAP, f), encoding="utf-8").read() for f in FILES)
for name, pat in checks.items():
    print("  %-46s %s" % (name, "已使用" if re.search(pat, src_all) else "未使用"))

print("\n=== 收尾回调接线（碰撞体应在回调里才撤） ===")
for f in ("DoorController.cs", "BattleDoorController.cs"):
    src = open(os.path.join(MAP, f), encoding="utf-8").read()
    for i, line in enumerate(src.splitlines(), 1):
        if "PlayOpen(" in line or "col.enabled" in line or "RemoveDoor" in line or "HideVisual" in line:
            print("  %-26s %4d  %s" % (f, i, line.strip()))

print("\n=== 开门动画 clip 的 Loop Time（必须全为 0） ===")
ANIM_DIR = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Animations"
for n in ("YellowDoor", "BlueDoor", "RedDoor", "BattleDoor"):
    t = open(os.path.join(ANIM_DIR, n + ".anim"), encoding="utf-8").read()
    loop = re.search(r"m_LoopTime: (\d)", t)
    stop = re.search(r"m_StopTime: ([\d.]+)", t)
    ok_txt = "OK 不循环" if loop and loop.group(1) == "0" else "!! 还在循环 —— 末尾会闪关门图"
    print("  %-12s LoopTime=%s  时长=%ss  %s" % (
        n, loop.group(1) if loop else "?", stop.group(1) if stop else "?", ok_txt))

print("\n=== 遗留的无参 Open() 调用 ===")
for f in FILES:
    src = open(os.path.join(MAP, f), encoding="utf-8").read()
    hits = [m.start() for m in re.finditer(r"\bOpen\(\)", src)]
    decl = len(re.findall(r"private void Open\(\)", src))
    print("  %-26s Open() 出现 %d 次（其中定义 %d 个）" % (f, len(hits), decl))

print("\n=== 全工程搜索对门 Open/OpenAnimation 的调用 ===")
root = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Scripts"
for dp, _dn, fn in os.walk(root):
    for f in fn:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(dp, f)
        for i, line in enumerate(open(p, encoding="utf-8").read().splitlines(), 1):
            if re.search(r"DoorOpenAnimation|TryOpen|\.Open\(", line):
                print("  %s:%d  %s" % (os.path.relpath(p, root), i, line.strip()))
