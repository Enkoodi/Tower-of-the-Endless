using UnityEngine;

/// <summary>
/// 『恩惠』的祝福 — 战斗胜利后额外获得 10×Level 金币。
/// </summary>
public class CharisBlessingEffect : BlessingEffect
{
    private const int GoldPerLevel = 10;

    public CharisBlessingEffect() : base(nameof(BlessingID.CharisBlessing)) { }

    public override float GetEffectValue()
    {
        return GoldPerLevel * Level;
    }

    public override string GetEffectDescription()
    {
        return $"战斗胜利后额外获得 {GoldPerLevel * Level} 金币";
    }

    public override void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won)
    {
        if (!won || player == null) return;

        int gold = GoldPerLevel * Level;
        player.AddGold(gold);
        ui?.AddLog($"<color=#FFDD44>恩惠的祝福</color>：额外获得 <color=#FFDD44>{gold}</color> 金币");
    }
}
