using UnityEngine;

/// <summary>
/// 上楼传送器 — 挂载在道具 Prefab 上。
/// 玩家走到该格子时拾取，上楼传送器数量 +1，随后由玩家按 X 键使用。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class FloorUpTeleporter : MonoBehaviour
{
    [Header("显示")]
    [SerializeField] private Sprite pickupSprite;

    private SpriteRenderer sr;

    /// <summary>在地图网格中的坐标（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

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
    /// 玩家走到该格子时由 PlayerMove 调用，拾取传送器。
    /// </summary>
    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError($"[FloorUpTeleporter] playerData 为 null，无法拾取");
            return false;
        }

        playerData.AddUpTeleporter(1);

        // 记录到楼层记忆中，防止重返楼层时重复生成
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[FloorUpTeleporter] 拾取上楼传送器（第 {floorNumber} 层）");
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
