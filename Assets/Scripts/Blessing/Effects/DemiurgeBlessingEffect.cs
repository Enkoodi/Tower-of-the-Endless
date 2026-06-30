using UnityEngine;

/// <summary>
/// 『工匠』的祝福 — 受到伤害后若 HP＜1，则恢复至 1000 点生命值。最多触发 Level 次。
/// </summary>
public class DemiurgeBlessingEffect : BlessingEffect
{
    private const int RestoreTo = 1000;

    private int remainingTriggers;

    public DemiurgeBlessingEffect() : base(nameof(BlessingID.DemiurgeBlessing)) { }

    public override float GetEffectValue()
    {
        return Level;
    }

    public override string GetEffectDescription()
    {
        return $"濒死时（HP＜1）恢复至 {RestoreTo} HP（剩余 {Level} 次）";
    }

    public override void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        remainingTriggers = Level;
    }

    public override void OnPlayerTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        if (player == null || remainingTriggers <= 0) return;

        if (player.HP < 1)
        {
            remainingTriggers--;
            player.SetHP(RestoreTo);
            ui?.AddLog($"<color=#FFAA44>工匠的祝福</color>：濒死回复至 <color=#44FF44>{RestoreTo}</color> HP（剩余 {remainingTriggers} 次）");
        }
    }
}
