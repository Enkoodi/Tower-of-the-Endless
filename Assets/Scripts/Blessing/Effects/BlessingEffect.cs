using UnityEngine;

/// <summary>
/// 特殊祝福效果基类。
/// 生命周方法以虚方法提供，子类按需重写。默认实现为空。
/// </summary>
public abstract class BlessingEffect
{
    /// <summary>叠加层数。0 = 不持有，1 = 1 层，2 = 2 层...</summary>
    public int Level { get; private set; }

    /// <summary>对应 BlessingID 名称，用于 Dictionary 索引。</summary>
    public string EffectId { get; private set; }

    protected BlessingEffect(string effectId)
    {
        EffectId = effectId;
        Level = 1;
    }

    /// <summary>层数 +1。</summary>
    public virtual void AddLevel()
    {
        Level++;
        Debug.Log($"[BlessingEffect] {EffectId} 层数 +1（当前 {Level}）");
    }

    public bool HasLevel(int minLevel) => Level >= minLevel;

    public abstract float GetEffectValue();
    public abstract string GetEffectDescription();

    // ============================================================
    //  生命周期（子类按需重写）
    // ============================================================

    /// <summary>每回合开始时触发。</summary>
    public virtual void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui) { }

    /// <summary>每回合结束时触发。</summary>
    public virtual void OnTurnEnd(PlayerData player, EnemyController enemy, BattleUI ui) { }

    /// <summary>玩家攻击造成伤害后触发。</summary>
    public virtual void OnPlayerDealDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }

    /// <summary>玩家受到伤害后触发。</summary>
    public virtual void OnPlayerTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }

    /// <summary>敌人攻击造成伤害后触发。</summary>
    public virtual void OnEnemyDealDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }

    /// <summary>敌人受到伤害后触发。</summary>
    public virtual void OnEnemyTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }
}
