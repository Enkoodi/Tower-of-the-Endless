# -*- coding: utf-8 -*-
"""TMP 字体体检报告：图集占用率 / 字符构成 / 源字体覆盖 / TMP Settings 配置。

用法：
    python font_report.py [字体目录]
默认 = 本项目 Assets/Font/smiley-sans-v2.0.1（同目录可用 .workbuddy/tools/font_audit.py 调用）
"""
import os, re, glob, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from font_audit import (DEFAULT_FONT_DIR, parse_rects, parse_unicodes, font_meta,
                        render_mode_name, source_font_for, ttf_cmap)


def assets_root(font_dir):
    d = os.path.abspath(font_dir)
    while d and os.path.basename(d) != "Assets":
        parent = os.path.dirname(d)
        if parent == d:
            return None
        d = parent
    return d


def read(p):
    try:
        return open(p, encoding="utf-8", errors="ignore").read()
    except Exception:
        return ""


def main(font_dir=DEFAULT_FONT_DIR):
    print("=" * 78)
    print("TMP 字体资产体检：%s" % font_dir)
    print("=" * 78)

    for asset in sorted(glob.glob(os.path.join(font_dir, "*.asset"))):
        text = read(asset)
        meta = font_meta(text)
        used = parse_rects(text, "m_UsedGlyphRects")
        free = parse_rects(text, "m_FreeGlyphRects")
        uni = parse_unicodes(text)
        W = meta["atlas_width"] or 0
        H = meta["atlas_height"] or 0
        total = W * H
        area = sum(w * h for _, _, w, h in used)
        right = max((x + w for x, y, w, h in used), default=0)
        bottom = max((y + h for x, y, w, h in used), default=0)
        avg = (area / len(used)) if used else 0

        print("\n【%s】" % os.path.basename(asset))
        print("  图集 %d×%d  模式=%s(0静态/1动态/2动态OS)  多图集=%s  渲染模式=%s"
              % (W, H, meta["population_mode"], meta["multi_atlas"],
                 render_mode_name(meta["render_mode"])))
        print("  采样 pointSize=%s padding=%s（创建时） / 图集内 padding=%s"
              % (meta["point_size"], meta["creation_padding"], meta["atlas_padding"]))
        print("  已用字形槽 %d 个 / 空闲矩形 %d 个 / 字符表 %d 个"
              % (len(used), len(free), len(uni)))
        if total:
            print("  占位率 %.1f%%（已用 %d px / %d px）" % (area * 100.0 / total, area, total))
            print("  最右 x=%d/%d  最下 y=%d/%d  → %s"
                  % (right, W, bottom, H,
                     "已铺满，新字形放不下" if (right >= W - 4 or bottom >= H - 4) else "仍有整块空间"))
            if avg:
                print("  平均每字形 %.0f px（约 %.0f×%.0f）→ 单张图集理论上限约 %d 个字形"
                      % (avg, avg ** .5, avg ** .5, total / avg))
        if uni:
            cjk = [u for u in uni if 0x4E00 <= u <= 0x9FFF]
            print("  构成：ASCII %d / 汉字 %d / 假名 %d / 其他 %d"
                  % (sum(1 for u in uni if u < 128), len(cjk),
                     sum(1 for u in uni if 0x3040 <= u <= 0x30FF),
                     len(uni) - sum(1 for u in uni if u < 128) - len(cjk)
                     - sum(1 for u in uni if 0x3040 <= u <= 0x30FF)))
        src = source_font_for(asset, text)
        if src:
            cps, _ = ttf_cmap(src)
            if cps:
                print("  源字体 %s（码点 %d / 汉字 %d）"
                      % (os.path.basename(src), len(cps),
                         sum(1 for u in cps if 0x4E00 <= u <= 0x9FFF)))

    print("\n" + "=" * 78)
    print("源字体文件覆盖（cmap）")
    for f in sorted(glob.glob(os.path.join(font_dir, "*.ttf")) +
                    glob.glob(os.path.join(font_dir, "*.otf"))):
        try:
            cps, tag = ttf_cmap(f)
        except Exception as e:
            print("  %-40s 解析失败: %s" % (os.path.basename(f), e))
            continue
        print("  %-38s 总码点 %6d  汉字 %5d  假名 %4d  扩展A %4d"
              % (os.path.basename(f), len(cps),
                 sum(1 for u in cps if 0x4E00 <= u <= 0x9FFF),
                 sum(1 for u in cps if 0x3040 <= u <= 0x30FF),
                 sum(1 for u in cps if 0x3400 <= u <= 0x4DBF)))

    root = assets_root(font_dir)
    ts = os.path.join(root, "TextMesh Pro", "Resources", "TMP Settings.asset") if root else None
    if ts and os.path.exists(ts):
        t = read(ts)
        print("\n" + "=" * 78)
        print("TMP Settings 检查")
        default_id = (re.search(r"m_defaultFontAsset: \{fileID: \d+, guid: ([0-9a-f]+)",
                                t) or [None, "?"])[1]
        name = None
        if root:
            for mt in glob.glob(os.path.join(root, "**", "*.asset.meta"), recursive=True):
                if default_id in read(mt):
                    name = os.path.relpath(mt[:-5], root)
                    break
        print("  默认字体 m_defaultFontAsset = %s" % (name or default_id))
        print("  m_fallbackFontAssets = %s"
              % (re.search(r"m_fallbackFontAssets:\s*(\[\]|.*)", t).group(1)))
        print("  m_missingGlyphCharacter = %s（0 → 用 U+25A1 □）"
              % re.search(r"m_missingGlyphCharacter:\s*(\d+)", t).group(1))
        print("  m_GetFontFeaturesAtRuntime = %s（动态中文场景下是纯损耗）"
              % re.search(r"m_GetFontFeaturesAtRuntime:\s*(\d+)", t).group(1))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_FONT_DIR)
