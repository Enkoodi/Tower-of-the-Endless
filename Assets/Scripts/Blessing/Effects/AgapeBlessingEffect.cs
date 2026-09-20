using UnityEngine;

/// <summary>
/// 『爱』的祝福 — 每回合损失当前生命值 3%×Level（最低只扣到 1 点，不会致命），
/// 同时获得攻击力 +60×Level。
/// 加攻写进战斗内属性修正层（固定值，不乘攻击系数），战斗结束随修正层清零。
/// </summary>
public class AgapeBlessingEffect : BlessingEffect
{
    private const int HpPercentPerLevel = 3;
    private const int AttackPerLevel = 60;

    /// <summary>仅用于战终日志展示，数值本身的清理由修正层统一负责。</summary>
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

        // 扣血（真实伤害，无视减伤；但最低只会扣到 1 点生命值，不会致命）
        // 向下取整：这是「削弱」，取整偏向玩家 → 少扣（HP 50 的 3% = 1.5 → 扣 1 而不是 2）
        int hpLoss = Mathf.FloorToInt(player.HP * HpPercentPerLevel * Level / 100f);
        int actualLoss = player.SubtractRawHPKeepAlive(hpLoss);
        ui?.AddLog($"<color=#FF6688>爱的祝福</color>：损失 <color=#FF4444>{actualLoss}</color> 点生命值");

        // 加攻：写进战斗内修正层，以战斗前攻击力为基准累加固定值
        int atkBonus = AttackPerLevel * Level;
        player.AddBattleAttackFlat(atkBonus);
        accumulatedAttackBonus += atkBonus;
        ui?.AddLog($"<color=#FF6688>爱的祝福</color>：攻击力 <color=#44FF44>+{atkBonus}</color>" +
                   $"（本场累计 +{accumulatedAttackBonus}）");
    }

    public override void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won)
    {
        if (player == null || accumulatedAttackBonus <= 0) return;

        // 只播日志 —— 数值由 PlayerData.EndBattleStats 在战斗结束时整体清零
        ui?.AddLog($"<color=#FF6688>爱的祝福</color>：攻击力加成消失（<color=#FF4444>-{accumulatedAttackBonus}</color>）");
        accumulatedAttackBonus = 0;
    }
}
