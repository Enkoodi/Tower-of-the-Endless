using UnityEngine;

/// <summary>
/// 祝福拾取物 — 挂载在祝福道具 Prefab 上。
/// 玩家拾取后弹出祝福选择面板，选择后销毁此对象。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class BlessingPickup : MonoBehaviour
{
    [Header("数据引用")]
    [SerializeField] private Sprite blessingSprite;
    [SerializeField] private BlessingPool overridePool;

    private SpriteRenderer sr;

    /// <summary>在地图网格中的坐标（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    public BlessingPool OverridePool => overridePool;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        if (sr != null && blessingSprite != null)
            sr.sprite = blessingSprite;
    }

    /// <summary>
    /// 被 PlayerMove 调用，弹出祝福选择面板。
    /// 实际销毁由 BlessingManager 在选择后执行。
    /// </summary>
    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[BlessingPickup] playerData 为 null");
            return false;
        }

        if (BlessingManager.Instance == null)
        {
            Debug.LogError("[BlessingPickup] BlessingManager 不存在！");
            return false;
        }

        Debug.Log("[BlessingPickup] 拾取祝福道具，弹出选择面板");

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        BlessingManager.Instance.Show(playerData, this);
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && blessingSprite != null)
            sr.sprite = blessingSprite;
    }
#endif
}
