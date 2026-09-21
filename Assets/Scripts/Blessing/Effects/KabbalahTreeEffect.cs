using UnityEngine;

/// <summary>
/// 卡巴拉生命之树 — 抵达 30 层时触发：HP×2、攻击段数 +1、减伤 +10%。最多触发 Level 次。
/// </summary>
public class KabbalahTreeEffect : BlessingEffect
{
    private const int TriggerFloor = 30;
    private const int DamageReductionBonus = 10;

    /// <summary>-1 = 尚未初始化（按当前层数算）。</summary>
    private int remainingTriggers = -1;

    /// <summary>
    /// 实际剩余触发次数。未初始化时回退到当前层数 ——
    /// 否则「战斗外获得 → 直接上 30 层」或「读档后立即上 30 层」会因为
    /// remainingTriggers 还是 0 而不触发（以前只有 OnBattleStart 会赋值）。
    /// </summary>
    private int Remaining => remainingTriggers < 0 ? Level : remainingTriggers;

    public KabbalahTreeEffect() : base(nameof(BlessingID.KabbalahTree)) { }

    public override float GetEffectValue()
    {
        return Level;
    }

    public override string GetEffectDescription()
    {
        return $"抵达 {TriggerFloor} 层时：HP×2、段数+1、减伤+{DamageReductionBonus}%（剩余 {Remaining} 次）";
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

    public override void OnEnterFloor(PlayerData player, int floorNumber, BattleUI ui)
    {
        int remaining = Remaining;
        if (player == null || remaining <= 0) return;
        if (floorNumber < TriggerFloor) return;

        remainingTriggers = remaining - 1;
        player.SetHP(player.HP * 2);
        player.AddAttackCount(1);
        player.AddDamageReduction(DamageReductionBonus);

        Debug.Log($"[KabbalahTree] 觉醒！HP → {player.HP}，段数 +1，减伤 +{DamageReductionBonus}%（剩余 {remainingTriggers} 次）");
    }
}
