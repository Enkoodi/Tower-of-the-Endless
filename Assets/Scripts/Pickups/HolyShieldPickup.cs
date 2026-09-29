using UnityEngine;

/// <summary>
/// 神圣盾拾取物 — 挂载在神圣盾装备 Prefab 上。
/// 玩家走到该格子时获得属性增益（可选）和免疫能力：
/// - 免疫魔力光环（MagicAuraAttack）的相邻伤害
/// - 免疫夹击（PincerAttack）的伤害
/// 注意：预制体上不要同时挂 StatBoostPickup，属性增益直接在本脚本配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class HolyShieldPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
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
        Debug.LogWarning("[HolyShieldPickup] 神圣盾是效果型道具，不能出售");
        return false;
    }


    /// <summary>IPurchasable：给玩家加上免疫组件（重复购买不叠加）。</summary>
    public void ApplyPurchase(PlayerData player, int amount)
    {
        if (player.GetComponent<HolyShieldEffect>() == null)
            player.gameObject.AddComponent<HolyShieldEffect>();
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[HolyShieldPickup] playerData 为 null");
            return false;
        }

        // 属性增益
        if (applyStatBoost)
            playerData.ApplyStatBoost(boostType, boostValue);

        // 避免重复添加免疫组件
        if (playerData.GetComponent<HolyShieldEffect>() == null)
        {
            playerData.gameObject.AddComponent<HolyShieldEffect>();
        }

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log("[HolyShieldPickup] 玩家获得神圣盾！免疫魔力光环和夹击攻击。");
        Destroy(gameObject);
        return true;
    }
}
