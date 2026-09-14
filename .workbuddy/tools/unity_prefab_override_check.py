# -*- coding: utf-8 -*-
"""
Unity 场景 PrefabInstance 与源预制体资源的差异核对。
识别：真正的场景独有 override / 场景新增或删除的物体与组件 / 僵尸修改条目。
纯文本解析，无第三方依赖。

用法：
  python unity_prefab_diff.py                 # 扫描 Assets/Scenes 下所有场景
  python unity_prefab_diff.py <场景路径>       # 只扫指定场景
"""
import os, re, sys, json, datetime

ROOT = r"D:/zzzMyWork/Game/unity/Tower of the Endless"
OUT = os.path.join(ROOT, ".workbuddy", "reports",
                   "prefab-override-report-%s.md" % datetime.date.today().isoformat())

DOC_RE = re.compile(r'^--- !u!(\d+) &(\d+)(?: stripped)?\s*$')
STRIPPED_RE = re.compile(r'^--- !u!(\d+) &(\d+) stripped\s*$')
PATH_RE = re.compile(r'\[(\d+)\]')


# ---------------------------------------------------------------- YAML 子集解析
def read_text(p):
    for enc in ("utf-8", "utf-8-sig", "latin-1"):
        try:
            with open(p, "r", encoding=enc) as f:
                return f.read()
        except UnicodeDecodeError:
            continue
    with open(p, "r", encoding="utf-8", errors="replace") as f:
        return f.read()


def indent_of(line):
    return len(line) - len(line.lstrip(' '))


def parse_scalar(s):
    s = s.strip()
    if s == "":
        return None
    if s.startswith("{") and s.endswith("}"):
        inner = s[1:-1].strip()
        d = {}
        if inner:
            for part in inner.split(","):
                k, _, v = part.partition(":")
                d[k.strip()] = v.strip()
        return d
    if s.startswith("[") and s.endswith("]"):
        inner = s[1:-1].strip()
        return [parse_scalar(x) for x in inner.split(",")] if inner else []
    return s


def parse_node(lines, i, indent):
    while i < len(lines) and lines[i].strip() == "":
        i += 1
    if i >= len(lines) or indent_of(lines[i]) < indent:
        return None, i
    ind = indent_of(lines[i])

    if lines[i][ind:].startswith("- "):
        lst = []
        while i < len(lines):
            if lines[i].strip() == "":
                i += 1; continue
            if indent_of(lines[i]) != ind or not lines[i][ind:].startswith("- "):
                break
            rest = lines[i][ind + 2:]
            st = rest.strip()
            if ":" in st and not st.startswith("{"):
                sub = [" " * (ind + 2) + rest]
                j = i + 1
                while j < len(lines):
                    if lines[j].strip() == "":
                        sub.append(lines[j]); j += 1; continue
                    if indent_of(lines[j]) > ind:
                        sub.append(lines[j]); j += 1
                    else:
                        break
                obj, _ = parse_node(sub, 0, ind + 2)
                lst.append(obj); i = j
            else:
                lst.append(parse_scalar(rest)); i += 1
        return lst, i

    d = {}
    while i < len(lines):
        if lines[i].strip() == "":
            i += 1; continue
        cur = indent_of(lines[i])
        if cur < ind:
            break
        if cur > ind:
            i += 1; continue
        key, _, val = lines[i][ind:].partition(":")
        key, val = key.strip(), val.strip()
        if val == "":
            j = i + 1
            while j < len(lines) and lines[j].strip() == "":
                j += 1
            child_ind = ind + 1
            # Unity 把序列项写在和父键相同的缩进上
            if j < len(lines) and indent_of(lines[j]) == ind and lines[j][ind:].startswith("- "):
                child_ind = ind
            d[key], i = parse_node(lines, j, child_ind)
            if d[key] is None and False:
                pass
        else:
            d[key] = parse_scalar(val); i += 1
    return d, i


