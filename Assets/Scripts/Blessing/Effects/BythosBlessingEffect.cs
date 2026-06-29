using UnityEngine;

/// <summary>
/// 『深渊』的祝福 — Bythos Blessing。
/// 每回合开始时，敌方受到当前生命值 15% × Level 的直接伤害（无视防御和减伤）。
/// </summary>
public class BythosBlessingEffect : BlessingEffect
{
    private const int PercentPerLevel = 15;

    public BythosBlessingEffect() : base(nameof(BlessingID.BythosBlessing)) { }

    public override float GetEffectValue() => PercentPerLevel * Level;

    public override string GetEffectDescription()
        => $"每回合开始时，敌方受到当前生命值 {PercentPerLevel * Level}% 的真实伤害";

    public override void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (enemy == null || enemy.IsDefeated) return;

        int dmg = Mathf.CeilToInt(enemy.HP * PercentPerLevel * Level / 100f);
        if (dmg <= 0) return;

        enemy.SubtractHP(dmg);
        ui.AddLog($"<color=#330033>深渊侵蚀</color>，造成 <color=#FF4444>{dmg}</color> 点真实伤害");
        ui.UpdateEnemyPanel(enemy);
    }
}
