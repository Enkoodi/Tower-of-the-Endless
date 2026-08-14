using UnityEngine;

/// <summary>
/// 移除墙壁触发器 — 挂载在对话NPC上。
/// 与NPC对话结束后玩家点击选项触发（由 DialogueTrigger 的 OnChoice1/OnChoice2 绑定）。
/// 移除指定楼层的若干墙（变成可通行），并写入楼层记忆，重返该楼层时这些墙不再生成。
/// </summary>
public class WallRemover : MonoBehaviour
{
    [Header("目标楼层")]
    [SerializeField] private int floorNumber;

    [Header("要移除的墙坐标（与 JSON 地形层坐标一致：x=列，y=行）")]
    [SerializeField] private Vector2Int[] wallPositions;

    /// <summary>供 UnityEvent 绑定的无参入口</summary>
    public void RemoveWalls()
    {
        if (wallPositions == null || wallPositions.Length == 0)
        {
            Debug.LogWarning("[WallRemover] 未配置要移除的墙坐标");
            return;
        }

        // 写入楼层记忆（持久化，重返楼层时跳过生成）
        FloorState state = FloorMemoryManager.Instance?.GetOrCreateState(floorNumber);
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();

        foreach (Vector2Int pos in wallPositions)
        {
            state?.MarkWallRemoved(pos);

            // 若当前就在目标楼层，立即移除墙对象
            if (mapGen != null && mapGen.CurrentFloor == floorNumber)
            {
                mapGen.RemoveWallAt(pos);
            }
        }

        Debug.Log($"[WallRemover] 已移除第 {floorNumber} 层的 {wallPositions.Length} 堵墙");
    }
}
