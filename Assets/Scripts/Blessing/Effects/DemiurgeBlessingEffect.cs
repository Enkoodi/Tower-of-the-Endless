using UnityEngine;

/// <summary>
/// 『工匠』的祝福 — 受到伤害后若 HP＜1，则复活至 1000 点生命值。**整局**最多触发 Level 次。
///
/// 触发次数**跨战斗、跨存档累计**：走 HasPersistentState 写进存档，
/// 且刻意**不**在 OnBattleStart 里重置 —— 否则每场战斗都能重新复活 Level 次（2026-10-05 修复）。
///
/// 复活时还会把**战斗状态整体回退到进战快照**（魔力充能回满、战斗内修正层清空），
/// 但**不重新触发开场祝福**（真理的 ×1.25 不会被再叠一次）。
/// </summary>
public class DemiurgeBlessingEffect : BlessingEffect
{
    private const int RestoreTo = 1000;

    /// <summary>-1 = 尚未初始化（按当前层数算）。</summary>
    private int remainingTriggers = -1;

    /// <summary>
    /// 实际剩余触发次数。未初始化时回退到当前层数 ——
    /// 否则「战斗外获得 → 下一场战斗濒死」或「读旧档（没有状态字段）后濒死」
    /// 会因为 remainingTriggers 还是 0 而不触发。
    /// </summary>
    private int Remaining => remainingTriggers < 0 ? Level : remainingTriggers;

    public DemiurgeBlessingEffect() : base(nameof(BlessingID.DemiurgeBlessing)) { }

    public override float GetEffectValue()
    {
        return Level;
    }

    public override string GetEffectDescription()
    {
        return $"濒死时（HP＜1）恢复至 {RestoreTo} HP（整局最多 {Level} 次）";
    }

    /// <summary>获得时立即生效，不必等第一场战斗。</summary>
    public override void OnAcquired(PlayerData player)
    {
        remainingTriggers = Level;
    }

    /// <summary>升级只补 1 次（总次数 = Level），不重置已经用掉的次数。</summary>
    public override void OnLevelUp(PlayerData player)
    {
        remainingTriggers = remainingTriggers < 0 ? Level : remainingTriggers + 1;
    }

    // ============================================================
    //  持久状态：触发次数跨战斗、跨存档累计
    //  ⚠️ 刻意不重写 OnBattleStart —— 那会让次数每场战斗回满。
    // ============================================================

    public override bool HasPersistentState => true;

    public override int GetPersistentState() => remainingTriggers;

    public override void RestorePersistentState(int state) => remainingTriggers = state;

    public override void OnPlayerTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        int remaining = Remaining;
        if (player == null || remaining <= 0) return;

        if (player.HP < 1)
        {
            remainingTriggers = remaining - 1;

            // 复活 = 战斗状态整体回退到「进战快照」：魔力充能回满（可再次释放）、战斗内修正层清空。
            // ⚠️ 只读快照，**不重新触发开场祝福** —— 否则真理的祝福会再 ×1.25、异乡人会再叠一次减伤。
            player.RestoreBattleEntryState();
            player.SetHP(RestoreTo);

            ui?.AddLog($"<color=#FFAA44>工匠的祝福</color>：濒死复活至 <color=#44FF44>{RestoreTo}</color> HP，" +
                       $"魔力与战斗状态回退到进战前（剩余 {remainingTriggers} 次）");
        }
    }
}
