using UnityEngine;

/// <summary>
/// 魔力增幅器拾取物 — 挂载在魔力增幅器装备 Prefab 上。
/// 玩家走到该格子时获得属性增益（可选）和魔力伤害增幅能力。
/// 注意：预制体上不要同时挂 StatBoostPickup，属性增益直接在本脚本配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class MagicAmplifierPickup : MonoBehaviour
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

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[MagicAmplifier] playerData 为 null");
            return false;
        }

        // 属性增益
        if (applyStatBoost)
            playerData.ApplyStatBoost(boostType, boostValue);

        // 添加魔力增幅组件
        MagicAmplifier amp = playerData.GetComponent<MagicAmplifier>();
        if (amp == null)
        {
            amp = playerData.gameObject.AddComponent<MagicAmplifier>();
        }
        amp.MultiplierPercent = multiplierPercent;

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log($"[MagicAmplifier] 玩家获得神圣剑！魔力伤害倍率 = {multiplierPercent}%");
        Destroy(gameObject);
        return true;
    }
}
