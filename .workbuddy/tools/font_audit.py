# -*- coding: utf-8 -*-
"""TMP 字体资产解析库（只读，import 时无副作用）。

供 font_report.py / font_coverage.py / font_risk.py / font_charset.py 共用。
命令行直接运行本文件时等价于跑 font_report.py。

用法：
    python font_audit.py [字体目录]
默认 = 本项目 Assets/Font/smiley-sans-v2.0.1
"""
import os, re, struct, sys

DEFAULT_FONT_DIR = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Font\smiley-sans-v2.0.1"
FONT_DIR = DEFAULT_FONT_DIR          # 兼容旧引用


def parse_rects(text, key):
    """取出 m_UsedGlyphRects / m_FreeGlyphRects 的 (x, y, w, h) 列表。"""
    m = re.search(r"\n  " + key + r":\n(.*?)(?=\n  m_\w+:|\n  \w)", text, re.S)
    if not m:
        return []
    items = re.findall(
        r"- m_X:\s*(-?\d+)\s*\n\s*m_Y:\s*(-?\d+)\s*\n\s*m_Width:\s*(\d+)\s*\n\s*m_Height:\s*(\d+)",
        m.group(1))
    return [tuple(int(v) for v in it) for it in items]


def parse_unicodes(text):
    """字符表里的 m_Unicode 码点列表（返回 int，不是字符）。"""
    m = re.search(r"\n  m_CharacterTable:\n(.*?)\n  m_AtlasTextureIndex:", text, re.S)
    if not m:
        return []
    return [int(u) for u in re.findall(r"m_Unicode:\s*(\d+)", m.group(1))]


def font_meta(text):
    """汇总字体资产的关键字段。"""
    def g(pat):
        m = re.search(pat, text)
        return int(m.group(1)) if m else None

    return {
        "atlas_width": g(r"m_AtlasWidth:\s*(\d+)"),
        "atlas_height": g(r"m_AtlasHeight:\s*(\d+)"),
        "atlas_padding": g(r"m_AtlasPadding:\s*(\d+)"),
        "population_mode": g(r"m_AtlasPopulationMode:\s*(\d+)"),   # 0静态 1动态 2动态OS
        "multi_atlas": g(r"m_IsMultiAtlasTexturesEnabled:\s*(\d+)"),
        "render_mode": g(r"m_AtlasRenderMode:\s*(\d+)"),
        "point_size": g(r"pointSize:\s*(\d+)"),
        "creation_padding": g(r"padding:\s*(\d+)"),
        "clear_on_build": g(r"m_ClearDynamicDataOnBuild:\s*(\d+)"),
    }


def render_mode_name(v):
    """GlyphRenderMode 数值 → 可读名（Unity TextCore 枚举）。"""
    return {
        4165: "SMOOTH_HINTED (位图)",
        4164: "SMOOTH (位图)",
        4117: "RASTER_HINTED (位图)",
        4116: "RASTER (位图)",
        4169: "SDFAA_HINTED (SDF)",
        4168: "SDFAA (SDF)",
    }.get(v, "未知(%s)" % v)


def source_font_for(asset_path, text):
    """按 m_SourceFontFileGUID 反查同目录下的源字体文件路径。"""
    m = re.search(r"m_SourceFontFileGUID:\s*([0-9a-f]+)", text)
    if not m:
        return None
    guid = m.group(1)
    d = os.path.dirname(asset_path)
    if not os.path.isdir(d):
        return None
    for cand in os.listdir(d):
        if cand.endswith(".meta") or not cand.lower().endswith((".ttf", ".otf")):
            continue
        try:
            if guid in open(os.path.join(d, cand + ".meta"), encoding="utf-8").read():
                return os.path.join(d, cand)
        except Exception:
            continue
    return None


def ttf_cmap(path):
    """解析 ttf/otf 的 cmap，返回 (支持的码点集合, 文件签名)。"""
    with open(path, "rb") as f:
        data = f.read()
    tag = data[:4]
    num_tables = struct.unpack(">H", data[4:6])[0]
    tables = {}
    for i in range(num_tables):
        off = 12 + i * 16
        name = data[off:off + 4].decode("latin-1")
        toff, tlen = struct.unpack(">II", data[off + 8:off + 16])
        tables[name] = (toff, tlen)
    if "cmap" not in tables:
        return set(), tag
    coff = tables["cmap"][0]
    n_sub = struct.unpack(">H", data[coff + 2:coff + 4])[0]
    best = None
    for i in range(n_sub):
        if coff + 12 + i * 8 > len(data):
            break
        pid, eid, sub_off = struct.unpack(">HHI", data[coff + 4 + i * 8: coff + 12 + i * 8])
        sub = coff + sub_off
        if sub + 2 > len(data):
            continue
        fmt = struct.unpack(">H", data[sub:sub + 2])[0]
        if fmt == 4:
            score = 3 if (pid, eid) in ((3, 1), (0, 3)) else (2 if pid == 3 else 1)
        elif fmt == 12:
            score = 5
        elif fmt == 0:
            score = 0
        else:
            continue
        if best is None or score > best[0]:
            best = (score, fmt, sub)
    if best is None:
        return set(), tag
    _, fmt, sub = best
    cps = set()
    if fmt == 4:
        segx2 = struct.unpack(">H", data[sub + 6:sub + 8])[0]
        seg = segx2 // 2
        ends = struct.unpack(">%dH" % seg, data[sub + 14: sub + 14 + segx2])
        starts = struct.unpack(">%dH" % seg, data[sub + 16 + segx2: sub + 16 + segx2 * 2])
        idelta = struct.unpack(">%dh" % seg, data[sub + 16 + segx2 * 2: sub + 16 + segx2 * 3])
        rangeoff_pos = sub + 16 + segx2 * 3
        rangeoffs = struct.unpack(">%dH" % seg, data[rangeoff_pos: rangeoff_pos + segx2])
        for i in range(seg):
            for c in range(starts[i], min(ends[i], 0xFFFF) + 1):
                if c == 0xFFFF:
                    continue
                if rangeoffs[i] == 0:
                    g = (c + idelta[i]) & 0xFFFF
                else:
                    gp = rangeoff_pos + i * 2 + rangeoffs[i] + (c - starts[i]) * 2
                    if gp + 2 > len(data):
                        continue
                    g = struct.unpack(">H", data[gp:gp + 2])[0]
                    if g:
                        g = (g + idelta[i]) & 0xFFFF
                if g:
                    cps.add(c)
    elif fmt == 12:
        n_groups = struct.unpack(">I", data[sub + 12:sub + 16])[0]
        for i in range(n_groups):
            s, e, _ = struct.unpack(">III", data[sub + 16 + i * 12: sub + 28 + i * 12])
            cps.update(range(s, e + 1))
    return cps, tag


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from font_report import main
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_FONT_DIR)