def split_docs(text):
    docs, cur_id, cur_cls, cur_strip, buf = {}, None, None, False, []
    for line in text.splitlines():
        m = STRIPPED_RE.match(line)
        if not m:
            m = DOC_RE.match(line)
            stripped = False
        else:
            stripped = True
        if m:
            if cur_id is not None:
                docs[cur_id] = (cur_cls, cur_strip, buf)
            cur_cls, cur_id, cur_strip, buf = m.group(1), m.group(2), stripped, []
        elif cur_id is not None:
            buf.append(line)
    if cur_id is not None:
        docs[cur_id] = (cur_cls, cur_strip, buf)
    return docs


def unwrap(lines):
    o, _ = parse_node(lines, 0, 0)
    if isinstance(o, dict) and len(o) == 1:
        k = list(o.keys())[0]
        if isinstance(o[k], dict):
            return k, o[k]
    return None, o


def resolve(obj, path):
    norm = PATH_RE.sub(r'.\1', path)
    cur = obj
    for part in norm.split("."):
        if part in ("", "Array", "data"):
            continue
        if isinstance(cur, list):
            if part.isdigit():
                idx = int(part)
                if idx < len(cur):
                    cur = cur[idx]; continue
                return None, False
            if part == "size":
                return str(len(cur)), True
            return None, False
        if isinstance(cur, dict):
            if part in cur:
                cur = cur[part]; continue
            return None, False
        return None, False
    return cur, True


# ---------------------------------------------------------------- 资源索引
def build_guid_map():
    m = {}
    roots = [os.path.join(ROOT, "Assets"), os.path.join(ROOT, "Library", "PackageCache")]
    for root in roots:
        if not os.path.isdir(root):
            continue
        for base, dirs, files in os.walk(root):
            dirs[:] = [d for d in dirs if d != "Library"]
            for fn in files:
                if fn.endswith(".meta"):
                    p = os.path.join(base, fn)
                    try:
                        head = open(p, "r", encoding="utf-8", errors="ignore").read(400)
                    except Exception:
                        continue
                    g = re.search(r'^guid:\s*([0-9a-fA-F]+)', head, re.M)
                    if g:
                        m[g.group(1)] = p[:-5]
    return m


SCRIPT_NAME_CACHE = {}


class Ctx:
    def __init__(self, guids):
        self.guids = guids
        self.prefabs = {}

    def prefab(self, path):
        if path not in self.prefabs:
            self.prefabs[path] = PrefabIndex(path, self.guids)
        return self.prefabs[path]

    def asset_name(self, guid):
        p = self.guids.get(guid)
        if not p:
            return None
        return os.path.basename(p)


class PrefabIndex:
    def __init__(self, path, guids=None):
        self.path = path
        self.guids = guids or {}
        self.text = read_text(path)
        self.docs = split_docs(self.text)
        self.cache = {}
        self.names, self.owner, self.cls, self.script = {}, {}, {}, {}
        for fid, (c, s, l) in self.docs.items():
            root, o = unwrap(l)
            self.cls[fid] = root or ("class%s" % c)
            if isinstance(o, dict):
                if isinstance(o.get("m_Name"), str):
                    self.names[fid] = o["m_Name"]
                go = o.get("m_GameObject")
                if isinstance(go, dict):
                    self.owner[fid] = str(go.get("fileID"))
                sc = o.get("m_Script")
                if isinstance(sc, dict):
                    self.script[fid] = sc.get("guid")

    def obj(self, fid):
        if fid not in self.cache:
            self.cache[fid] = unwrap(self.docs[fid][2])[1] if fid in self.docs else None
        return self.cache[fid]

    def label(self, fid):
        nm = self.names.get(fid)
        if nm is None:
            own = self.owner.get(fid)
            nm = self.names.get(own) if own else None
        kind = self.cls.get(fid, "?")
        g = self.script.get(fid)
        if g:
            p = self.guids.get(g)
            kind = os.path.basename(p) if p else ("脚本 " + g[:8] + "…")
        return nm or "(未命名)", kind


# ---------------------------------------------------------------- 比较
def norm_num(s):
    if s is None:
        return ""
    s = str(s).strip()
    try:
        return "%.6g" % float(s)
    except ValueError:
        return s


