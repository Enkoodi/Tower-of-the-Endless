using UnityEngine;

/// <summary>
/// 『工匠』的祝福 — 受到伤害后若 HP＜1，则恢复至 1000 点生命值。最多触发 Level 次。
/// </summary>
public class DemiurgeBlessingEffect : BlessingEffect
{
    private const int RestoreTo = 1000;

    /// <summary>-1 = 尚未初始化（按当前层数算）。</summary>
    private int remainingTriggers = -1;

    /// <summary>实际剩余触发次数；未初始化时回退到当前层数（获得/读档后没打过仗也不至于为 0）。</summary>
    private int Remaining => remainingTriggers < 0 ? Level : remainingTriggers;

    public DemiurgeBlessingEffect() : base(nameof(BlessingID.DemiurgeBlessing)) { }

    public override float GetEffectValue()
    {
        return Level;
    }

    public override string GetEffectDescription()
    {
        return $"濒死时（HP＜1）恢复至 {RestoreTo} HP（最多 {Level} 次）";
    }

    /// <summary>获得时立即生效，不必等第一场战斗。</summary>
    public override void OnAcquired(PlayerData player)
    {
        remainingTriggers = Level;
    }

    /// <summary>升级时刷新次数。</summary>
    public override void OnLevelUp(PlayerData player)
    {
        remainingTriggers = Level;
    }

    public override void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        remainingTriggers = Level;
    }

    public override void OnPlayerTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        int remaining = Remaining;
        if (player == null || remaining <= 0) return;

        if (player.HP < 1)
        {
            remainingTriggers = remaining - 1;
            player.SetHP(RestoreTo);
            ui?.AddLog($"<color=#FFAA44>工匠的祝福</color>：濒死回复至 <color=#44FF44>{RestoreTo}</color> HP（剩余 {remainingTriggers} 次）");
        }
    }
}
