using UnityEngine;

/// <summary>
/// 『真理』的祝福 — 战斗开始时魔力充能 ×(1 + 0.25×Level)，战斗结束后恢复。
/// </summary>
public class AletheiaBlessingEffect : BlessingEffect
{
    private const float BoostPerLevel = 0.25f;

    private int originalMana;

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

        originalMana = player.ManaCharge;
        int boosted = (int)(originalMana * (1f + BoostPerLevel * Level));
        int gain = boosted - originalMana;
        if (gain > 0)
        {
            player.ManaCharge = boosted;
            ui?.AddLog($"<color=#88CCFF>真理的祝福</color>：魔力充能 ×{1f + BoostPerLevel * Level:F2}（+{gain}）");
        }
    }

    public override void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won)
    {
        if (player == null) return;
        player.ManaCharge = originalMana;
    }
}
