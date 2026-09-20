"""一次性脚本：① 商店去掉逆推 ② 系数换算统一改成「向上取整（偏向玩家）」。

背景：
  · 商店原来用「逆推」抵消 AddAttack/AddDefense/AddHP 内部的乘系数，
    导致两次整数截断，最坏情况是「花钱白买」（攻击 10 + 系数 150% 买 +10% → 到手 0）。
  · 属性道具 / 祝福两条路径本来就会被系数放大，商店是唯一异类。
  · 取整口径已于 2026-09-20 定为「一律偏向玩家」→ 乘系数处一律 Mathf.CeilToInt。

不改的东西：各处 `* (100 - 减伤) / 100` 保持向下截断（用户明确要求）。
"""

import io
import sys

BASE = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Scripts"

# ---------------- PlayerData.cs：8 处系数换算改成 CeilToInt ----------------
PD = BASE + r"\Player\PlayerData.cs"

PD_JOBS = [
    ("int actualHeal = amount * hpMultiplier / 100;",
     "int actualHeal = Mathf.CeilToInt(amount * hpMultiplier / 100f);"),
    ("int actualGain = amount * attackMultiplier / 100;",
     "int actualGain = Mathf.CeilToInt(amount * attackMultiplier / 100f);"),
    ("int actualGain = amount * defenseMultiplier / 100;",
     "int actualGain = Mathf.CeilToInt(amount * defenseMultiplier / 100f);"),
    ("int actualGold = amount * goldMultiplier / 100;",
     "int actualGold = Mathf.CeilToInt(amount * goldMultiplier / 100f);"),
    ("int actualGain = amount * hpMultiplier / 100;",
     "int actualGain = Mathf.CeilToInt(amount * hpMultiplier / 100f);"),
    ("attack        += b.attackBonus * attackMultiplier / 100;",
     "attack        += Mathf.CeilToInt(b.attackBonus * attackMultiplier / 100f);"),
    ("defense       += b.defenseBonus * defenseMultiplier / 100;",
     "defense       += Mathf.CeilToInt(b.defenseBonus * defenseMultiplier / 100f);"),
    ("int hpChange = b.hpBonus * hpMultiplier / 100;",
     "int hpChange = Mathf.CeilToInt(b.hpBonus * hpMultiplier / 100f);"),
]

# ---------------- NPCInteractionUI.cs：三个购买方法 ----------------
SHOP = BASE + r"\NPC\NPCInteractionUI.cs"

SHOP_JOBS = [
    # --- HP ---
    ('''        int rawGain = currentPlayer.HP * currentNPC.HpPercent / 100;
        // 逆推换算后通过 AddHP（内部会乘以 hpMultiplier / 100，正好抵消）
        int inversed = rawGain * 100 / Mathf.Max(1, currentPlayer.HPMultiplier);
        currentPlayer.AddHP(inversed);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 生命值 +{currentNPC.HpPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");''',
     '''        // 购买量 = 当前生命值 × 购买百分比（向上取整，偏向玩家）。
        // 直接交给 AddHP，内部那次 × hpMultiplier 会把它放大 ——
        // 与属性道具、祝福两条路径口径一致（它们的 HP/攻/防获取同样会被系数放大）。
        // 别再写「先乘系数再除回去」的逆推：那个反解会被整数截断吃掉，最坏情况是花钱白买。
        int rawGain = Mathf.CeilToInt(currentPlayer.HP * currentNPC.HpPercent / 100f);
        currentPlayer.AddHP(rawGain);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 生命值 +{currentNPC.HpPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");'''),

    # --- 攻击 ---
    ('''        int rawGain = currentPlayer.Attack * currentNPC.AtkPercent / 100;
        int inversed = rawGain * 100 / Mathf.Max(1, currentPlayer.AttackMultiplier);
        currentPlayer.AddAttack(inversed);
        NPCController.IncrementPurchaseCount();''',
     '''        // 同上：不再逆推，直接加，让攻击系数正常生效（与属性道具/祝福一致）
        int rawGain = Mathf.CeilToInt(currentPlayer.Attack * currentNPC.AtkPercent / 100f);
        currentPlayer.AddAttack(rawGain);
        NPCController.IncrementPurchaseCount();'''),

    # --- 防御 ---
    ('''        int rawGain = currentPlayer.Defense * currentNPC.DefPercent / 100;
        int inversed = rawGain * 100 / Mathf.Max(1, currentPlayer.DefenseMultiplier);
        currentPlayer.AddDefense(inversed);
        NPCController.IncrementPurchaseCount();''',
     '''        // 同上：不再逆推，直接加，让防御系数正常生效（与属性道具/祝福一致）
        int rawGain = Mathf.CeilToInt(currentPlayer.Defense * currentNPC.DefPercent / 100f);
        currentPlayer.AddDefense(rawGain);
        NPCController.IncrementPurchaseCount();'''),
]


def read(path):
    with io.open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def apply(path, jobs, label):
    text = read(path)
    ok = True
    for old, new in jobs:
        n = text.count(old)
        if n != 1:
            print(f"[FAIL] {label}: 期望 1 次匹配，实际 {n} 次\n        片段: {old.splitlines()[0][:70]}")
            ok = False
            continue
        text = text.replace(old, new)
    if ok:
        write(path, text)
        print(f"[OK] {label}: {len(jobs)} 处替换完成")
    return ok


def main():
    a = apply(PD, PD_JOBS, "PlayerData.cs 系数换算")
    b = apply(SHOP, SHOP_JOBS, "NPCInteractionUI.cs 去逆推")
    print("全部完成" if (a and b) else "有失败项，未写入失败文件")
    return 0 if (a and b) else 1


if __name__ == "__main__":
    sys.exit(main())
