using UnityEngine;

/// <summary>
/// 神圣火花（DivineSpark）拾取物 — 挂载在神圣火花 Prefab 上。
/// 玩家走到该格子时拾取：全局存档中的神圣火花数量 +1，无任何属性增幅，
/// 并触发游戏真结局片尾 ED。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class DivineSparkPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    /// <summary>在地图网格中的坐标（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    /// <summary>IFloorPickup：写入所属楼层与网格坐标（由 MapGenerator / DropManager 调用）。</summary>
    public void SetFloorInfo(Vector2Int gridPos, int floor)
    {
        gridPosition = gridPos;
        floorNumber = floor;
    }

    /// <summary>IPurchasable：效果型道具，不能出售。</summary>
    public bool CanSell(PlayerData player, int sellAmount)
    {
        Debug.LogWarning("[DivineSparkPickup] 神圣火花是效果型道具，不能出售");
        return false;
    }


    /// <summary>IPurchasable：只加数量，不触发片尾 ED —— 商店买到 ≠ 在地图上找到。</summary>
    public void ApplyPurchase(PlayerData player, int amount)
    {
        SaveManager.Instance?.AddDivineSpark(Mathf.Abs(amount));
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[DivineSparkPickup] playerData 为 null，无法拾取神圣火花");
            return false;
        }

        // 全局道具 +1（跨存档保留，立即写入全局存档）
        SaveManager.Instance?.AddDivineSpark(1);

        // 记录到楼层记忆中，防止重返楼层时重复生成
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log("[DivineSparkPickup] 拾取神圣火花！触发真结局片尾 ED。");
        Destroy(gameObject);

        // 触发片尾 ED（游戏真结局结尾）
        EndingManager.Trigger();
        return true;
    }
}
