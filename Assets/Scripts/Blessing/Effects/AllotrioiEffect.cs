using UnityEngine;

/// <summary>
/// 异乡人 — 前 N 回合减伤系数 +25%，N = 1 + Level。
/// </summary>
public class AllotrioiEffect : BlessingEffect
{
    private const int BaseTurns = 1;
    private const int Percent = 25;

    private int turnCount = 0;
    private bool bonusActive = false;

    private int ProtectedTurns => BaseTurns + Level;

    public AllotrioiEffect() : base(nameof(BlessingID.Allotrioi)) { }

    public override float GetEffectValue()
    {
        return ProtectedTurns;
    }

    public override string GetEffectDescription()
    {
        return $"前 {ProtectedTurns} 回合减伤 +{Percent}%";
    }

    public override void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (player == null) return;

        turnCount = 0;
        bonusActive = true;
        player.AddDamageReduction(Percent);
        ui?.AddLog($"<color=#CC88FF>异乡人</color>：前 {ProtectedTurns} 回合减伤 +{Percent}%");
    }

    public override void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        if (player == null || !bonusActive) return;

        turnCount++;
        if (turnCount > ProtectedTurns)
        {
            player.AddDamageReduction(-Percent);
            bonusActive = false;
            ui?.AddLog($"<color=#CC88FF>异乡人</color>：减伤效果消失");
        }
    }

    public override void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won)
    {
        if (player == null || !bonusActive) return;

        player.AddDamageReduction(-Percent);
        bonusActive = false;
    }
}
