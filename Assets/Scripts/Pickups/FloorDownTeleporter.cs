using UnityEngine;

/// <summary>
/// 下楼传送器 — 挂载在道具 Prefab 上。
/// 玩家拾取后按 X 键可向下传送一层，传送到目标层的"上楼梯"位置。
/// 仅可使用一次，使用后销毁。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class FloorDownTeleporter : MonoBehaviour
{
    [Header("显示")]
    [SerializeField] private Sprite pickupSprite;

    private SpriteRenderer sr;

    /// <summary>在地图网格中的坐标（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    private bool isPickedUp = false;
    private bool isUsed = false;

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

    void Update()
    {
        if (!isPickedUp || isUsed) return;

        if (Input.GetKeyDown(KeyCode.X))
        {
            TryUse();
        }
    }

    /// <summary>
    /// 玩家走到该格子时由 PlayerMove 调用，拾取传送器。
    /// </summary>
    public bool TryPickup(PlayerData playerData)
    {
        if (isPickedUp) return false;

        isPickedUp = true;

        // 隐藏视觉并禁用碰撞，让玩家可以站在该格
        if (sr != null) sr.enabled = false;
        GetComponent<BoxCollider2D>().enabled = false;

        // 记录到楼层记忆中，防止重返楼层时重复生成
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        // 脱离地图挂载点并设为跨场景持久，确保楼层切换后不丢失
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Debug.Log($"[FloorDownTeleporter] 拾取下楼传送器！按 X 键使用（第 {floorNumber} 层）");
        return true;
    }

    private void TryUse()
    {
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen == null)
        {
            Debug.LogWarning("[FloorDownTeleporter] 未找到 MapGenerator，无法传送");
            return;
        }

        int currentFloor = mapGen.CurrentFloor;
        int targetFloor = currentFloor - 1;

        // 检查目标楼层是否存在
        string path = $"floor_{targetFloor:D2}";
        if (Resources.Load<TextAsset>(path) == null)
        {
            Debug.LogWarning($"[FloorDownTeleporter] 目标楼层 {targetFloor} 不存在");
            return;
        }

        Debug.Log($"[FloorDownTeleporter] 使用下楼传送器：第 {currentFloor} 层 → 第 {targetFloor} 层");
        isUsed = true;

        // FromAbove = 从上层进入 → 出生在目标层的上楼梯(8)
        mapGen.LoadFloor(targetFloor, EntryDirection.FromAbove);

        Destroy(gameObject);
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
