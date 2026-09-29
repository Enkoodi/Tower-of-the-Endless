using UnityEngine;

/// <summary>
/// 魔力精华 — 挂载在道具 Prefab 上。
/// 玩家走到该格子时拾取，魔力精华数量 +1，随后由玩家使用（使用效果待定）。
/// 与其它道具一致：拾取会写入楼层记忆，防止重返楼层时重复生成。
///
/// 实现 IFloorPickup 即可 —— PlayerMove 的拾取分发、MapGenerator 的坐标写入、
/// DropManager 的掉落物坐标写入都会自动认它，不需要再改那三处。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class ManaEssencePickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    [Header("显示")]
    [SerializeField] private Sprite pickupSprite;

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

    /// <summary>IPurchasable：数量型道具，可出售。</summary>
    public bool CanSell(PlayerData player, int sellAmount)
    {
        if (player.ManaEssenceCount >= sellAmount) return true;

        Debug.LogWarning($"[ManaEssencePickup] 魔力精华不足：需要出售 {sellAmount}，当前 {player.ManaEssenceCount}");
        return false;
    }

    /// <summary>IPurchasable：发放（amount &gt; 0）或回收（amount &lt; 0）。</summary>
    public void ApplyPurchase(PlayerData player, int amount) => player.AddManaEssence(amount);

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        if (sr != null && pickupSprite != null)
            sr.sprite = pickupSprite;
    }

    /// <summary>
    /// 玩家走到该格子时由 PlayerMove 调用，拾取魔力精华。
    /// </summary>
    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[ManaEssencePickup] playerData 为 null，无法拾取");
            return false;
        }

        playerData.AddManaEssence(1);

        // 记录到楼层记忆中，防止重返楼层时重复生成
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[ManaEssencePickup] 拾取魔力精华（第 {floorNumber} 层）");
        Destroy(gameObject);
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && pickupSprite != null)
            sr.sprite = pickupSprite;
    }
#endif
}
