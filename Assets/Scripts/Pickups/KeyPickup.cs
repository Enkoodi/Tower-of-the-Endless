using UnityEngine;

/// <summary>
/// 钥匙拾取物 — 挂载在钥匙 Prefab 上。
/// 通过 KeyPickupData 资产配置类型和精灵。
/// 玩家走到该格子时自动拾取，钥匙数量 +1，物体消失。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class KeyPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    [Header("数据引用")]
    [SerializeField] private KeyPickupData data;

    private SpriteRenderer sr;

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

    /// <summary>IPurchasable：钥匙是数量型道具，可出售。</summary>
    public bool CanSell(PlayerData player, int sellAmount)
    {
        int owned = player.GetKeyCount(KeyType);
        if (owned >= sellAmount) return true;

        Debug.LogWarning($"[KeyPickup] {KeyType} 钥匙不足：需要出售 {sellAmount}，当前 {owned}");
        return false;
    }

    /// <summary>IPurchasable：发放（amount &gt; 0）或回收（amount &lt; 0）。</summary>
    public void ApplyPurchase(PlayerData player, int amount) => player.AddKey(KeyType, amount);

    public KeyType KeyType => data != null ? data.keyType : KeyType.Yellow;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        ApplySprite();
    }

    /// <summary>
    /// 被 PlayerMove 调用，尝试拾取（IFloorPickup 统一入口）。
    /// 参数用 PlayerData 而非 IKeyInventory：调用方只有 PlayerMove，
    /// 而原实现拿到 IKeyInventory 后第一件事就是强转回 PlayerData，白绕一层。
    /// </summary>
    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[KeyPickup] playerData 为 null，无法拾取钥匙");
            return false;
        }

        KeyType type = KeyType;
        Debug.Log($"[KeyPickup] 拾取 {type} 钥匙！");

        playerData.AddKey(type, 1);

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Destroy(gameObject);
        return true;
    }

    private void ApplySprite()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && data != null && data.keySprite != null)
            sr.sprite = data.keySprite;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        ApplySprite();
    }
#endif
}
