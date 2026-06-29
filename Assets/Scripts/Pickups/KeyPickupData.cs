using UnityEngine;

/// <summary>
/// 钥匙拾取物数据资产 — 右键 Create → MagicTower → Key Pickup Data 创建 .asset
/// 拖入 KeyPickup 的 Data 字段即可。
/// </summary>
[CreateAssetMenu(fileName = "NewKeyPickupData", menuName = "MagicTower/Key Pickup Data")]
public class KeyPickupData : ScriptableObject
{
    [Header("钥匙类型")]
    public KeyType keyType = KeyType.Yellow;

    [Header("显示精灵")]
    public Sprite keySprite;
}
