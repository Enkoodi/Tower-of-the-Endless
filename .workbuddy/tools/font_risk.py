# -*- coding: utf-8 -*-
"""按来源拆解项目用字，并对照两个实际在用的字体图集容量。"""
import os, re, glob, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from font_audit import parse_unicodes, ttf_cmap

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets"
FONT_DIR = os.path.join(ROOT, "Font", "smiley-sans-v2.0.1")
EXCLUDE = ("\\TextMesh Pro\\", "\\Font\\", "\\导入素材\\")

def read(p):
    try:
        return open(p, encoding="utf-8", errors="ignore").read()
    except Exception:
        return ""

def unesc(s):
    return re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), s)

def relevant(s):
    return {c for c in s if 0x4E00 <= ord(c) <= 0x9FFF}

def files(ext):
    out = glob.glob(os.path.join(ROOT, "**", "*" + ext), recursive=True)
    return [f for f in out if not any(e in f for e in EXCLUDE)]

src = {}

# 1) 场景 + 预制体里的 TMP 文本
ui = set()
for f in files(".unity") + files(".prefab"):
    t = read(f)
    for m in re.finditer(r'\n\s*m_text:\s*("(?:[^"\\]|\\.)*")', t):
        pass
    # 直接对所有 m_text 行取值（含转义）
    for m in re.finditer(r'm_text:\s*(".*?")\s*\n', t):
        ui |= relevant(unesc(m.group(1)))
src["场景/预制体 TMP 文本"] = ui

# 2) 脚本里双引号字符串字面量
cs = set()
cs_all = set()
for f in files(".cs"):
    t = read(f)
    cs_all |= relevant(t)
    for m in re.finditer(r'"((?:[^"\\\n]|\\.)*)"', t):
        cs |= relevant(m.group(1))
src["脚本字符串字面量"] = cs
src["脚本(含注释)"] = cs_all

# 3) 数据文件（.asset / .json / .txt，Data 与 Resources）
dt = set()
for ext in (".asset", ".json", ".txt"):
    for f in files(ext):
        if f.endswith("SDF.asset"):
            continue
        dt |= relevant(read(f))
src["数据资产(.asset/.json/.txt)"] = dt

print("按来源统计汉字数")
print("-" * 60)
for k, v in src.items():
    print("  %-26s %5d" % (k, len(v)))

runtime = src["场景/预制体 TMP 文本"] | src["脚本字符串字面量"] | src["数据资产(.asset/.json/.txt)"]
print("-" * 60)
print("  运行时会显示的字（并集）      %5d" % len(runtime))

# 4) 对照字体
targets = {
    "SmileySans-Oblique SDF.asset": "b99276c05bd6b354dab16d12b9cf6e03",
    "culiufangxin24x SDF.asset": "656e1fb00c3557a48a4de08974fe45d7",
}
print("\n" + "=" * 60)
print("图集容量核算")
print("=" * 60)
CAP = 2048.0
for asset, guid in targets.items():
    p = os.path.join(FONT_DIR, asset)
    t = read(p)
    baked = {chr(u) for u in parse_unicodes(t)}
    used_area = 0
    rects = re.findall(r"- m_X:\s*(\d+)\s*\n\s*m_Y:\s*(\d+)\s*\n\s*m_Width:\s*(\d+)\s*\n\s*m_Height:\s*(\d+)",
                       re.search(r"m_UsedGlyphRects:\n(.*?)\n  m_FreeGlyphRects:", t, re.S).group(1))
    n = len(rects)
    for x, y, w, h in rects:
        used_area += int(w) * int(h)
    avg = used_area / n
    free = 2048 * 2048 - used_area
    print("\n【%s】" % asset)
    print("  已烘焙字形 %d 个，平均每字占 %.0f 像素(约 %.0f×%.0f)" % (n, avg, avg ** .5, avg ** .5))
    print("  图集 2048×2048 = %d 像素，已用 %.1f%%" % (2048 * 2048, used_area / (2048 * 2048) * 100))
    print("  剩余空间只够再塞约 %d 个字形" % (free / avg))
    print("  运行时要显示 %d 个汉字 → 需要 %.1f 倍当前图集容量" % (len(runtime), len(runtime) / (n + free / avg)))
    miss = sorted(runtime - baked)
    print("  当前【未烘焙】的汉字 %d 个，举例: %s" % (len(miss), "".join(miss[:50])))
