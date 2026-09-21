# -*- coding: utf-8 -*-
"""
交叉校验：PlayerHUD.cs 里 FindText(panel, "Xxx") 的每个名字，
       必须在 DataCanvas.prefab 对应面板下真实存在（且是直接子物体）。
同时反向检查：面板里有没有「存在但代码没绑」的文本节点。
只读。
"""
import io
import os
import re
import sys

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless"
PREFAB = os.path.join(ROOT, "Assets", "Prefabs", "UI", "DataCanvas.prefab")
SCRIPT = os.path.join(ROOT, "Assets", "Scripts", "UI", "PlayerHUD.cs")


def parse_prefab():
    txt = io.open(PREFAB, encoding="utf-8").read()
    docs = re.split(r"(?m)^--- !u!", txt)
    cls_of, body, owner = {}, {}, {}
    for d in docs:
        m = re.match(r"(\d+) &(-?\d+)\n", d)
        if not m:
            continue
        cls_of[m.group(2)] = m.group(1)
        body[m.group(2)] = d
        om = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", d)
        if om:
            owner[m.group(2)] = om.group(1)

    name_of = {}
    for fid, d in body.items():
        if cls_of[fid] == "1":
            n = re.search(r"m_Name: (.*)", d)
            name_of[fid] = n.group(1).strip()

    children = {}   # 面板名 -> [子物体名...]
    for fid, d in body.items():
        if cls_of[fid] != "224":
            continue
        go = owner.get(fid)
        nm = name_of.get(go)
        if nm not in ("LeftPanel", "RightPanel"):
            continue
        seg = d.split("m_Children:", 1)[1].split("m_Father", 1)[0] if "m_Children:" in d else ""
        kids = []
        for c_rt in re.findall(r"- \{fileID: (-?\d+)\}", seg):
            cgo = owner.get(c_rt)
            kids.append((name_of.get(cgo, "?"), cgo))
        children[nm] = kids

    # 节点名 -> 是否带 TMP
    has_tmp = set()
    for fid, d in body.items():
        if cls_of[fid] == "114" and "m_text:" in d:
            has_tmp.add(owner.get(fid))
    return name_of, children, has_tmp


def main():
    _name_of, children, has_tmp = parse_prefab()
    src = io.open(SCRIPT, encoding="utf-8-sig").read()
    pairs = re.findall(r'FindText\(\s*(\w+)\s*,\s*"([^"]+)"\s*\)', src)

    panel_var = {"leftPanel": "LeftPanel", "rightPanel": "RightPanel"}

    print("代码里声明的绑定（%d 处）" % len(pairs))
    bad = []
    bound = {"LeftPanel": [], "RightPanel": []}
    for var, node in pairs:
        panel = panel_var.get(var)
        if panel is None:
            bad.append("未知面板变量 %s（应为 leftPanel/rightPanel）" % var)
            continue
        names = [k for k, _ in children.get(panel, [])]
        if node not in names:
            bad.append("%s 下不存在节点 %s" % (panel, node))
            continue
        go = dict(children[panel])[node]
        if go not in has_tmp:
            bad.append("%s/%s 上没有 TextMeshProUGUI" % (panel, node))
            continue
        bound[panel].append(node)
        print("  ✓ %-10s %s" % (panel, node))

    print()
    for panel in ("LeftPanel", "RightPanel"):
        all_kids = children.get(panel, [])
        tmp_kids = [k for k, go in all_kids if go in has_tmp]
        unbound = [k for k in tmp_kids if k not in bound[panel]]
        print("%s：子物体 %d 个，其中文本 %d 个；已绑定 %d 个；未绑定的文本 = %s"
              % (panel, len(all_kids), len(tmp_kids), len(bound[panel]),
                 unbound if unbound else "无"))

    print()
    if bad:
        print("!! 发现 %d 处问题：" % len(bad))
        for b in bad:
            print("   -", b)
        return 1
    print("全部通过 ✓")
    return 0


if __name__ == "__main__":
    sys.exit(main())
