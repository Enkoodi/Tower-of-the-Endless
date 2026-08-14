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

    /// <summary>
    /// 根据 BlessingID 创建对应的特殊祝福效果实例（未注册返回 null）。
    /// </summary>
    public static BlessingEffect Create(BlessingID id)
    {
        return id switch
        {
            BlessingID.BythosBlessing => new BythosBlessingEffect(),
            BlessingID.AgapeBlessing => new AgapeBlessingEffect(),
            BlessingID.AletheiaBlessing => new AletheiaBlessingEffect(),
            BlessingID.Allotrioi => new AllotrioiEffect(),
            BlessingID.CharisBlessing => new CharisBlessingEffect(),
            BlessingID.DemiurgeBlessing => new DemiurgeBlessingEffect(),
            BlessingID.GnosisBlessing => new GnosisBlessingEffect(),
            BlessingID.KabbalahTree => new KabbalahTreeEffect(),
            BlessingID.Longinus => new LonginusEffect(),
            BlessingID.SigeBlessing => new SigeBlessingEffect(),
            BlessingID.SophiaBlessing => new SophiaBlessingEffect(),
            _ => null,
        };
    }

    /// <summary>层数 +1。</summary>
    public virtual void AddLevel()
    {
        Level++;
        Debug.Log($"[BlessingEffect] {EffectId} 层数 +1（当前 {Level}）");
    }

    /// <summary>设置层数（供读档恢复使用，最小为 1）。</summary>
    public void SetLevel(int level)
    {
        Level = Mathf.Max(1, level);
    }

    public bool HasLevel(int minLevel) => Level >= minLevel;

    public abstract float GetEffectValue();
    public abstract string GetEffectDescription();

    // ============================================================
    //  生命周期（子类按需重写）
    // ============================================================

    /// <summary>战斗开始时触发。</summary>
    public virtual void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui) { }

    /// <summary>每回合开始时触发。</summary>
    public virtual void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui) { }

    /// <summary>每回合结束时触发。</summary>
    public virtual void OnTurnEnd(PlayerData player, EnemyController enemy, BattleUI ui) { }

    /// <summary>战斗结束时触发。won = 是否胜利。</summary>
    public virtual void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won) { }

    /// <summary>玩家攻击造成伤害后触发。</summary>
    public virtual void OnPlayerDealDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }

    /// <summary>玩家受到伤害后触发。</summary>
    public virtual void OnPlayerTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }

    /// <summary>敌人攻击造成伤害后触发。</summary>
    public virtual void OnEnemyDealDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }

    /// <summary>敌人受到伤害后触发。</summary>
    public virtual void OnEnemyTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage) { }

    /// <summary>返回 false 时本回合不消耗玩家魔力充能（默认 true）。</summary>
    public virtual bool ShouldConsumeMana(PlayerData player) => true;

    /// <summary>进入新楼层时触发。</summary>
    public virtual void OnEnterFloor(PlayerData player, int floorNumber, BattleUI ui) { }

    /// <summary>获得该祝福时触发（仅一次，用于永久属性加成）。</summary>
    public virtual void OnAcquired(PlayerData player) { }

    /// <summary>祝福升级时触发（每升一级调用一次）。</summary>
    public virtual void OnLevelUp(PlayerData player) { }
}
