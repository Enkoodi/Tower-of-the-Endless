using UnityEngine;

/// <summary>
/// 战斗门触发器 — 挂载在触发器 Prefab 上。
/// 玩家走到该格时，在指定坐标位置动态生成战斗门 Prefab。
/// 触发器不阻挡玩家通行（isTrigger = true），走过即触发。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BattleTrigger : MonoBehaviour
{
    [Header("生成的战斗门")]
    [Tooltip("要生成的战斗门 Prefab（需挂载 BattleDoorController）")]
    public GameObject doorPrefab;

    [Header("生成位置")]
    [Tooltip("战斗门要生成的网格坐标列表（可多个）")]
    public Vector2Int[] spawnPositions;

    private bool triggered = false;

    /// <summary>所属楼层编号（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    /// <summary>
    /// 触发 — 在指定网格坐标生成战斗门。
    /// 由 PlayerMove 在玩家走到该格时调用。
    /// </summary>
    public void Trigger()
    {
        if (triggered) return;
        triggered = true;

        if (doorPrefab == null)
        {
            Debug.LogError($"[BattleTrigger] {name} 未设置 doorPrefab！");
            return;
        }

        if (spawnPositions == null || spawnPositions.Length == 0)
        {
            Debug.LogWarning($"[BattleTrigger] {name} 未配置生成位置");
            return;
        }

        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen == null)
        {
            Debug.LogError("[BattleTrigger] 未找到 MapGenerator");
            return;
        }

        foreach (var pos in spawnPositions)
        {
            mapGen.SpawnBattleDoor(doorPrefab, pos, floorNumber);
        }

        Debug.Log($"[BattleTrigger] 触发器已激活！生成了 {spawnPositions.Length} 扇战斗门");
    }
}
