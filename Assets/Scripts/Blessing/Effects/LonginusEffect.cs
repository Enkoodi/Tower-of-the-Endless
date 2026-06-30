using UnityEngine;

/// <summary>
/// 朗基努斯之枪 — 奇数回合攻击段数 +1×Level，偶数回合攻击力 +50×Level。当回合有效，回合结束移除。
/// </summary>
public class LonginusEffect : BlessingEffect
{
    private const int AttackPerLevel = 50;

    private int turnCount = 0;
    private bool buffApplied = false;
    private int appliedAttackBonus = 0;
    private int appliedAttackCountBonus = 0;

    public LonginusEffect() : base(nameof(BlessingID.Longinus)) { }

    public override float GetEffectValue()
    {
        return Level;
    }

    public override string GetEffectDescription()
    {
        return $"奇数回合段数 +{Level}，偶数回合攻击力 +{AttackPerLevel * Level}";
    }

    public override void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        turnCount = 0;
        buffApplied = false;
        appliedAttackBonus = 0;
        appliedAttackCountBonus = 0;
    }

    public override void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (player == null) return;

        turnCount++;

        if (turnCount % 2 == 1)
        {
            // 奇数回合：段数临时 +Level
            appliedAttackCountBonus = Level;
            player.AddAttackCount(appliedAttackCountBonus);
            ui?.AddLog($"<color=#FF6666>朗基努斯之枪</color>：奇数回合，攻击段数 +{appliedAttackCountBonus}");
        }
        else
        {
            // 偶数回合：攻击力临时 +50×Level
            appliedAttackBonus = AttackPerLevel * Level;
            player.AddAttack(appliedAttackBonus);
            ui?.AddLog($"<color=#FF6666>朗基努斯之枪</color>：偶数回合，攻击力 +{appliedAttackBonus}");
        }

        buffApplied = true;
    }

    public override void OnTurnEnd(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (!buffApplied || player == null) return;

        if (appliedAttackCountBonus > 0)
        {
            player.AddAttackCount(-appliedAttackCountBonus);
            appliedAttackCountBonus = 0;
        }
        if (appliedAttackBonus > 0)
        {
            player.AddAttack(-appliedAttackBonus);
            appliedAttackBonus = 0;
        }

        buffApplied = false;
    }
}
