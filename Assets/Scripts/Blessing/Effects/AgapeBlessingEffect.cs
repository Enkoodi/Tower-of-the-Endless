using UnityEngine;

/// <summary>
/// 『爱』的祝福 — 每回合损失当前生命值 3%×Level，同时获得攻击力 +60×Level。
/// 攻击力在战斗结束后移除。
/// </summary>
public class AgapeBlessingEffect : BlessingEffect
{
    private const int HpPercentPerLevel = 3;
    private const int AttackPerLevel = 60;

    private int accumulatedAttackBonus = 0;

    public AgapeBlessingEffect() : base(nameof(BlessingID.AgapeBlessing)) { }

    public override float GetEffectValue()
    {
        return Level * HpPercentPerLevel;
    }

    public override string GetEffectDescription()
    {
        return $"每回合损失当前 {Level * HpPercentPerLevel}% HP，攻击力 +{Level * AttackPerLevel}";
    }

    public override void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (player == null) return;

        // 扣血（真实伤害，无视减伤）
        int hpLoss = Mathf.CeilToInt(player.HP * HpPercentPerLevel * Level / 100f);
        int actualLoss = player.SubtractRawHP(hpLoss);
        ui?.AddLog($"<color=#FF6688>爱的祝福</color>：损失 <color=#FF4444>{actualLoss}</color> 点生命值");

        // 加攻（累积，战终清除）
        int atkBonus = AttackPerLevel * Level;
        player.AddAttack(atkBonus);
        accumulatedAttackBonus += atkBonus;
        ui?.AddLog($"<color=#FF6688>爱的祝福</color>：攻击力 <color=#44FF44>+{atkBonus}</color>");
    }

    public override void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won)
    {
        if (player == null || accumulatedAttackBonus <= 0) return;

        // 战终移除累积的加攻
        int toRemove = accumulatedAttackBonus;
        player.AddAttack(-toRemove);
        accumulatedAttackBonus = 0;
        ui?.AddLog($"<color=#FF6688>爱的祝福</color>：攻击力加成消失（<color=#FF4444>-{toRemove}</color>）");
    }
}
