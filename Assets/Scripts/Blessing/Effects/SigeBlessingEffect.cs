using UnityEngine;

/// <summary>
/// 『静默』的祝福 — 每回合开始敌人受到当前生命值 5%×Level 的真实伤害。
/// </summary>
public class SigeBlessingEffect : BlessingEffect
{
    private const int PercentPerLevel = 5;

    public SigeBlessingEffect() : base(nameof(BlessingID.SigeBlessing)) { }

    public override float GetEffectValue()
    {
        return PercentPerLevel * Level;
    }

    public override string GetEffectDescription()
    {
        return $"每回合开始对敌人造成当前 HP 的 {PercentPerLevel * Level}% 真实伤害";
    }

    public override void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (enemy == null || ui == null) return;

        int dmg = Mathf.CeilToInt(enemy.HP * PercentPerLevel * Level / 100f);
        if (dmg > 0)
        {
            enemy.SubtractHP(dmg);
            ui.AddLog($"<color=#9988AA>静默的祝福</color>：对 <color=#779977>{enemy.EnemyName}</color> 造成 <color=#FF4444>{dmg}</color> 点真实伤害");
        }
    }
}
