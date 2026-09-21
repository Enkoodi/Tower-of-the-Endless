# -*- coding: utf-8 -*-
"""生成用于重建/预烘焙字体图集的字符集文件。

每次运行都会和上次的「字符集_清单.json」比对：
  - 有变化 → 明确列出新增/移除的字符，提示需要重做字体图集
  - 无变化 → 直接告诉你不用重做
"""
import os, re, glob, json, datetime

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets"
OUT = r"D:\zzzMyWork\Game\unity\Tower of the Endless\.workbuddy\font"
FONT_DIR = os.path.join(ROOT, "Font", "smiley-sans-v2.0.1")
EXCLUDE = ("\\TextMesh Pro\\", "\\Font\\", "\\导入素材\\", "\\Editor\\")

os.makedirs(OUT, exist_ok=True)

def read(p):
    try:
        return open(p, encoding="utf-8", errors="ignore").read()
    except Exception:
        return ""

def unesc(s):
    return re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), s)

def files(*exts):
    out = []
    for e in exts:
        out += glob.glob(os.path.join(ROOT, "**", "*" + e), recursive=True)
    return [f for f in out if not any(x in f for x in EXCLUDE)]

chars = set()

# 场景 / 预制体里的 TMP 文本
for f in files(".unity", ".prefab"):
    t = read(f)
    for m in re.finditer(r'm_text:\s*(".*?")\s*\n', t):
        chars |= set(unesc(m.group(1)))
    for m in re.finditer(r'm_Name:\s*(.*)', t):
        chars |= set(unesc(m.group(1)))

# 脚本字符串字面量（游戏内文本大多硬编码在代码里）
for f in files(".cs"):
    t = read(f)
    for m in re.finditer(r'"((?:[^"\\\n]|\\.)*)"', t):
        chars |= set(unesc(m.group(1)))

# 数据资产与 JSON 关卡数据
for f in files(".asset", ".json"):
    if f.endswith("SDF.asset"):
        continue
    chars |= set(unesc(read(f)))

# 只要可打印字符
chars = {c for c in chars if c.isprintable() and ord(c) not in (0xFEFF,)}

# 基础必备：ASCII 可见字符 + 常用标点
base = set(chr(i) for i in range(0x20, 0x7F))
base |= set("　、。〃々〈〉《》「」『』【】〔〕〖〗！＂＃＄％＆＇（）＊＋，－．／０１２３４５６７８９：；＜＝＞？＠［＼］＾＿｀｛｜｝～"
            "·—…‘’“”※←→↑↓★☆○●□■△▲▽▼◇◆✕✓")
chars |= base

cjk = sorted(c for c in chars if 0x4E00 <= ord(c) <= 0x9FFF)
other = sorted(c for c in chars if not (0x4E00 <= ord(c) <= 0x9FFF))
ordered = cjk + other

# ---- 与上次清单比对：判断是否需要重做图集 ----
MANIFEST = os.path.join(OUT, "字符集_清单.json")
prev = None
if os.path.exists(MANIFEST):
    try:
        prev = set(json.load(open(MANIFEST, encoding="utf-8"))["chars"])
    except Exception as e:
        print("[警告] 清单读取失败，按首次处理: %s" % e)

now = set(ordered)
print("=" * 60)
if prev is None:
    print("首次生成字符集（没有历史清单可比对）")
else:
    added = sorted(now - prev)
    removed = sorted(prev - now)
    if added or removed:
        print("⚠ 字符集有变化 → 需要重做一次字体图集")
        if added:
            print("  新增 %d 个: %s" % (len(added), "".join(added)))
        if removed:
            print("  移除 %d 个: %s" % (len(removed), "".join(removed)))
        print("  重做后本次清单会被更新，下次再来对照。")
    else:
        print("✔ 字符集与上次完全一致 → 不需要重做图集")
print("=" * 60)

# 1) 每行一个（便于核对）
with open(os.path.join(OUT, "字符集_逐行.txt"), "w", encoding="utf-8") as fh:
    fh.write("\n".join(ordered))

# 2) 单行拼接（可直接粘进 Font Asset Creator 的 Custom Characters）
with open(os.path.join(OUT, "字符集_单行.txt"), "w", encoding="utf-8") as fh:
    fh.write("".join(ordered))

# 3) Unicode 区间形式（可直接粘进 Unicode Range (Hex) 字段）
def ranges(cps):
    rg, s, p = [], None, None
    for c in cps:
        if s is None:
            s = p = c
        elif c == p + 1:
            p = c
        else:
            rg.append((s, p)); s = p = c
    if s is not None:
        rg.append((s, p))
    return rg

with open(os.path.join(OUT, "字符集_UnicodeRange.txt"), "w", encoding="utf-8") as fh:
    for a, b in ranges(sorted(ord(c) for c in ordered)):
        fh.write("%X-%X\n" % (a, b))

print("总字符数: %d  (汉字 %d / 其他 %d)" % (len(ordered), len(cjk), len(other)))
print("输出目录: %s" % OUT)
for n in ("字符集_逐行.txt", "字符集_单行.txt", "字符集_UnicodeRange.txt"):
    p = os.path.join(OUT, n)
    print("  %-24s %d 字节" % (n, os.path.getsize(p)))

# 写入清单（供下次比对）
with open(MANIFEST, "w", encoding="utf-8") as fh:
    json.dump({
        "generated": datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "count": len(ordered),
        "cjk": len(cjk),
        "chars": "".join(ordered),
    }, fh, ensure_ascii=False, indent=1)
print("  %-24s 已更新" % "字符集_清单.json")

# 顺便核算：需要多少字形 + 建议的采样参数
need = len(ordered)
for ps in (90, 72, 64, 56, 48, 40):
    pd = max(2, round(ps / 10))
    side = ps + 2 * pd
    cap = int(2048 / side) ** 2
    print("  pointSize=%2d padding=%d → 单张2048²约容 %4d 字形 %s"
          % (ps, pd, cap, "够" if cap >= need else "不够"))
