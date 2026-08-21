using UnityEngine;

/// <summary>
/// 卡巴拉生命之树 — 抵达 30 层时触发：HP×2、攻击段数 +1、减伤 +10%。最多触发 Level 次。
/// </summary>
public class KabbalahTreeEffect : BlessingEffect
{
    private const int TriggerFloor = 30;
    private const int DamageReductionBonus = 10;

    private int remainingTriggers;

    public KabbalahTreeEffect() : base(nameof(BlessingID.KabbalahTree)) { }

    public override float GetEffectValue()
    {
        return Level;
    }

    public override string GetEffectDescription()
    {
        return $"抵达 {TriggerFloor} 层时：HP×2、段数+1、减伤+{DamageReductionBonus}%（剩余 {Level} 次）";
    }

    public override void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        remainingTriggers = Level;
    }

    public override void OnEnterFloor(PlayerData player, int floorNumber, BattleUI ui)
    {
        if (player == null || remainingTriggers <= 0) return;
        if (floorNumber < TriggerFloor) return;

        remainingTriggers--;
        player.SetHP(player.HP * 2);
        player.AddAttackCount(1);
        player.AddDamageReduction(DamageReductionBonus);

        Debug.Log($"[KabbalahTree] 觉醒！HP → {player.HP}，段数 +1，减伤 +{DamageReductionBonus}%（剩余 {remainingTriggers} 次）");
    }
}
