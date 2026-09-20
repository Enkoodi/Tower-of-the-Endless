using UnityEngine;

/// <summary>
/// 『真理』的祝福 — 战斗开始时魔力充能 ×(1 + 0.25×Level)。
///
/// 战斗结束后「恢复」由 BattleManager 统一负责（它在 OnBattleStart 之前就快照了战斗前魔力）。
/// 本 Effect 刻意不自己还原：以前它和 BattleManager 都写 ManaCharge，而 BattleManager
/// 的快照时机在 OnBattleStart 之后，取到的是加成后的值 → 战斗结束把加成固化成新基准，
/// 每打一场魔力就再涨一次。现在只有 BattleManager 一个 owner。
/// </summary>
public class AletheiaBlessingEffect : BlessingEffect
{
    private const float BoostPerLevel = 0.25f;

    public AletheiaBlessingEffect() : base(nameof(BlessingID.AletheiaBlessing)) { }

    public override float GetEffectValue()
    {
        return 1f + BoostPerLevel * Level;
    }

    public override string GetEffectDescription()
    {
        float mult = 1f + BoostPerLevel * Level;
        return $"战斗开始时魔力充能 ×{mult:F2}";
    }

    public override void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (player == null) return;

        int before = player.ManaCharge;
        int boosted = (int)(before * (1f + BoostPerLevel * Level));
        int gain = boosted - before;
        if (gain > 0)
        {
            player.ManaCharge = boosted;
            ui?.AddLog($"<color=#88CCFF>真理的祝福</color>：魔力充能 ×{1f + BoostPerLevel * Level:F2}（+{gain}）");
        }
    }
}
