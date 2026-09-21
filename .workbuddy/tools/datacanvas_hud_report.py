# -*- coding: utf-8 -*-
"""
只读：打印 DataCanvas.prefab 的 HUD 层级结构。
对 LeftPanel / RightPanel 的每个子物体，输出
  序号 | 名字 | 类型(TMP/Image/其他) | 纵向位置 y | 文本或图标文件名 | fileID
用于核对「第 N 行 = 哪个数值」，以及给节点改名前的现状快照。
不修改任何文件。
"""
import io
import os
import re
import sys

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless"
PREFAB = os.path.join(ROOT, "Assets", "Prefabs", "UI", "DataCanvas.prefab")
ASSETS = os.path.join(ROOT, "Assets")


def build_guid_index():
    """guid -> Assets 下的相对路径（只收图片，省时间）"""
    idx = {}
    for dirpath, _dirnames, filenames in os.walk(ASSETS):
        for fn in filenames:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(dirpath, fn)
            try:
                head = io.open(p, encoding="utf-8", errors="ignore").read(400)
            except Exception:
                continue
            m = re.search(r"^guid: ([0-9a-f]{32})", head, re.M)
            if not m:
                continue
            target = fn[:-5]
            if target.lower().endswith((".png", ".jpg", ".jpeg", ".tga", ".psd")):
                idx[m.group(1)] = os.path.relpath(os.path.join(dirpath, target), ROOT)
    return idx


def main():
    txt = io.open(PREFAB, encoding="utf-8").read()
    # 按文档切分；注意 Unity 文档间用 '--- !u!<class> &<id>' 分隔
    docs = re.split(r"(?m)^--- !u!", txt)
    go_name = {}          # goFileID -> name
    go_comp = {}          # goFileID -> [component fileID]
    comp_owner = {}       # component fileID -> goFileID
    comp_class = {}       # component fileID -> classId
    body = {}             # component fileID -> 文本块

    for d in docs:
        m = re.match(r"(\d+) &(-?\d+)\n", d)
        if not m:
            continue
        cls, fid = m.group(1), m.group(2)
        comp_class[fid] = cls
        body[fid] = d
        om = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", d)
        if om:
            comp_owner[fid] = om.group(1)
        if cls == "1":
            nm = re.search(r"m_Name: (.*)", d)
            go_name[fid] = nm.group(1).strip() if nm else "?"
            go_comp[fid] = re.findall(r"- component: \{fileID: (-?\d+)\}", d)

    rect_of_go = {}
    for fid, d in body.items():
        if comp_class[fid] != "224":
            continue
        owner = comp_owner.get(fid)
        ch = []
        if "m_Children:" in d:
            seg = d.split("m_Children:", 1)[1].split("m_Father", 1)[0]
            ch = re.findall(r"- \{fileID: (-?\d+)\}", seg)
        y = re.search(r"m_AnchoredPosition: \{x: (-?\S+), y: (-?\S+)\}", d)
        rect_of_go[owner] = {
            "rt": fid,
            "children": ch,
            "y": y.group(2) if y else "-",
            "x": y.group(1) if y else "-",
        }

    guid_path = build_guid_index()

    def describe(go_fid):
        """返回 (类型, 详情)"""
        kinds = []
        detail = ""
        for cid in go_comp.get(go_fid, []):
            cls = comp_class.get(cid)
            d = body.get(cid, "")
            if cls == "114" and "m_text:" in d:
                kinds.append("TMP")
                t = re.search(r"m_text: (.*)", d)
                detail = t.group(1).strip() if t else ""
            elif cls == "114":
                kinds.append("Image")
                g = re.search(
                    r"m_Sprite: \{fileID: (-?\d+), guid: ([0-9a-f]{32})", d)
                if g:
                    gid = g.group(2)
                    if gid == "0000000000000000f000000000000000":
                        detail = "内置UI精灵"
                    else:
                        detail = guid_path.get(gid, "guid:" + gid)
                else:
                    detail = "(无 sprite)"
            elif cls == "95":
                kinds.append("Animator")
            elif cls == "222":
                pass
            else:
                kinds.append("?" + cls)
        return "/".join(kinds) if kinds else "-", detail

    # 找 DataCanvas -> LeftPanel / RightPanel
    root = None
    for fid, nm in go_name.items():
        if nm == "DataCanvas" and "1834" not in fid:
            # 有一个同名 GameObject 的 Canvas 组件块，排除误配（按 class=1 已过滤）
            root = fid
    print("PREFAB:", PREFAB)
    print("mtime :", __import__("datetime").datetime.fromtimestamp(
        os.path.getmtime(PREFAB)).strftime("%Y-%m-%d %H:%M:%S"))
    print()

    def dump(panel_name):
        pfid = None
        for fid, nm in go_name.items():
            if nm == panel_name:
                pfid = fid
        if pfid is None:
            print("! 找不到", panel_name)
            return
        r = rect_of_go[pfid]
        print("=" * 78)
        print("%s  (go=%s  rt=%s  y=%s)" % (panel_name, pfid, r["rt"], r["y"]))
        n = 0
        for c_rt in r["children"]:
            cgo = comp_owner.get(c_rt)
            kind, detail = describe(cgo)
            cr = rect_of_go.get(cgo, {})
            n += 1
            print("  %2d | go=%-20s | %-12s | y=%-6s | %-22s | %s"
                  % (n, cgo, kind, cr.get("y", "-"), go_name.get(cgo, "?"), detail))
        print()

    for p in ("LeftPanel", "RightPanel"):
        dump(p)


if __name__ == "__main__":
    main()
