using UnityEngine;

/// <summary>
/// 敌人减半道具 — 挂载在道具 Prefab 上。
/// 玩家走到该格子时拾取，敌人减半道具数量 +1，随后由玩家按 V 键使用。
/// 使用后下一场战斗（普通敌人与 NPC 敌人都生效）敌人血量减半。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class EnemyHalveItemPickup : MonoBehaviour, IFloorPickup, IPurchasable
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
        if (player.EnemyHalveItemCount >= sellAmount) return true;

        Debug.LogWarning($"[EnemyHalveItemPickup] 圣水不足：需要出售 {sellAmount}，当前 {player.EnemyHalveItemCount}");
        return false;
    }

    /// <summary>IPurchasable：发放（amount &gt; 0）或回收（amount &lt; 0）。</summary>
    public void ApplyPurchase(PlayerData player, int amount) => player.AddEnemyHalveItem(amount);

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
    /// 玩家走到该格子时由 PlayerMove 调用，拾取道具。
    /// </summary>
    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError($"[EnemyHalveItemPickup] playerData 为 null，无法拾取");
            return false;
        }

        playerData.AddEnemyHalveItem(1);

        // 记录到楼层记忆中，防止重返楼层时重复生成
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[EnemyHalveItemPickup] 拾取圣水（第 {floorNumber} 层）");
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
