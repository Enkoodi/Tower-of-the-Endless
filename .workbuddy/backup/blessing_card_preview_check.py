"""
祝福卡「升级预览」算法验证 —— 复刻 BlessingPanel.ApplyLevelUpPreview 的两步逻辑：
  1) 静默把资产文案里的 Lv.1 数字换算到当前层
  2) 再标注「当前层 → 下一层」的变化
并用 FilterUnambiguous 挡住歧义替换（源描述/目标文案里 from 必须各只出现 1 次）。

输入是 asset 真实文案 + 各 Effect 的 GetEffectDescription(level)（照抄 C# 公式）。
改祝福文案或公式后重跑本脚本，能立刻看出哪条祝福的预览失效。
"""

import re, glob, os, codecs

NUM = re.compile(r"\d+(?:\.\d+)?")


def split_numbers(s):
    parts, last = [], 0
    for m in NUM.finditer(s):
        parts.append(s[last:m.start()])
        parts.append(m.group(0))
        last = m.end()
    parts.append(s[last:])
    return parts


def extract_number_changes(cur, nxt):
    changes = []
    a, b = split_numbers(cur), split_numbers(nxt)
    if len(a) != len(b):
        return changes
    for i in range(len(a)):
        if i % 2 == 0:
            if a[i] != b[i]:
                return []
        elif a[i] != b[i]:
            changes.append((a[i], b[i]))
    return changes


def count_number(parts, value):
    return sum(1 for i in range(1, len(parts), 2) if parts[i] == value)


def filter_unambiguous(source, target, changes):
    sn, tn = split_numbers(source), split_numbers(target)
    return [(f, t) for f, t in changes if count_number(sn, f) == 1 and count_number(tn, f) == 1]


def replace_numbers(text, changes, annotate):
    parts = split_numbers(text)
    out = []
    for i, p in enumerate(parts):
        if i % 2 == 0:
            out.append(p)
            continue
        hit = next((c for c in changes if c[0] == p), None)
        if not hit:
            out.append(p)
        else:
            out.append(f"{hit[0]}（{hit[0]}→{hit[1]}）" if annotate else hit[1])
    return "".join(out)


def apply_preview(asset_text, descf, level):
    desc1, now, nxt = descf(1), descf(level), descf(level + 1)
    text = asset_text
    if level > 1 and desc1 != now:
        to_cur = filter_unambiguous(desc1, text, extract_number_changes(desc1, now))
        if to_cur:
            text = replace_numbers(text, to_cur, annotate=False)
    if now == nxt:
        return text, "该祝福升级无数值变化（只改次数/开关）"
    ch = filter_unambiguous(now, text, extract_number_changes(now, nxt))
    if not ch:
        return text, "无内联预览（文案里找不到唯一对应数字）"
    return replace_numbers(text, ch, annotate=True), "ok"


EFFECT_DESC = {
    "BythosBlessing":   lambda L: f"每回合开始时，敌方受到当前生命值 {15 * L}% 的真实伤害",
    "AgapeBlessing":    lambda L: f"每回合损失当前 {3 * L}% HP，攻击力 +{60 * L}",
    "AletheiaBlessing": lambda L: f"战斗开始时魔力充能 ×{1 + 0.25 * L:.2f}",
    "Allotrioi":        lambda L: f"前 {1 + L} 回合减伤 +25%",
    "CharisBlessing":   lambda L: f"战斗胜利后额外获得 {10 * L} 金币",
    "DemiurgeBlessing": lambda L: f"濒死时（HP＜1）恢复至 1000 HP（最多 {L} 次）",
    "GnosisBlessing":   lambda L: f"前 {L} 回合不消耗魔力充能",
    "KabbalahTree":     lambda L: f"抵达 30 层时：HP×2、段数+1、减伤+10%（剩余 {L} 次）",
    "Longinus":         lambda L: f"奇数回合段数 +{L}，偶数回合攻击力 +{100 * L}",
    "SigeBlessing":     lambda L: f"每回合开始对敌人造成当前 HP 的 {5 * L}% 真实伤害",
    "SophiaBlessing":   lambda L: f"HP +1000（永久），每回合恢复上回合受伤量的 {10 + 10 * L}%",
}


def load_asset_texts(root="Assets/Data/Blessing"):
    def dec(v):
        v = v.strip()
        if v.startswith('"') and v.endswith('"'):
            v = v[1:-1]
        return codecs.decode(v, "unicode_escape") if "\\u" in v else v

    src = open("Assets/Scripts/Blessing/BlessingData.cs", encoding="utf-8").read()
    enum = src.split("public enum BlessingID")[1].split("{", 1)[1].split("}")[0]
    ids = [l.split("//")[0].strip().rstrip(",") for l in enum.splitlines() if l.split("//")[0].strip().rstrip(",")]
    id2n = {i: n for i, n in enumerate(ids)}

    out = {}
    for p in glob.glob(os.path.join(root, "[1-4]", "*.asset")):
        idv = None
        for line in open(p, encoding="utf-8"):
            if line.strip().startswith("id:"):
                idv = int(line.split(":")[1])
                break
        for line in open(p, encoding="utf-8"):
            if line.strip().startswith("description:"):
                out[id2n.get(idv)] = dec(line.split(":", 1)[1])
                break
    return out


def main():
    texts = load_asset_texts()
    print(f"{'祝福':<20}{'层':<5}卡片文案")
    print("-" * 112)
    for key, descf in EFFECT_DESC.items():
        asset = texts.get(key, "(未找到 asset)")
        print(f"\n● {key}   [asset] {asset}")
        for lv in (1, 2, 3):
            shown, note = apply_preview(asset, descf, lv)
            tag = "" if note == "ok" else f"    ← {note}"
            print(f"   Lv.{lv:<3}{shown}{tag}")


if __name__ == "__main__":
    main()
