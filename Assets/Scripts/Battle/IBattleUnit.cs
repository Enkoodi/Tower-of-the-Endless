/// <summary>
/// 战斗结算的最小单位状态。
///
/// 存在的理由：`BattleResolver` 要同时驱动两种东西 ——
///   · 实战：真实的 `PlayerData` / `EnemyController`（HP、魔力都得是活的，
///     因为面板要显示、祝福的濒死回复要读真实血量）
///   · 图鉴：一个纯数据副本（`SimBattleUnit`），绝不能碰到玩家真身
/// 所以把「结算需要的那几个读写」抽成接口，两边各自实现。
///
/// ⚠️ 命名注意：两个旧类的同名方法语义是**相反的**，别被带偏 ——
///   · `PlayerData.SubtractHP(amount)`   = 过了减伤
///   · `EnemyController.SubtractHP(amount)` = 真实伤害，**不过**减伤
///   · `EnemyController.TakeRawDamage(amount)` = 过了减伤
/// 本接口统一用 `ReceiveDamage`（过了减伤）和 `HealRaw`（精确回血），不用那两个名字。
/// </summary>
public interface IBattleUnit
{
    int HP { get; }
    int Attack { get; }
    int Defense { get; }
    int AttackCount { get; }
    int LifeSteal { get; }
    int ReflectDamage { get; }
    int DamageReduction { get; }
    int ManaMax { get; }
    int Speed { get; }

    /// <summary>战斗中会被消耗，所以必须可写</summary>
    int ManaCharge { get; set; }

    /// <summary>按减伤系数结算伤害，返回实际扣掉的 HP。</summary>
    int ReceiveDamage(int amount);

    /// <summary>精确回血：加多少就是多少，不乘任何系数。</summary>
    void HealRaw(int amount);
}