def scene_mod_value(m):
    """(kind, value) —— kind ∈ {ref, value, null}"""
    ref, val = m.get("objectReference"), m.get("value")
    if isinstance(ref, dict) and str(ref.get("fileID", "0")).strip() not in ("", "0"):
        return "ref", ref
    if val is not None and str(val).strip() != "":
        return "value", str(val).strip()
    return "null", None


def prefab_value(got):
    if isinstance(got, dict):
        fid = str(got.get("fileID", "0")).strip()
        return ("null", None) if fid in ("", "0") else ("ref", got)
    if got is None:
        return "null", None
    return "value", str(got).strip()


def compare(sk, sv, pk, pv, stripped_map, ctx, prefab_guid, idx):
    """返回 (是否相同, 场景显示值, 预制体显示值)"""
    def disp(d, is_scene):
        fid = str(d.get("fileID", "0"))
        g = str(d.get("guid", "")).strip()
        if fid in ("", "0"):
            return "None"
        if g and g != prefab_guid:
            nm = ctx.asset_name(g)
            return "%s(fileID %s)" % (nm or g[:8] + "…", fid)
        src = stripped_map.get(fid, fid) if is_scene else fid
        nm, kind = idx.label(src)
        return "预制体内「%s」(%s)" % (nm, kind)

    s_disp = s_p_disp = ""
    if sk == "null":
        s_disp = "None"
    elif sk == "value":
        s_disp = sv
    else:
        s_disp = disp(sv, True)

    if pk == "null":
        p_disp = "None"
    elif pk == "value":
        p_disp = pv
    else:
        p_disp = disp(pv, False)

    if sk == "value" and pk == "value":
        return norm_num(sv) == norm_num(pv), s_disp, p_disp
    if sk == "null" and pk == "null":
        return True, s_disp, p_disp
    if sk == "ref" and pk == "ref":
        sf = str(sv.get("fileID", "0"))
        sg = str(sv.get("guid", "")).strip()
        pf = str(pv.get("fileID", "0"))
        pg = str(pv.get("guid", "")).strip()
        if not sg:
            # 无 guid → 可能是实例内部 stripped 引用，映射回源 fileID
            mapped = stripped_map.get(sf)
            if mapped:
                return mapped == pf, s_disp, p_disp
        if sg and sg != pg:
            return False, s_disp, p_disp
        return sf == pf, s_disp, p_disp
    return False, s_disp, p_disp


