/// <summary>
/// 钥匙类型枚举。
/// </summary>
public enum KeyType
{
    Yellow,
    Blue,
    Red,
    Psyche,
    Aeon,
}

/// <summary>
/// 钥匙背包接口 — 由 PlayerData 实现，DoorController / KeyPickup 通过此接口查询/消耗钥匙。
/// </summary>
public interface IKeyInventory
{
    bool HasKey(KeyType keyType);
    void UseKey(KeyType keyType);
}

/// <summary>
/// 玩家生命值接口 — 供 DoorController（Psyche 生命之门）查询/扣除 HP。
/// </summary>
public interface IPlayerHealth
{
    int HP { get; }
    void SubtractHP(int amount);
}
