using UnityEngine;

/// <summary>
/// 门控制器 — 挂载在门 Prefab 上。
/// 支持四种开门方式：
///   1. 钥匙门（healthCost == 0 && requiredKeyCount == 0）：检测 IKeyInventory 是否拥有钥匙
///   2. HP 门（healthCost  > 0）：检测 IPlayerHealth
///   3. 数量检测门（healthCost == 0 && requiredKeyCount > 0）：检测钥匙数量 ≥ requiredKeyCount，不消耗
///   4. 数量消耗门（healthCost == 0 && requiredKeyCount > 0 && consumeKey）：检测并消耗指定数量钥匙
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class DoorController : MonoBehaviour
{
    [Header("数据引用")]
    [SerializeField] private DoorData doorData;

    private BoxCollider2D col;
    private SpriteRenderer sr;
    private bool isOpened = false;

    public bool IsOpened => isOpened;

    /// <summary>在地图网格中的坐标（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (doorData != null && doorData.doorSprite != null && sr != null)
            sr.sprite = doorData.doorSprite;
    }

    public bool TryOpen(IKeyInventory playerInventory, IPlayerHealth playerHealth)
    {
        if (isOpened)
        {
            Debug.Log($"[Door] {name} 已打开");
            return false;
        }

        if (doorData == null)
        {
            Debug.LogError($"[Door] {name} 的 DoorData 未设置！");
            return false;
        }

        // --- HP 门（healthCost > 0）---
        if (doorData.healthCost > 0)
        {
            if (playerHealth == null)
            {
                Debug.LogError($"[Door] {name} 传入的 playerHealth 为 null！");
                return false;
            }

            if (playerHealth.HP <= doorData.healthCost)
            {
                Debug.Log($"[Door] {name} 生命值不足（需要 {doorData.healthCost}，当前 {playerHealth.HP}），无法打开");
                return false;
            }

            playerHealth.SubtractHP(doorData.healthCost);
            Open();
            Debug.Log($"[Door] {name} 消耗 {doorData.healthCost} 生命值，已打开！");
            return true;
        }

        // --- 钥匙门 ---
        if (playerInventory == null)
        {
            Debug.LogError($"[Door] {name} 传入的 playerInventory 为 null！");
            return false;
        }

        // --- 数量检测门（requiredKeyCount > 0）：钥匙数量 ≥ requiredKeyCount 才开门 ---
        if (doorData.requiredKeyCount > 0)
        {
            int count = playerInventory.GetKeyCount(doorData.requiredKeyType);
            if (count >= doorData.requiredKeyCount)
            {
                if (doorData.consumeKey)
                {
                    // 数量消耗门：一次性扣除所需数量
                    for (int i = 0; i < doorData.requiredKeyCount; i++)
                        playerInventory.UseKey(doorData.requiredKeyType);
                }

                Open();
                Debug.Log($"[Door] {name} 钥匙数量满足（需要 {doorData.requiredKeyCount}，当前 {count}），已打开！");
                return true;
            }

            Debug.Log($"[Door] {name} 需要 {doorData.requiredKeyCount} 把 {doorData.requiredKeyType} 钥匙（当前 {count}），无法打开");
            return false;
        }

        Debug.Log($"[Door] {name} 需要 {doorData.requiredKeyType} 钥匙，消耗={doorData.consumeKey}");

        if (playerInventory.HasKey(doorData.requiredKeyType))
        {
            if (doorData.consumeKey)
                playerInventory.UseKey(doorData.requiredKeyType);

            Open();
            Debug.Log($"[Door] {name} 已打开！");
            return true;
        }

        Debug.Log($"[Door] {name} 钥匙不足，无法打开");
        return false;
    }

    private void Open()
    {
        isOpened = true;

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkDoorOpened(gridPosition);

        col.enabled = false;
        if (sr != null) sr.enabled = false;
    }
}
