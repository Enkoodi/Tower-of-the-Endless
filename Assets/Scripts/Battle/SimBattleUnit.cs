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

    public static SimBattleUnit FromPlayer(PlayerData p) => FromPlayer(p, 0, 0);

    /// <summary>
    /// 带装备加成的副本 —— 给「衔尾之十字」这类**战斗开始时**才生效的效果用：
    /// 图鉴在战斗外打开，那些效果不会自动生效，所以由调用方先算好加成再传进来。
    /// </summary>
    public static SimBattleUnit FromPlayer(PlayerData p, int lifeStealBonus, int reflectDamageBonus) => new SimBattleUnit(
        p.HP, p.Attack, p.Defense, p.AttackCount,
        p.LifeSteal + lifeStealBonus, p.ReflectDamage + reflectDamageBonus,
        p.DamageReduction, p.ManaCharge, p.ManaMax, p.Speed);

    /// <summary>
    /// 「理论无限血量」的哨兵值，见 <see cref="FromPlayerUnlimitedHP"/>。
    /// 取 10^9：200 回合上限 × 单回合数百万伤害也撑得住，离 <c>int.MaxValue</c> 还留足余量，
    /// 回血累加不会溢出。
    /// </summary>
    public const int UnlimitedHP = 1000000000;

    /// <summary>
    /// 图鉴专用：把玩家当成<b>理论无限血量</b> —— 战斗中不会阵亡，一路打到敌人被击败。
    ///
    /// 这样 <c>InitialHP - HP</c> 拿到的就是「打完这一场真实要扣掉多少血」（战损），
    /// 而<b>不会</b>被玩家当前的血量截断。之前用玩家真实血量，一旦打不赢就会扣到 0 为止，
    /// 于是显示的数字恒等于「玩家自己的血量」、与敌人无关 —— 那是个没用的数。
    ///
    /// ⚠️ 只给图鉴用。实战走 <c>PlayerData</c> 真身，那里必须正常死亡。
    /// </summary>
    public static SimBattleUnit FromPlayerUnlimitedHP(PlayerData p, int lifeStealBonus, int reflectDamageBonus)
        => new SimBattleUnit(
            UnlimitedHP, p.Attack, p.Defense, p.AttackCount,
            p.LifeSteal + lifeStealBonus, p.ReflectDamage + reflectDamageBonus,
            p.DamageReduction, p.ManaCharge, p.ManaMax, p.Speed);

    /// <summary>
    /// 图鉴副本。数值走 <see cref="EnemyScaling"/>：挑战模式下必须与实战一样被放大，
    /// 否则手册的战损与排序全是按原值算的，和真正打起来对不上。
    /// </summary>
    /// <param name="exempt">可战斗 NPC 传 true —— 与实战一样豁免挑战倍率。</param>
    public static SimBattleUnit FromEnemyStats(EnemyStats e, bool exempt = false)
    {
        EnemyScaling.Values v = EnemyScaling.Apply(e, exempt);
        return new SimBattleUnit(
            v.hp, v.attack, v.defense, v.attackCount, v.lifeSteal, v.reflectDamage,
            v.damageReduction, v.manaCharge, v.manaMax, v.speed);
    }

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

    /// <summary>
    /// 真实伤害：无视防御与减伤，可以致死。返回实际扣掉的 HP。
    /// 与 ReceiveDamage 的唯一区别就是不过减伤（撕裂 Rend 用）。
    /// </summary>
    public int ReceiveTrueDamage(int amount)
    {
        if (amount <= 0) return 0;
        int actual = System.Math.Min(amount, HP);
        HP -= actual;
        return actual;
    }

    /// <summary>精确回血，不乘任何系数。</summary>
    public void HealRaw(int amount)
    {
        if (amount <= 0) return;
        HP += amount;
    }
}
