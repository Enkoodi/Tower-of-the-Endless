/// <summary>
/// 图鉴专用的「战斗单位」—— 纯数据副本，绝不会碰玩家或敌人的真身。
///
/// 图鉴在战斗外打开，此时 `PlayerData` 的属性就是真实字段（战斗内修正层没生效），
/// 和实战开打时快照到的值一致，所以直接拷过来即可。
/// 敌人的数值来自 `EnemyStats` 资产。
/// </summary>
public class SimBattleUnit : IBattleUnit
{
    public int HP { get; private set; }
    public int Attack { get; }
    public int Defense { get; }
    public int AttackCount { get; }
    public int LifeSteal { get; }
    public int ReflectDamage { get; }
    public int DamageReduction { get; }
    public int ManaMax { get; }
    public int Speed { get; }
    public int ManaCharge { get; set; }

    /// <summary>开打前的血量，用来算净变化（负数 = 反而回血）</summary>
    public int InitialHP { get; }

    private SimBattleUnit(int hp, int attack, int defense, int attackCount, int lifeSteal,
                          int reflectDamage, int damageReduction,
                          int manaCharge, int manaMax, int speed)
    {
        HP = hp;
        InitialHP = hp;
        Attack = attack;
        Defense = defense;
        AttackCount = attackCount;
        LifeSteal = lifeSteal;
        ReflectDamage = reflectDamage;
        DamageReduction = damageReduction;
        ManaCharge = manaCharge;
        ManaMax = manaMax;
        Speed = speed;
    }

    public static SimBattleUnit FromPlayer(PlayerData p) => new SimBattleUnit(
        p.HP, p.Attack, p.Defense, p.AttackCount, p.LifeSteal, p.ReflectDamage,
        p.DamageReduction, p.ManaCharge, p.ManaMax, p.Speed);

    public static SimBattleUnit FromEnemyStats(EnemyStats e) => new SimBattleUnit(
        e.hp, e.attack, e.defense, e.attackCount, e.lifeSteal, e.reflectDamage,
        e.damageReduction, e.manaCharge, e.manaMax, e.speed);

    /// <summary>
    /// 按减伤结算伤害，返回实际扣掉的 HP。
    /// 与 `PlayerData.SubtractHP` / `EnemyController.TakeRawDamage` 完全同式。
    /// </summary>
    public int ReceiveDamage(int amount)
    {
        int reduced = amount * (100 - DamageReduction) / 100;
        HP -= reduced;
        if (HP < 0) HP = 0;
        return reduced;
    }

    /// <summary>精确回血，不乘任何系数。</summary>
    public void HealRaw(int amount)
    {
        if (amount <= 0) return;
        HP += amount;
    }
}
