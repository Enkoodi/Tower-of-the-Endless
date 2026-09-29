using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个楼层的记忆状态 — 记录哪些敌人已被击败、哪些物品已被拾取、
/// 哪些门已开启、哪些掉落物仍活跃。
/// </summary>
[System.Serializable]
public class FloorState
{
    public int floorNumber;

    /// <summary>已被击败的敌人网格坐标</summary>
    public HashSet<Vector2Int> defeatedEnemies = new HashSet<Vector2Int>();

    /// <summary>已被拾取的物品网格坐标（地图预设物品）</summary>
    public HashSet<Vector2Int> pickedUpItems = new HashSet<Vector2Int>();

    /// <summary>已开启的门网格坐标</summary>
    public HashSet<Vector2Int> openedDoors = new HashSet<Vector2Int>();

    /// <summary>已开启的战斗门网格坐标</summary>
    public HashSet<Vector2Int> openedBattleDoors = new HashSet<Vector2Int>();

    /// <summary>当前楼层活跃的掉落物位置（未被拾取）</summary>
    public HashSet<Vector2Int> activeDropItems = new HashSet<Vector2Int>();

    /// <summary>已被夹击扣血的敌人，网格坐标 → 剩余HP</summary>
    public Dictionary<Vector2Int, int> pinceredEnemies = new Dictionary<Vector2Int, int>();

    /// <summary>已被移除（消失）的墙网格坐标</summary>
    public HashSet<Vector2Int> removedWalls = new HashSet<Vector2Int>();

    /// <summary>已被移除（消失）的NPC网格坐标</summary>
    public HashSet<Vector2Int> removedNpcs = new HashSet<Vector2Int>();

    public FloorState(int floor)
    {
        floorNumber = floor;
    }

    public bool IsEnemyDefeated(Vector2Int pos) => defeatedEnemies.Contains(pos);
    public bool IsItemPickedUp(Vector2Int pos) => pickedUpItems.Contains(pos);

    /// <summary>
    /// 该格子上的敌人 / NPC 是否已被清除 —— **查询敌人是否还在，一律用这个**。
    ///
    /// 普通敌人记进 defeatedEnemies，脚本敌人（对话触发的 NPC 战斗）记进 removedNpcs，
    /// 这只是「同一件事的两种记法」，取决于当时走的是哪条代码路径。
    /// 所以**必须两个集合都看**：只查其中一个，用另一种记法写下的格子就会被当成
    /// 「还活着」而在重返楼层时复活。
    ///
    /// 历史事故：`Enemies/VampireLord/VampireLord.prefab` 放在 enemies 层，
    /// 但挂的是 NpcBattler（记进 removedNpcs），而 SpawnEnemy 当时只查
    /// IsEnemyDefeated → 击败后上下楼就复活。
    /// </summary>
    public bool IsEntityCleared(Vector2Int pos) =>
        defeatedEnemies.Contains(pos) || removedNpcs.Contains(pos);
    public bool IsDoorOpened(Vector2Int pos) => openedDoors.Contains(pos);
    public bool IsBattleDoorOpened(Vector2Int pos) => openedBattleDoors.Contains(pos);
    public bool IsDropActive(Vector2Int pos) => activeDropItems.Contains(pos);
    public bool IsEnemyPincered(Vector2Int pos) => pinceredEnemies.ContainsKey(pos);
    public bool IsWallRemoved(Vector2Int pos) => removedWalls.Contains(pos);
    public bool IsNpcRemoved(Vector2Int pos) => removedNpcs.Contains(pos);

    public void MarkEnemyDefeated(Vector2Int pos) => defeatedEnemies.Add(pos);
    public void MarkItemPickedUp(Vector2Int pos) => pickedUpItems.Add(pos);
    public void MarkDoorOpened(Vector2Int pos) => openedDoors.Add(pos);
    public void MarkBattleDoorOpened(Vector2Int pos) => openedBattleDoors.Add(pos);
    public void MarkDropActive(Vector2Int pos) => activeDropItems.Add(pos);
    public void MarkDropPickedUp(Vector2Int pos) => activeDropItems.Remove(pos);
    public void MarkEnemyPincered(Vector2Int pos, int remainingHp) => pinceredEnemies[pos] = remainingHp;
    public void MarkWallRemoved(Vector2Int pos) => removedWalls.Add(pos);
    public void MarkNpcRemoved(Vector2Int pos) => removedNpcs.Add(pos);

    public void Reset()
    {
        defeatedEnemies.Clear();
        pickedUpItems.Clear();
        openedDoors.Clear();
        openedBattleDoors.Clear();
        activeDropItems.Clear();
        pinceredEnemies.Clear();
        removedWalls.Clear();
        removedNpcs.Clear();
    }
}
