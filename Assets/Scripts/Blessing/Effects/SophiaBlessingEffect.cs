using UnityEngine;

/// <summary>
/// 『智慧』的祝福 — 获得时 HP +1000，每升一级再 +1000。
/// 每回合开始恢复上一回合受到伤害的 (10 + Level×10)%。
/// </summary>
public class SophiaBlessingEffect : BlessingEffect
{
    private const int HPPerLevel = 1000;
    private const int BaseHealPercent = 10; // 基础 + 10×Level

    private int damageTakenThisTurn;

    public SophiaBlessingEffect() : base(nameof(BlessingID.SophiaBlessing)) { }

    public override float GetEffectValue()
    {
        return HPPerLevel;
    }

    public override string GetEffectDescription()
    {
        return $"HP +{HPPerLevel}（永久），每回合恢复上回合受伤量的 {BaseHealPercent + Level * 10}%";
    }

    public override void OnAcquired(PlayerData player)
    {
        player.Heal(HPPerLevel);
    }

    public override void OnLevelUp(PlayerData player)
    {
        player.Heal(HPPerLevel);
    }

    public override void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (damageTakenThisTurn > 0 && player != null)
        {
            int healPercent = BaseHealPercent + Level * 10;
            int healAmount = damageTakenThisTurn * healPercent / 100;
            if (healAmount > 0)
            {
                player.Heal(healAmount);
                ui.AddLog($"<color=#9999CC>智慧的祝福</color>：恢复 <color=#44FF44>{healAmount}</color> 点生命");
            }
            damageTakenThisTurn = 0;
        }
    }

    public override void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won)
    {
        damageTakenThisTurn = 0;
    }

    public override void OnPlayerTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        damageTakenThisTurn += damage;
    }
}
