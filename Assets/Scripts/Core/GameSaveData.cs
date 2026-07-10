using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 当前游戏存档数据 — 包含玩家状态、楼层记忆等。
/// </summary>
[System.Serializable]
public class GameSaveData
{
    // ============================================================
    //  玩家战斗属性
    // ============================================================
    public int hp;
    public int attack;
    public int defense;
    public int attackCount;
    public int lifeSteal;
    public int reflectDamage;
    public int damageReduction;
    public int manaCharge;
    public int manaMax;
    public int speed;

    // ============================================================
    //  属性系数
    // ============================================================
    public int goldMultiplier;
    public int hpMultiplier;
    public int attackMultiplier;
    public int defenseMultiplier;

    // ============================================================
    //  金币与钥匙
    // ============================================================
    public int gold;
    public int yellowKeys;
    public int blueKeys;
    public int redKeys;
    public int psycheKeys;
    public int aeonKeys;

    // ============================================================
    //  楼层状态（序列化格式）
    // ============================================================
    public List<FloorStateEntry> floorStates = new List<FloorStateEntry>();

    // ============================================================
    //  玩家位置
    // ============================================================
    public float playerX;
    public float playerY;
    public float playerZ;

    // ============================================================
    //  当前楼层
    // ============================================================
    public int currentFloor = -1;
}

/// <summary>
/// 单个楼层的存档条目，用于 JSON 序列化。
/// </summary>
[System.Serializable]
public class FloorStateEntry
{
    public int floorNumber;
    public List<string> defeatedEnemies = new List<string>();
    public List<string> pickedUpItems = new List<string>();
    public List<string> openedDoors = new List<string>();
    public List<string> openedBattleDoors = new List<string>();
    public List<string> activeDropItems = new List<string>();

    /// <summary>将 Vector2Int 序列化为 "x,y" 字符串</summary>
    public static string PosToString(Vector2Int pos) => $"{pos.x},{pos.y}";

    /// <summary>从 "x,y" 字符串反序列化为 Vector2Int</summary>
    public static Vector2Int StringToPos(string s)
    {
        string[] parts = s.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
            return new Vector2Int(x, y);
        return Vector2Int.zero;
    }
}
