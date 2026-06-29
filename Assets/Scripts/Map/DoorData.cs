using UnityEngine;

/// <summary>
/// 门数据资产 — 右键 Create → MagicTower → Door Data 创建 .asset
/// 拖入 DoorController 的 DoorData 字段即可。
/// </summary>
[CreateAssetMenu(fileName = "NewDoorData", menuName = "MagicTower/Door Data")]
public class DoorData : ScriptableObject
{
    [Header("显示信息")]
    public string doorName = "黄之门";
    public Sprite doorSprite;

    [Header("开门条件")]
    public KeyType requiredKeyType = KeyType.Yellow;

    [Tooltip("是否消耗钥匙")]
    public bool consumeKey = true;

    [Header("生命值消耗（Psyche 等 HP 门）")]
    [Tooltip("大于 0 时开门消耗玩家 HP，而非消耗钥匙")]
    public int healthCost = 0;
}
