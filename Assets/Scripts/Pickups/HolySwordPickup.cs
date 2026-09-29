using UnityEngine;

/// <summary>
/// 神圣剑拾取物 — 挂载在神圣剑装备 Prefab 上。
/// 玩家走到该格子时获得属性增益（可选）和魔力伤害增幅能力。
/// 注意：预制体上不要同时挂 StatBoostPickup，属性增益直接在本脚本配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class HolySwordPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    [Header("增幅配置")]
    [Tooltip("魔力伤害倍率（百分比），200 = 2倍伤害")]
    [SerializeField] private int multiplierPercent = 200;

    [Header("属性增益（可选）")]
    [SerializeField] private bool applyStatBoost = false;
    [SerializeField] private StatBoostType boostType;
    [SerializeField] private int boostValue;

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
        Debug.LogWarning("[HolySwordPickup] 神圣剑是效果型道具，不能出售");
        return false;
    }


    /// <summary>IPurchasable：给玩家加上魔力倍率组件（重复购买覆盖倍率）。</summary>
    public void ApplyPurchase(PlayerData player, int amount)
    {
        if (applyStatBoost)
            player.ApplyStatBoost(boostType, boostValue);

        HolySwordEffect amp = player.GetComponent<HolySwordEffect>();
        if (amp == null)
            amp = player.gameObject.AddComponent<HolySwordEffect>();
        amp.MultiplierPercent = multiplierPercent;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[HolySwordPickup] playerData 为 null");
            return false;
        }

        // 属性增益
        if (applyStatBoost)
            playerData.ApplyStatBoost(boostType, boostValue);

        // 添加神圣剑效果组件
        HolySwordEffect amp = playerData.GetComponent<HolySwordEffect>();
        if (amp == null)
        {
            amp = playerData.gameObject.AddComponent<HolySwordEffect>();
        }
        amp.MultiplierPercent = multiplierPercent;

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[HolySwordPickup] 玩家获得神圣剑！魔力伤害倍率 = {multiplierPercent}%");
        Destroy(gameObject);
        return true;
    }
}