def main():
    guids = build_guid_map()
    ctx = Ctx(guids)
    if len(sys.argv) > 1:
        scenes = [sys.argv[1]]
    else:
        sd = os.path.join(ROOT, "Assets/Scenes")
        scenes = sorted(os.path.join(sd, f) for f in os.listdir(sd) if f.endswith(".unity"))

    lines = []
    def w(s=""):
        lines.append(s)

    w("# 场景 ↔ 预制体 差异核对报告")
    w("")
    w("生成时间：%s" % datetime.datetime.now().strftime("%Y-%m-%d %H:%M"))
    w("")
    w("> 比对方式：解析场景里的 `PrefabInstance.m_Modifications`，逐条回源预制体资源取值对比。")
    w("> 只有「场景值 ≠ 预制体资源值」的才算真正没同步的改动。")
    w("")

    summary = []

    for scene in scenes:
        docs = split_docs(read_text(scene))
        insts = [f for f, v in docs.items() if v[0] == "1001"]
        if not insts:
            continue
        # 场景内 stripped 占位对象：实例内部 fileID → 源预制体 fileID
        stripped_map = {}
        for fid, (cls, strip, l) in docs.items():
            if not strip:
                continue
            o = unwrap(l)[1]
            if isinstance(o, dict):
                cso = o.get("m_CorrespondingSourceObject")
                if isinstance(cso, dict):
                    stripped_map[fid] = str(cso.get("fileID", "0"))

        w("")
        w("---")
        w("")
        w("## 场景 `%s`（%d 个预制体实例）" % (os.path.relpath(scene, ROOT).replace("\\", "/"), len(insts)))
        w("")

        for inst in sorted(insts):
            pi = unwrap(docs[inst][2])[1] or {}
            pi = pi if isinstance(pi, dict) else {}
            src = pi.get("m_SourcePrefab") or {}
            sg = src.get("guid")
            spath = guids.get(sg)
            relname = os.path.relpath(spath, ROOT).replace("\\", "/") if spath else "? (guid %s)" % sg

            if not spath:
                w("### 实例 `&%s` → 源预制体解析失败（guid %s）" % (inst, sg))
                continue

            idx = ctx.prefab(spath)
            mw = pi.get("m_Modification") or {}
            mods = mw.get("m_Modifications") or pi.get("m_Modifications") or []
            rm_comp = mw.get("m_RemovedComponents") or []
            rm_go = mw.get("m_RemovedGameObjects") or []
            add_go = mw.get("m_AddedGameObjects") or []
            add_comp = mw.get("m_AddedComponents") or []

            diffs, stale, same = [], [], 0
            for m in mods:
                if not isinstance(m, dict):
                    continue
                tgt = m.get("target") or {}
                tfid = str(tgt.get("fileID"))
                pp = m.get("propertyPath") or ""
                po = idx.obj(tfid)
                got, ok = (None, False) if po is None else resolve(po, pp)
                if not ok:
                    stale.append((tfid, pp, m)); continue
                sk, sv = scene_mod_value(m)
                pk, pv = prefab_value(got)
                eq, sd, pd = compare(sk, sv, pk, pv, stripped_map, ctx, sg, idx)
                if eq:
                    same += 1
                else:
                    diffs.append((tfid, pp, sd, pd))

            w("### 实例 `&%s` ← `%s`" % (inst, relname))
            w("")
            w("| 项 | 数量 |")
            w("|---|---|")
            w("| 修改条目总数 | %d |" % len(mods))
            w("| 与预制体一致（无需处理） | %d |" % same)
            w("| **场景独有差异（未同步）** | **%d** |" % len(diffs))
            w("| 僵尸条目（字段已不存在，无效） | %d |" % len(stale))
            w("| 场景删除的组件 | %d |" % len(rm_comp))
            w("")

            summary.append((os.path.basename(scene), inst, relname, len(diffs),
                            len(rm_comp), len(add_go) + len(add_comp), len(stale)))

            if diffs:
                w("#### 只在场景里改过 —— 共 %d 项" % len(diffs))
                w("")
                w("| 对象 | 组件/脚本 | 属性 | 场景里的值 | 预制体里的值 |")
                w("|---|---|---|---|---|")
                for tfid, pp, sd, pd in diffs:
                    nm, kind = idx.label(tfid)
                    w("| %s | %s | `%s` | %s | %s |" % (nm, kind, pp, sd or "(空)", pd or "(空)"))
                w("")

            if rm_comp:
                w("#### 场景里删除了、预制体里还留着的组件")
                w("")
                for c in rm_comp:
                    fid = str((c or {}).get("fileID"))
                    nm, kind = idx.label(fid)
                    w("- %s · %s" % (nm, kind))
                w("")

            if rm_go:
                w("#### 场景里删除的子物体")
                w("")
                for g in rm_go:
                    w("- fileID %s" % (g or {}).get("fileID"))
                w("")

            if add_go or add_comp:
                w("#### 场景实例内新增的物体/组件")
                w("")
                w("```")
                w(json.dumps(add_go + add_comp, ensure_ascii=False, indent=1)[:2000])
                w("```")
                w("")

            if stale:
                w("#### 僵尸修改条目（预制体里已找不到对应字段，可忽略）")
                w("")
                for tfid, pp, m in stale:
                    nm, kind = idx.label(tfid)
                    sk, sv = scene_mod_value(m)
                    w("- %s · %s → `%s`" % (nm, kind, pp))
                w("")

    out = "\n".join(lines)

    # 顶部插入汇总表
    sm = ["", "## 总览", "",
          "| 场景 | 实例 | 源预制体 | 未同步差异 | 删除的组件 | 新增物体/组件 | 僵尸条目 |",
          "|---|---|---|---|---|---|---|"]
    for row in summary:
        sm.append("| %s | `&%s` | `%s` | **%d** | %d | %d | %d |" % row)
    sm.append("")
    head = out.split("\n")
    out = "\n".join(head[:8] + sm + head[8:])

    out += """

---

## 怎么把场景里的改动同步回预制体

### A. 整个实例一次性同步（最常用）
1. 在 Hierarchy 里选中场景中的预制体实例（例如 `BattleCanvas`）
2. Inspector 面板**最顶部那一行**（名字 + Revert/Apply 按钮那一行）右侧有个 **Overrides** 下拉
3. 展开后底部有两个按钮：
   - **Apply All** → 把该实例上所有差异推到预制体资源（场景和预制体都变成新值）
   - **Revert All** → 反过来，丢弃场景改动，实例恢复成预制体的样子

### B. 只同步某一项
- Overrides 下拉里每一项右侧都有单独的 **Apply** / **Revert**
- 或者在 Inspector 里**右键该字段** → `Apply to Prefab 'xxx'`
- 两种方式都只作用于这一项，不会动别的差异

### C. 物体/组件的增删
Overrides 下拉里会分组成 `Added Components` / `Removed Components` / `Added GameObjects` / `Removed GameObjects`，同样支持逐项 Apply / Revert。
**注意**：BattleCanvas 删掉的那 3 个 LayoutGroup 就在 `Removed Components` 组里。

### D. 反过来改（推荐的长期做法）
双击 Project 里的 `.prefab` 进入预制体编辑模式直接改。只要场景实例没有 override 该属性，所有实例会自动跟着变，也就不会再积累「只在场景里改过」的差异。

### Apply 之前的两条注意事项
1. **LayoutGroup 的删除和 RectTransform 坐标改动是配套的**。要么一起 Apply，要么一起 Revert。只 Apply 坐标却不 Apply 组件删除的话，LayoutGroup 会把手动坐标重新覆盖掉。
2. Apply All 会影响所有引用该预制体的地方。已确认本项目这 5 个 UI Canvas 预制体**只被 `Game.unity` 引用**，其它三个场景都没有引用，所以 Apply 是安全的。

---

## 重点解读

**1. `BattleCanvas` 的 42 项是最大一块，看起来是有意为之的整体改版**
- 布局重排：`Center` / `LeftSide` / `RightSide` / `EnemyImage` / `EnemyStatsText` / `PlayerStatsText` / `ContentArea ` 的锚点与坐标
- 删除了 3 个 LayoutGroup（`ContentArea ` 的 HorizontalLayoutGroup，`LeftSide` / `RightSide` 的 VerticalLayoutGroup）——与上面的坐标改动是配套动作
- `Round` 改名成 `Turn`
- `BattleWindow.turnText` 从空绑定到了文本组件
- 4 处 `m_RaycastTarget` 由 1 改成 0（关掉了挡点击的底板射线）

**2. 玩家头像攻击坐标的问题：场景里改对了，预制体没跟上**
`PlayerImage.m_AnchoredPosition.x`：场景 = `0`，预制体 = `175`。这正是之前分析 `PlayerUI_Attack` 曲线（x 走 0→125→0）时提到的坐标不匹配。场景已改成 0，预制体里还是 175 —— 必须同步的一项。

**3. TMP 字体差异（5 个 Canvas 都有，数量最多）**
所有含 TextMeshPro 文本的实例，场景里是 `SmileySans-Oblique SDF`，预制体里是 `culiufangxin24x SDF`。
已核对：TMP 的默认字体是 `LiberationSans SDF`，这两个都不是 —— 所以**不是 TMP 自动写入的噪音，而是一次真实的人为改动**，需要你决定统一成哪一个，然后 Apply 或 Revert。

**4. 僵尸条目可以无视**
`DialogueCanvas` 上有 4 条 `choice1Label / choice2Label / choice1Button / choice2Button` 修改记录，但脚本里这些字段已改名成 `exitButtonLabel / actionButtonLabel / exitButton / actionButton`。这些条目在预制体里找不到对应字段，不产生任何效果，Unity 下次保存场景时会自动清掉。

**5. `DataCanvas` 完全同步，0 项差异** —— 可以作为「干净」的参照。
"""

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8").write(out)
    print(out)
    print("\n[报告] " + OUT)


if __name__ == "__main__":
    main()
