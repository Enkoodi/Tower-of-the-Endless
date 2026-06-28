using UnityEngine;

/// <summary>
/// 祝福稀有度（影响抽取权重）。
/// </summary>
public enum BlessingRarity
{
    Common,
    Rare,
    Epic,
    Legendary,
}

/// <summary>
/// 祝福效果类型。
/// </summary>
public enum BlessingType
{
    /// <summary>直接数值增加（attackBonus / defenseBonus / ...）</summary>
    StatBonus,
    /// <summary>百分比提升（percentValue 代表百分比，percentTarget 代表作用于哪个属性）</summary>
    PercentBonus,
    /// <summary>条件触发型（复杂机制，预留）</summary>
    Conditional,
}

/// <summary>
/// 百分比加成目标属性。
/// </summary>
public enum PercentTarget
{
    Attack,
    Defense,
    HP,
    ManaMax,
    ManaCharge,
    Speed,
    AttackCount,
    LifeSteal,
    ReflectDamage,
}

/// <summary>
/// 祝福数据资产 — 右键 Create → MagicTower → Blessing Data 创建 .asset。
/// 定义一种被动技能的效果。
/// </summary>
[CreateAssetMenu(fileName = "NewBlessingData", menuName = "MagicTower/Blessing Data")]
public class BlessingData : ScriptableObject
{
    [Header("基本信息")]
    public string blessingName = "铁壁";
    [TextArea(2, 4)]
    public string description = "防御力 +5";
    public BlessingRarity rarity = BlessingRarity.Common;

    [Header("显示")]
    [Tooltip("卡面背景（每个祝福可以不同，不根据稀有度决定）")]
    public Sprite backgroundSprite;

    // ============================================================
    //  效果配置
    // ============================================================

    [Header("效果类型")]
    public BlessingType type = BlessingType.StatBonus;

    [Header("直接数值（StatBonus 时生效）")]
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int manaMaxBonus = 0;
    public int manaChargeBonus = 0;
    public int speedBonus = 0;
    public int hpBonus = 0;
    public int attackCountBonus = 0;
    public int lifeStealBonus = 0;
    public int reflectDamageBonus = 0;

    [Header("百分比加成（PercentBonus 时生效）")]
    public int percentValue = 10;
    public PercentTarget percentTarget = PercentTarget.Attack;

    [Header("条件机制（Conditional 时生效，预留）")]
    public string effectDescription = "第3回合发动，造成双倍伤害";
}
