# -*- coding: utf-8 -*-
"""只读：打印 DataCanvas LeftPanel/RightPanel 内 Image 节点的 sizeDelta，并读 PNG 头拿原始像素尺寸。"""
import io
import os
import re
import struct

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless"
PREFAB = os.path.join(ROOT, "Assets", "Prefabs", "UI", "DataCanvas.prefab")


def png_size(path):
    try:
        with open(path, "rb") as f:
            head = f.read(33)
        if head[:8] != b"\x89PNG\r\n\x1a\n":
            return None
        w, h = struct.unpack(">II", head[16:24])
        bitdepth, colortype = head[24], head[25]
        return w, h, bitdepth, colortype
    except Exception as e:
        return "err:" + str(e)


def main():
    txt = io.open(PREFAB, encoding="utf-8").read()
    docs = re.split(r"(?m)^--- !u!", txt)
    body, cls_of, owner = {}, {}, {}
    for d in docs:
        m = re.match(r"(\d+) &(-?\d+)\n", d)
        if not m:
            continue
        cls_of[m.group(2)] = m.group(1)
        body[m.group(2)] = d
        om = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", d)
        if om:
            owner[m.group(2)] = om.group(1)
    names = {}
    for fid, d in body.items():
        if cls_of[fid] == "1":
            n = re.search(r"m_Name: (.*)", d)
            names[fid] = n.group(1).strip()
    # guid -> abs path
    gi = {}
    for dp, _, fns in os.walk(os.path.join(ROOT, "Assets")):
        for fn in fns:
            if fn.endswith(".meta"):
                h = io.open(os.path.join(dp, fn), encoding="utf-8", errors="ignore").read(200)
                m = re.search(r"^guid: ([0-9a-f]{32})", h, re.M)
                if m:
                    gi[m.group(1)] = os.path.join(dp, fn[:-5])

    for fid, d in body.items():
        if cls_of[fid] != "114" or "m_Sprite:" not in d or "m_text:" in d:
            continue
        go = owner.get(fid)
        rt = [k for k, v in owner.items() if v == go and cls_of[k] == "224"]
        size = "-"
        pos = "-"
        if rt:
            sd = re.search(r"m_SizeDelta: \{x: (-?\S+), y: (-?\S+)\}", body[rt[0]])
            ap = re.search(r"m_AnchoredPosition: \{x: (-?\S+), y: (-?\S+)\}", body[rt[0]])
            if sd:
                size = sd.group(1) + " x " + sd.group(2)
            if ap:
                pos = "(" + ap.group(1) + ", " + ap.group(2) + ")"
        g = re.search(r"m_Sprite: \{fileID: (-?\d+), guid: ([0-9a-f]{32})", d)
        desc = "-"
        if g and g.group(2) != "0000000000000000f000000000000000":
            p = gi.get(g.group(2), "?")
            ps = png_size(p) if p.endswith(".png") else None
            desc = "%s  原始像素=%s" % (os.path.relpath(p, ROOT), ps)
        print("%-20s | size=%-14s | pos=%-16s | %s" % (names.get(go, "?"), size, pos, desc))


if __name__ == "__main__":
    main()
