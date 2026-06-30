using UnityEngine;

/// <summary>
/// 『灵知』的祝福 — 前 Level 回合不消耗魔力充能。
/// </summary>
public class GnosisBlessingEffect : BlessingEffect
{
    private int turnCount = 0;

    public GnosisBlessingEffect() : base(nameof(BlessingID.GnosisBlessing)) { }

    public override float GetEffectValue()
    {
        return Level;
    }

    public override string GetEffectDescription()
    {
        return $"前 {Level} 回合不消耗魔力充能";
    }

    public override void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        turnCount = 0;
    }

    public override void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        turnCount++;
    }

    public override bool ShouldConsumeMana(PlayerData player)
    {
        return turnCount > Level;
    }
}
