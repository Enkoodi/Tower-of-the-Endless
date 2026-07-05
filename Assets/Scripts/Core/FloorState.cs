using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个楼层的记忆状态 — 记录哪些敌人已被击败、哪些物品已被拾取、哪些门已开启。
/// </summary>
[System.Serializable]
public class FloorState
{
    public int floorNumber;

    /// <summary>已被击败的敌人网格坐标</summary>
    public HashSet<Vector2Int> defeatedEnemies = new HashSet<Vector2Int>();

    /// <summary>已被拾取的物品网格坐标</summary>
    public HashSet<Vector2Int> pickedUpItems = new HashSet<Vector2Int>();

    /// <summary>已开启的门网格坐标</summary>
    public HashSet<Vector2Int> openedDoors = new HashSet<Vector2Int>();

    /// <summary>已开启的战斗门网格坐标</summary>
    public HashSet<Vector2Int> openedBattleDoors = new HashSet<Vector2Int>();

    public FloorState(int floor)
    {
        floorNumber = floor;
    }

    public bool IsEnemyDefeated(Vector2Int pos) => defeatedEnemies.Contains(pos);
    public bool IsItemPickedUp(Vector2Int pos) => pickedUpItems.Contains(pos);
    public bool IsDoorOpened(Vector2Int pos) => openedDoors.Contains(pos);
    public bool IsBattleDoorOpened(Vector2Int pos) => openedBattleDoors.Contains(pos);

    public void MarkEnemyDefeated(Vector2Int pos) => defeatedEnemies.Add(pos);
    public void MarkItemPickedUp(Vector2Int pos) => pickedUpItems.Add(pos);
    public void MarkDoorOpened(Vector2Int pos) => openedDoors.Add(pos);
    public void MarkBattleDoorOpened(Vector2Int pos) => openedBattleDoors.Add(pos);

    public void Reset()
    {
        defeatedEnemies.Clear();
        pickedUpItems.Clear();
        openedDoors.Clear();
        openedBattleDoors.Clear();
    }
}
