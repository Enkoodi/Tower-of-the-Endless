using UnityEngine;

/// <summary>
/// 掉落管理器 — 单例，集中处理敌人击败后的物品生成与楼层状态同步。
///
/// 完整流程：
///   1. EnemyController.Defeat() → DropManager.OnEnemyDefeated()
///   2. DropManager 读取敌人上的 ItemDrop 配置，生成掉落物，写入 FloorState
///   3. MapGenerator 重进楼层时调用 RespawnDropsForEnemy() 复活未拾取的掉落物
///   4. 拾取物被捡起后调用 MarkDropPickedUp() 从 FloorState 移除记录
/// </summary>
public class DropManager : MonoBehaviour
{
    public static DropManager Instance { get; private set; }

    // ========================================================================
    //  单例
    // ========================================================================

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[DropManager] 单例已初始化");
        }
        else
        {
            Debug.LogWarning($"[DropManager] 检测到重复实例，销毁 GameObject '{gameObject.name}'");
            Destroy(gameObject);
        }
    }

    // ========================================================================
    //  公开接口 — 敌人击败
    // ========================================================================

    /// <summary>
    /// 由 EnemyController.Defeat() 调用。
    /// 读取敌人身上的 ItemDrop 配置，生成掉落物并同步到 FloorState。
    /// </summary>
    public void OnEnemyDefeated(EnemyController enemy)
    {
        if (enemy == null) return;

        ItemDrop itemDrop = enemy.GetComponent<ItemDrop>();
        if (itemDrop == null || itemDrop.Drops == null || itemDrop.Drops.Length == 0)
            return;

        // 将掉落物挂到敌人所在的地图容器下，切换楼层时随 ClearMap 一起销毁
        Transform parent = enemy.transform.parent;

        foreach (var drop in itemDrop.Drops)
        {
            if (drop.prefab == null) continue;

            Vector3 spawnPos = enemy.transform.position + (Vector3)drop.offset;
            SpawnDrop(drop.prefab, spawnPos, enemy.floorNumber, parent);
        }
    }

    // ========================================================================
    //  公开接口 — 楼层重生成（由 MapGenerator 调用）
    // ========================================================================

    /// <summary>
    /// 在楼层重新生成时，为已被击败的敌人复活尚未被拾取的掉落物。
    /// 由 MapGenerator.SpawnEnemy() 调用。
    /// </summary>
    public void RespawnDropsForEnemy(
        GameObject enemyPrefab, Vector3 worldPos, int floor, FloorState state, Transform parent)
    {
        if (enemyPrefab == null || state == null) return;

        ItemDrop itemDrop = enemyPrefab.GetComponent<ItemDrop>();
        if (itemDrop == null || itemDrop.Drops == null || itemDrop.Drops.Length == 0)
            return;

        foreach (var drop in itemDrop.Drops)
        {
            if (drop.prefab == null) continue;

            Vector3 spawnPos = worldPos + (Vector3)drop.offset;
            Vector2Int dropGridPos = WorldToGrid(spawnPos);

            // 已被拾取则跳过
            if (!state.IsDropActive(dropGridPos))
                continue;

            GameObject obj = Instantiate(drop.prefab, spawnPos, Quaternion.identity, parent);
            obj.name = drop.prefab.name;
            SetPickupInfo(obj, dropGridPos, floor);
        }
    }

    // ========================================================================
    //  公开接口 — 拾取物回调
    // ========================================================================

    /// <summary>
    /// 由**所有**拾取物脚本在拾取成功后调用（KeyPickup / StatBoostPickup / BlessingPickup /
    /// 传送器 / 神圣盾 / 神圣剑 / 圣水 / 神圣火花 / 麦酒）。
    /// 从 FloorState 中移除该掉落记录，确保再次进入楼层时不会重复生成。
    ///
    /// 调用方传进来的 floor / gridPos 必须是 SetPickupInfo 写进去的那两个值 ——
    /// 一旦是默认的 (0,0) / 0，移除的就是别处的记录，本层这条永远留着。
    /// </summary>
    public void MarkDropPickedUp(int floor, Vector2Int gridPos)
    {
        FloorMemoryManager mgr = FloorMemoryManager.Instance;
        if (mgr == null) return;

        FloorState state = mgr.GetState(floor);
        if (state != null)
            state.MarkDropPickedUp(gridPos);
    }

    // ========================================================================
    //  内部工具
    // ========================================================================

    private static void SpawnDrop(GameObject prefab, Vector3 spawnPos, int floor, Transform parent)
    {
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity, parent);
        obj.name = prefab.name;

        Vector2Int gridPos = WorldToGrid(spawnPos);
        SetPickupInfo(obj, gridPos, floor);

        // 同步到 FloorState
        FloorMemoryManager.Instance?.GetOrCreateState(floor).MarkDropActive(gridPos);

        Debug.Log($"[DropManager] 掉落 {prefab.name}，位置: ({gridPos.x}, {gridPos.y})，楼层: {floor}");
    }

    /// <summary>世界坐标 → 网格坐标（取整）</summary>
    public static Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x),
            Mathf.RoundToInt(worldPos.y)
        );
    }

    /// <summary>
    /// 给生成的拾取物写入楼层编号和网格坐标。
    ///
    /// 只按 IFloorPickup 接口取组件，不再逐个类型枚举。
    /// 这里原先是一张手写的类型清单，新增拾取物时漏同步过 7 种（麦酒、神圣剑、神圣盾等），
    /// 后果是 gridPosition / floorNumber 停在默认 (0,0) / 0 —— 拾取时清掉的是**第 0 层**的记录，
    /// 本层 activeDropItems 永远清不掉 → 重返楼层重复刷出。
    /// 现在新增拾取物只要实现 IFloorPickup，这里一行都不用改。
    /// </summary>
    public static void SetPickupInfo(GameObject pickupObj, Vector2Int gridPos, int floor)
    {
        if (pickupObj == null) return;

        if (pickupObj.TryGetComponent(out IFloorPickup pickup))
            pickup.SetFloorInfo(gridPos, floor);
    }
}
