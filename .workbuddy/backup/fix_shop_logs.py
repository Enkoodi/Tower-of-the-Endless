"""一次性脚本（续）：让 AddAttack/AddDefense/AddHP/AddGold 返回「实际增量」，
并把商店日志改成打实际到手值；同时给「属性修改」段补上取整口径说明。

为什么：改掉逆推之后，商店拿到的是 rawGain（标称值），
但真正进账的是 CeilToInt(rawGain * 系数 / 100) —— 两者不等，日志会骗人。
让接口返回实际增量，日志就有唯一可信来源，不用在商店里再抄一遍公式。
"""

import io
import sys

BASE = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Scripts"
PD = BASE + r"\Player\PlayerData.cs"
SHOP = BASE + r"\NPC\NPCInteractionUI.cs"

# ---------- 1. 四个接口改成返回实际增量 ----------
PD_RETURN_JOBS = [
    ('''    public void AddAttack(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddAttack))) return;
        int actualGain = Mathf.CeilToInt(amount * attackMultiplier / 100f);
        attack += actualGain;
        Debug.Log($"[PlayerData] 攻击力 +{amount}×{attackMultiplier}%={actualGain}（当前 {attack}）");
    }''',
     '''    /// <summary>加攻击力，返回实际增加量（已乘攻击系数）。</summary>
    public int AddAttack(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddAttack))) return 0;
        int actualGain = Mathf.CeilToInt(amount * attackMultiplier / 100f);
        attack += actualGain;
        Debug.Log($"[PlayerData] 攻击力 +{amount}×{attackMultiplier}%={actualGain}（当前 {attack}）");
        return actualGain;
    }'''),

    ('''    public void AddDefense(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddDefense))) return;
        int actualGain = Mathf.CeilToInt(amount * defenseMultiplier / 100f);
        defense += actualGain;
        Debug.Log($"[PlayerData] 防御力 +{amount}×{defenseMultiplier}%={actualGain}（当前 {defense}）");
    }''',
     '''    /// <summary>加防御力，返回实际增加量（已乘防御系数）。</summary>
    public int AddDefense(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddDefense))) return 0;
        int actualGain = Mathf.CeilToInt(amount * defenseMultiplier / 100f);
        defense += actualGain;
        Debug.Log($"[PlayerData] 防御力 +{amount}×{defenseMultiplier}%={actualGain}（当前 {defense}）");
        return actualGain;
    }'''),

    ('''    public void AddHP(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddHP))) return;
        int actualGain = Mathf.CeilToInt(amount * hpMultiplier / 100f);
        hp += actualGain;
        Debug.Log($"[PlayerData] 生命值 +{amount}×{hpMultiplier}%={actualGain}（当前 {hp}）");
    }''',
     '''    /// <summary>加生命值，返回实际增加量（已乘生命系数）。</summary>
    public int AddHP(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddHP))) return 0;
        int actualGain = Mathf.CeilToInt(amount * hpMultiplier / 100f);
        hp += actualGain;
        Debug.Log($"[PlayerData] 生命值 +{amount}×{hpMultiplier}%={actualGain}（当前 {hp}）");
        return actualGain;
    }'''),

    ('''    public void AddGold(int amount)
    {
        int actualGold = Mathf.CeilToInt(amount * goldMultiplier / 100f);
        gold += actualGold;
        Debug.Log($"[PlayerData] 金币 +{amount}×{goldMultiplier}%={actualGold}（当前 {gold}）");
    }''',
     '''    public int AddGold(int amount)
    {
        int actualGold = Mathf.CeilToInt(amount * goldMultiplier / 100f);
        gold += actualGold;
        Debug.Log($"[PlayerData] 金币 +{amount}×{goldMultiplier}%={actualGold}（当前 {gold}）");
        return actualGold;
    }'''),
]

# ---------- 2. 「属性修改」段补上取整口径 ----------
PD_HEADER_JOB = (
    '''    // ============================================================
    //  属性修改
    // ============================================================
''',
    '''    // ============================================================
    //  属性修改
    //
    //  取整口径（2026-09-20 定稿，与战斗内修正层同一规则）：**取整方向一律偏向玩家**。
    //  带系数的加成一律 Mathf.CeilToInt(amount * xxxMultiplier / 100f)，不要用整数除法 ——
    //  那是向下截断，等于少给，基础值小的时候还会把加成整个抹掉。
    //  AddAttack / AddDefense / AddHP / AddGold 都返回「实际增量」，调用方要显示数字就用它，
    //  不要自己再抄一遍公式（抄了就会和真实值漂移）。
    // ============================================================
''')

# ---------- 3. 商店日志改成打实际到手值 ----------
SHOP_JOBS = [
    ('''        int rawGain = Mathf.CeilToInt(currentPlayer.HP * currentNPC.HpPercent / 100f);
        currentPlayer.AddHP(rawGain);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 生命值 +{currentNPC.HpPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");''',
     '''        int rawGain = Mathf.CeilToInt(currentPlayer.HP * currentNPC.HpPercent / 100f);
        int actualGain = currentPlayer.AddHP(rawGain);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 生命值 +{currentNPC.HpPercent}%（=+{actualGain}，含生命系数 {currentPlayer.HPMultiplier}%），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");'''),

    ('''        int rawGain = Mathf.CeilToInt(currentPlayer.Attack * currentNPC.AtkPercent / 100f);
        currentPlayer.AddAttack(rawGain);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 攻击力 +{currentNPC.AtkPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");''',
     '''        int rawGain = Mathf.CeilToInt(currentPlayer.Attack * currentNPC.AtkPercent / 100f);
        int actualGain = currentPlayer.AddAttack(rawGain);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 攻击力 +{currentNPC.AtkPercent}%（=+{actualGain}，含攻击系数 {currentPlayer.AttackMultiplier}%），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");'''),

    ('''        int rawGain = Mathf.CeilToInt(currentPlayer.Defense * currentNPC.DefPercent / 100f);
        currentPlayer.AddDefense(rawGain);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 防御力 +{currentNPC.DefPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");''',
     '''        int rawGain = Mathf.CeilToInt(currentPlayer.Defense * currentNPC.DefPercent / 100f);
        int actualGain = currentPlayer.AddDefense(rawGain);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 防御力 +{currentNPC.DefPercent}%（=+{actualGain}，含防御系数 {currentPlayer.DefenseMultiplier}%），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");'''),
]


def run(path, jobs, label):
    with io.open(path, "r", encoding="utf-8", newline="") as f:
        text = f.read()
    ok = True
    for old, new in jobs:
        n = text.count(old)
        if n != 1:
            print(f"[FAIL] {label}: 期望 1 次匹配，实际 {n} 次 | {old.splitlines()[0][:60]}")
            ok = False
            continue
        text = text.replace(old, new)
    if ok:
        with io.open(path, "w", encoding="utf-8", newline="") as f:
            f.write(text)
        print(f"[OK] {label}: {len(jobs)} 处完成")
    return ok


def main():
    a = run(PD, PD_RETURN_JOBS, "PlayerData 接口返回实际增量")
    b = run(PD, [PD_HEADER_JOB], "PlayerData 属性修改段注释")
    c = run(SHOP, SHOP_JOBS, "商店日志打实际到手值")
    print("全部完成" if (a and b and c) else "有失败项")
    return 0 if (a and b and c) else 1


if __name__ == "__main__":
    sys.exit(main())
