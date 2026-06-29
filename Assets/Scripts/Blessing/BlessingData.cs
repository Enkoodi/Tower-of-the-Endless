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
    /// <summary>直接加成：数值 + 钥匙 + 百分比均生效</summary>
    DirectBonus,
    /// <summary>需要特殊设计（条件触发等，预留）</summary>
    Conditional,
}

/// <summary>
/// 祝福唯一标识。
/// </summary>
public enum BlessingID
{
    None,
    AnthroposBlessing,              // 『安特罗波斯』的祝福
    UnbreakableWall,                // 不屈壁垒
    Regeneration,                   // 再生
    Strength,                       // 力量
    Tenacity,                       // 坚韧
    NightWalk,                      // 夜行
    ToxinSecretion,                 // 毒素分泌
    Drain,                          // 汲取
    FuryHeart,                      // 狂怒之心
    MagicAttack,                    // 魔攻
    MagicEnergy,                    // 魔能
    SlimeRegeneration,              // 黏液再生
    NousBlessing,                   // 『心灵』的祝福
    CharisBlessing,                 // 『恩惠』的祝福
    GnosisBlessing,                 // 『灵知』的祝福
    RegenerationPlus,               // 再生+
    SwordDance,                     // 剑舞
    SwordDemon,                     // 剑鬼
    StrengthPlus,                   // 力量+
    TenacityPlus,                   // 坚韧+
    NightShadowDash,                // 夜影疾行
    WarriorInstinct,                // 战士本能
    MageInstinct,                   // 法师本能
    HunterInstinct,                 // 猎手本能
    BoneShield,                     // 骨盾守护
    ManaShield,                     // 魔力护盾
    MagicAttackPlus,                // 魔攻+
    MagicEnergyPlus,                // 魔能+
    AgapeBlessing,                  // 『爱』的祝福
    AletheiaBlessing,               // 『真理』的祝福
    SigeBlessing,                   // 『静默』的祝福
    RegenerationPlusPlus,           // 再生++
    SwordSaint,                     // 剑圣
    SwordDevil,                     // 剑魔
    StrengthPlusPlus,               // 力量++
    BloodDevour,                    // 噬血
    TenacityPlusPlus,               // 坚韧++
    Allotrioi,                      // 异乡人
    ThornArmor,                     // 棘甲
    DeathMarch,                     // 死亡行军
    PleromaShard,                   // 溢光石
    ElfMagicBottle,                 // 精灵魔瓶
    WindWalkerBody,                 // 风行之体
    MagicAttackPlusPlus,            // 魔攻++
    MagicEnergyPlusPlus,            // 魔能++
    DemiurgeBlessing,               // 『工匠』的祝福
    SophiaBlessing,                 // 『智慧』的祝福
    BythosBlessing,                 // 『深渊』的祝福
    BloodForBlood,                  // 以血还血
    RegenerationPlusPlusPlus,       // 再生+++
    StrengthPlusPlusPlus,           // 力量+++
    KabbalahTree,                   // 卡巴拉生命之树
    TenacityPlusPlusPlus,           // 坚韧+++
    ArchmageWill,                   // 大法师的意志
    GrudgeThorn,                    // 怨棘
    SupremeSwordHeart,              // 无上剑心
    NoChantDomain,                  // 无咏唱领域
    AstralDrain,                    // 星界汲取
    Longinus,                        // 朗基努斯之枪
    ExtremeSpeedSoul,               // 极速之魂
    EternalBloodOath,               // 永恒的血誓
    EternalMagicSpring,             // 永恒魔泉
    GospelBook,                     // 福音书
    BloodThorn,                     // 血棘
    PhilosopherStone,               // 贤者之石
    MagicAttackPlusPlusPlus,        // 魔攻+++
    MagicEnergyPlusPlusPlus,        // 魔能+++
}

/// <summary>
/// 祝福数据资产 — 右键 Create → MagicTower → Blessing Data 创建 .asset。
/// 定义一种被动技能的效果。
/// </summary>
[CreateAssetMenu(fileName = "NewBlessingData", menuName = "MagicTower/Blessing Data")]
public class BlessingData : ScriptableObject
{
    [Header("基本信息")]
    public BlessingID id = BlessingID.None;
    public string blessingName = "铁壁";
    [TextArea(2, 4)]
    public string description = "防御力 +5";
    public BlessingRarity rarity = BlessingRarity.Common;

    [Header("显示")]
    [Tooltip("卡面背景（每个祝福可以不同，不根据稀有度决定）")]
    public Sprite backgroundSprite;

    // ============================================================
    //  效果类型
    // ============================================================

    [Header("效果类型")]
    public BlessingType type = BlessingType.DirectBonus;

    // ============================================================
    //  直接加成 — 数值
    // ============================================================

    [Header("直接数值（DirectBonus 时生效）")]
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int manaMaxBonus = 0;
    public int manaChargeBonus = 0;
    public int speedBonus = 0;
    public int hpBonus = 0;
    public int attackCountBonus = 0;
    public int lifeStealBonus = 0;
    public int reflectDamageBonus = 0;
    public int damageReductionBonus = 0;
    public int attackMultiplierBonus = 0;
    public int defenseMultiplierBonus = 0;
    public int hpMultiplierBonus = 0;
    public int goldMultiplierBonus = 0;

    // ============================================================
    //  直接加成 — 钥匙
    // ============================================================

    [Header("钥匙增减（DirectBonus 时生效，可为负数）")]
    public int yellowKeyBonus = 0;
    public int blueKeyBonus = 0;
    public int redKeyBonus = 0;
    public int psycheKeyBonus = 0;
    public int aeonKeyBonus = 0;

    // ============================================================
    //  直接加成 — 百分比（生命/攻击/防御）
    // ============================================================

    [Header("百分比加成（DirectBonus 时生效）")]
    public int hpPercentBonus = 0;
    public int attackPercentBonus = 0;
    public int defensePercentBonus = 0;

    // ============================================================
    //  条件机制（Conditional 时生效，预留）
    // ============================================================

    [Header("条件机制（Conditional 时生效，预留）")]
    public string effectDescription = "第3回合发动，造成双倍伤害";
}
