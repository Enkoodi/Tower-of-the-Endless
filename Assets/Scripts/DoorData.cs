using UnityEngine;

[CreateAssetMenu(fileName = "NewDoorData", menuName = "MagicTower/Door Data")]
public class DoorData : ScriptableObject
{
    [Header("显示信息")]
    public string doorName = "黄色铁门";
    public Sprite doorSprite;
    
    [Header("开门条件")]
    public KeyType requiredKeyType = KeyType.Yellow; // 枚举类型
    
    [Tooltip("是否消耗钥匙")]
    public bool consumeKey = true;
}

// 钥匙类型枚举，方便扩展
public enum KeyType
{
    Yellow,
    Blue,
    Red,
    Scarlet,
    Aeon,
}