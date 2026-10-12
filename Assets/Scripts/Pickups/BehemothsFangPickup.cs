using UnityEngine;

/// <summary>
/// 贝希摩斯之牙拾取物 — 挂载在「贝希摩斯之牙」装备 Prefab 上。
/// 玩家走到该格子时获得属性增益（可选）和「攻击叠撕裂」的能力。
/// 注意：预制体上不要同时挂 StatBoostPickup，属性增益直接在本脚本配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class BehemothsFangPickup : MonoBehaviour, IFloorPickup, IPurchasable
{
    [Header("撕裂配置")]
    [Tooltip("每次主动攻击叠加的撕裂层数")]
    [SerializeField] private int stacksPerAttack = 1;

    [Tooltip("撕裂层数上限")]
    [SerializeField] private int maxStacks = 10;

    [Tooltip("每层撕裂按「玩家实时攻击力」的百分之几结算真实伤害（1 = 1%）")]
    [SerializeField] private int percentPerStack = 1;

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

    /// <summary>玩家日志中的显示名。</summary>
    public string LogName => "贝希摩斯之牙";

    /// <summary>IPurchasable：效果型道具，不能出售。</summary>
    public bool CanSell(PlayerData player, int sellAmount)
    {
        Debug.LogWarning("[BehemothsFangPickup] 贝希摩斯之牙是效果型道具，不能出售");
        return false;
    }

    /// <summary>IPurchasable：给玩家加上撕裂效果组件（重复购买覆盖参数）。</summary>
    public void ApplyPurchase(PlayerData player, int amount)
    {
        if (applyStatBoost)
            player.ApplyStatBoost(boostType, boostValue);

        BehemothsFangEffect effect = player.GetComponent<BehemothsFangEffect>();
        if (effect == null)
            effect = player.gameObject.AddComponent<BehemothsFangEffect>();
        ApplyConfig(effect);
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[BehemothsFangPickup] playerData 为 null");
            return false;
        }

        // 属性增益
        if (applyStatBoost)
            playerData.ApplyStatBoost(boostType, boostValue);

        // 添加贝希摩斯之牙效果组件
        BehemothsFangEffect effect = playerData.GetComponent<BehemothsFangEffect>();
        if (effect == null)
        {
            effect = playerData.gameObject.AddComponent<BehemothsFangEffect>();
        }
        ApplyConfig(effect);

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[BehemothsFangPickup] 玩家获得贝希摩斯之牙！每次攻击叠 {stacksPerAttack} 层撕裂" +
                  $"（上限 {maxStacks} 层，每层按攻击力 {percentPerStack}% 结算真伤）");
        Destroy(gameObject);
        return true;
    }

    private void ApplyConfig(BehemothsFangEffect effect)
    {
        effect.StacksPerAttack = stacksPerAttack;
        effect.MaxStacks = maxStacks;
        effect.PercentPerStack = percentPerStack;
    }
}
