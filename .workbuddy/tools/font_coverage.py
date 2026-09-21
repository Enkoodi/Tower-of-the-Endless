# -*- coding: utf-8 -*-
"""统计项目实际用到的汉字/字符，并对比 4 个字体资产的覆盖能力。"""
import os, re, glob, sys, json, struct

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets"
FONT_DIR = os.path.join(ROOT, "Font", "smiley-sans-v2.0.1")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from font_audit import ttf_cmap, parse_unicodes

TEXT_EXT = (".cs", ".unity", ".prefab", ".asset", ".json", ".txt")

def read_text(p):
    try:
        return open(p, encoding="utf-8", errors="ignore").read()
    except Exception:
        return ""

def decode_unity_escapes(s):
    return re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), s)

files = []
for ext in TEXT_EXT:
    files += glob.glob(os.path.join(ROOT, "**", "*" + ext), recursive=True)

# 排除第三方 / 内置资源目录
EXCLUDE = ("\\TextMesh Pro\\", "\\Font\\", "\\导入素材\\", "\\Library\\")
files = [f for f in files if not any(e in f for e in EXCLUDE)]

used = {}          # char -> set(files)
for f in files:
    raw = read_text(f)
    raw = decode_unity_escapes(raw)
    for ch in set(raw):
        o = ord(ch)
        if o < 0x20:
            continue
        if 0x4E00 <= o <= 0x9FFF or 0x3000 <= o <= 0x303F or 0xFF00 <= o <= 0xFFEF \
           or 0x3040 <= o <= 0x30FF or 0x2000 <= o <= 0x206F or 0x00A0 <= o <= 0x00FF:
            used.setdefault(ch, set()).add(f)

def analyze(chars, label):
    cjk = sorted(c for c in chars if 0x4E00 <= ord(c) <= 0x9FFF)
    print("\n" + "=" * 78)
    print("%s — 需要渲染的非 ASCII 字符 %d 个（其中汉字 %d 个）" % (label, len(chars), len(cjk)))
    print("汉字: %s ..." % "".join(cjk[:60]))
    return cjk

cjk = analyze(used, "全项目文本（场景+预制体+脚本+数据）")

print("\n" + "-" * 78)
print("各字体资产上已烘焙(静态存在图集里) vs 需运行时动态添加 vs 源字体根本没有")
print("-" * 78)
rows = []
for asset in sorted(glob.glob(os.path.join(FONT_DIR, "*.asset"))):
    txt = read_text(asset)
    baked = set(parse_unicodes(txt))
    m = re.search(r"m_SourceFontFileGUID:\s*([0-9a-f]+)", txt)
    guid = m.group(1) if m else None
    src = None
    for cand in glob.glob(os.path.join(FONT_DIR, "*")):
        if cand.endswith(".meta"):
            continue
        mt = read_text(cand + ".meta")
        if guid and guid in mt:
            src = cand
            break
    cmap, _ = ttf_cmap(src) if src else (set(), None)
    need = [c for c in cjk]
    b = sum(1 for c in need if ord(c) in baked)
    miss_font = [c for c in need if ord(c) not in cmap]
    print("\n【%s】" % os.path.basename(asset))
    print("  源字体: %s" % (os.path.basename(src) if src else "??"))
    print("  源字体总码点 %d" % len(cmap))
    print("  项目汉字中: 已烘焙 %d / 需运行时补 %d / 源字体根本没有 %d"
          % (b, len(need) - b, len(miss_font)))
    if miss_font:
        print("  ★ 永远显示方块的字符(%d):" % len(miss_font))
        print("    " + " ".join(miss_font[:80]))
    rows.append((os.path.basename(asset), len(cmap), len(need), b, len(need) - b, len(miss_font)))

print("\n" + "=" * 78)
print("汇总：字体 / 源字体码点 / 项目需要汉字 / 已烘焙 / 需动态补 / 源字体缺")
for r in rows:
    print("  %-34s %6d  需%4d  已烘焙%4d  需动态%4d  ★缺%3d" % r)
