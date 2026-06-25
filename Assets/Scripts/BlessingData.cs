using UnityEngine;

/// <summary>
/// 祝福数据资产 — 右键 Create → MagicTower → Blessing Data 创建 .asset。
/// 定义一种被动技能的效果，BlessingPickup 弹出选择面板时随机展示。
/// （预留接口，Blessing 系统尚未实现）
/// </summary>
[CreateAssetMenu(fileName = "NewBlessingData", menuName = "MagicTower/Blessing Data")]
public class BlessingData : ScriptableObject
{
    [Header("基本信息")]
    public string blessingName = "铁壁";
    [TextArea(2, 4)]
    public string description = "防御力 +5";

    [Header("属性加成")]
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int manaMaxBonus = 0;
    public int manaChargeBonus = 0;
    public int speedBonus = 0;
    public int hpBonus = 0;
}
