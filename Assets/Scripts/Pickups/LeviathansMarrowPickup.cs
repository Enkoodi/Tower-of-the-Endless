using UnityEngine;

/// <summary>
/// 利维坦之髓拾取物 — 挂载在「利维坦之髓」装备 Prefab 上。
/// 玩家走到该格子时获得属性增益（可选）和「每隔若干回合冻结敌人一回合」的能力。
/// 注意：预制体上不要同时挂 StatBoostPickup，属性增益直接在本脚本配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class LeviathansMarrowPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    [Header("冻结配置")]
    [Tooltip("每隔多少个回合冻结敌人一回合（5 = 第 5、10、15…回合）")]
    [SerializeField] private int freezeInterval = 5;

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
        Debug.LogWarning("[LeviathansMarrowPickup] 利维坦之髓是效果型道具，不能出售");
        return false;
    }

    /// <summary>IPurchasable：给玩家加上冻结效果组件（重复购买覆盖间隔）。</summary>
    public void ApplyPurchase(PlayerData player, int amount)
    {
        if (applyStatBoost)
            player.ApplyStatBoost(boostType, boostValue);

        LeviathansMarrowEffect effect = player.GetComponent<LeviathansMarrowEffect>();
        if (effect == null)
            effect = player.gameObject.AddComponent<LeviathansMarrowEffect>();
        effect.FreezeInterval = freezeInterval;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[LeviathansMarrowPickup] playerData 为 null");
            return false;
        }

        // 属性增益
        if (applyStatBoost)
            playerData.ApplyStatBoost(boostType, boostValue);

        // 添加利维坦之髓效果组件
        LeviathansMarrowEffect effect = playerData.GetComponent<LeviathansMarrowEffect>();
        if (effect == null)
        {
            effect = playerData.gameObject.AddComponent<LeviathansMarrowEffect>();
        }
        effect.FreezeInterval = freezeInterval;

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[LeviathansMarrowPickup] 玩家获得利维坦之髓！每隔 {freezeInterval} 回合冻结敌人一回合");
        Destroy(gameObject);
        return true;
    }
}
