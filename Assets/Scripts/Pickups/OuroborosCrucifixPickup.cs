using UnityEngine;

/// <summary>
/// 衔尾之十字拾取物 — 挂载在「衔尾之十字」装备 Prefab 上。
/// 玩家走到该格子时获得属性增益（可选）和「战斗开始时补齐吸血/反伤」的能力。
/// 注意：预制体上不要同时挂 StatBoostPickup，属性增益直接在本脚本配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class OuroborosCrucifixPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    [Header("衔尾之十字配置")]
    [Tooltip("较低的一项补上「较高一项」的百分之几（50 = 一半）")]
    [SerializeField] private int percentOfHigher = 50;

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
        Debug.LogWarning("[OuroborosCrucifixPickup] 衔尾之十字是效果型道具，不能出售");
        return false;
    }

    /// <summary>IPurchasable：给玩家加上衔尾之十字效果组件（重复购买覆盖参数）。</summary>
    public void ApplyPurchase(PlayerData player, int amount)
    {
        if (applyStatBoost)
            player.ApplyStatBoost(boostType, boostValue);

        OuroborosCrucifixEffect effect = player.GetComponent<OuroborosCrucifixEffect>();
        if (effect == null)
            effect = player.gameObject.AddComponent<OuroborosCrucifixEffect>();
        effect.PercentOfHigher = percentOfHigher;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[OuroborosCrucifixPickup] playerData 为 null");
            return false;
        }

        // 属性增益
        if (applyStatBoost)
            playerData.ApplyStatBoost(boostType, boostValue);

        // 添加衔尾之十字效果组件
        OuroborosCrucifixEffect effect = playerData.GetComponent<OuroborosCrucifixEffect>();
        if (effect == null)
        {
            effect = playerData.gameObject.AddComponent<OuroborosCrucifixEffect>();
        }
        effect.PercentOfHigher = percentOfHigher;

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[OuroborosCrucifixPickup] 玩家获得衔尾之十字！战斗开始时较低的一项吸血/反伤补上较高项的 {percentOfHigher}%");
        Destroy(gameObject);
        return true;
    }
}
