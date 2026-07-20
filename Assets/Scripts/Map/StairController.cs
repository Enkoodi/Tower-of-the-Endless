using UnityEngine;

/// <summary>
/// 楼梯控制器 — 挂载在楼梯 Prefab 上。
/// 与玩家碰撞时触发楼层切换，由 MapGenerator 加载目标楼层。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StairController : MonoBehaviour
{
    [Header("楼梯类型")]
    [SerializeField] private StairType stairType = StairType.Up;

    public StairType Type => stairType;

    private BoxCollider2D col;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
    }

    /// <summary>
    /// 玩家踩上楼梯时调用，切换楼层。
    /// 上楼梯 → 从下层进入目标层 → 出生在目标层的下楼梯(9)
    /// 下楼梯 → 从上层进入目标层 → 出生在目标层的上楼梯(8)
    /// </summary>
    public void Use(MapGenerator mapGen)
    {
        if (mapGen == null)
        {
            Debug.LogError("[Stair] MapGenerator 为空，无法切换楼层！");
            return;
        }

        int currentFloor = mapGen.CurrentFloor;
        int targetFloor = stairType == StairType.Up ? currentFloor + 1 : currentFloor - 1;

        // 上楼梯 = 从下层进入目标层；下楼梯 = 从上层进入目标层
        EntryDirection entryDir = stairType == StairType.Up
            ? EntryDirection.FromBelow
            : EntryDirection.FromAbove;

        Debug.Log($"[Stair] 使用{(stairType == StairType.Up ? "上" : "下")}楼梯：第 {currentFloor} 层 → 第 {targetFloor} 层（{entryDir}）");
        mapGen.LoadFloor(targetFloor, entryDir);
    }
}

/// <summary>
/// 楼梯类型
/// </summary>
public enum StairType
{
    Up,   // 上楼梯（前往 floor+1）
    Down  // 下楼梯（前往 floor-1）
}
