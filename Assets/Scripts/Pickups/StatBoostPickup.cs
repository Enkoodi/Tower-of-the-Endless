using UnityEngine;

/// <summary>
/// 属性增益拾取物 — 挂载在增益道具 Prefab 上。
/// 通过 StatBoostData 资产配置类型、数值和精灵。
/// 玩家走到该格子时直接为 PlayerData 增加对应属性。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class StatBoostPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    [Header("数据引用")]
    [SerializeField] private StatBoostData data;

    private SpriteRenderer sr;

    /// <summary>增益数据（供 MerchantPurchase 等外部脚本读取）</summary>
    public StatBoostData Data => data;

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
        Debug.LogWarning("[StatBoostPickup] 属性碎片是效果型道具，不能出售");
        return false;
    }


    /// <summary>IPurchasable：按数量叠加属性增益。</summary>
    public void ApplyPurchase(PlayerData player, int amount)
    {
        if (data == null)
        {
            Debug.LogWarning($"[StatBoostPickup] {name} 的 StatBoostData 未设置");
            return;
        }
        player.ApplyStatBoost(data.boostType, data.value * Mathf.Abs(amount));
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        if (sr != null && data != null && data.pickupSprite != null)
            sr.sprite = data.pickupSprite;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (data == null)
        {
            Debug.LogError($"[StatBoostPickup] {name} 的 StatBoostData 未设置！");
            return false;
        }

        if (playerData == null)
        {
            Debug.LogError($"[StatBoostPickup] playerData 为 null");
            return false;
        }

        Debug.Log($"[StatBoostPickup] 拾取 {data.displayName}！");
        playerData.ApplyStatBoost(data.boostType, data.value);

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Destroy(gameObject);
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && data != null && data.pickupSprite != null)
            sr.sprite = data.pickupSprite;
    }
#endif
}
