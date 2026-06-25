using UnityEngine;

/// <summary>
/// 属性增益数据资产 — 右键 Create → MagicTower → Stat Boost Data 创建 .asset
/// 拖入 StatBoostPickup 的 Data 字段即可。
/// </summary>
[CreateAssetMenu(fileName = "NewStatBoostData", menuName = "MagicTower/Stat Boost Data")]
public class StatBoostData : ScriptableObject
{
    [Header("增益类型")]
    public StatBoostType boostType = StatBoostType.Attack;

    [Header("增益数值")]
    public int value = 5;

    [Header("显示名称（如「攻击力 +5」）")]
    public string displayName = "攻击力 +5";

    [Header("显示精灵")]
    public Sprite pickupSprite;
}
