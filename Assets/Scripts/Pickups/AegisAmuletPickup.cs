using UnityEngine;

/// <summary>
/// 护身符装备拾取物 — 挂载在护身符装备 Prefab 上。
/// 玩家走到该格子时获得属性增益（可选）和免疫能力：
/// - 免疫魔力光环（MagicAuraAttack）的相邻伤害
/// - 免疫夹击（PincerAttack）的伤害
/// 注意：预制体上不要同时挂 StatBoostPickup，属性增益直接在本脚本配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class AegisAmuletPickup : MonoBehaviour
{
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
            Debug.LogError("[AegisAmulet] playerData 为 null");
            return false;
        }

        // 属性增益
        if (applyStatBoost)
            playerData.ApplyStatBoost(boostType, boostValue);

        // 避免重复添加免疫组件
        if (playerData.GetComponent<PlayerImmunity>() == null)
        {
            playerData.gameObject.AddComponent<PlayerImmunity>();
        }

        // 记录到楼层记忆中
        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkItemPickedUp(gridPosition);

        // 通知 DropManager 移除此位置的活跃掉落记录
        DropManager.Instance?.MarkDropPickedUp(floorNumber, gridPosition);

        Debug.Log("[AegisAmulet] 玩家获得神圣盾！免疫魔力光环和夹击攻击。");
        Destroy(gameObject);
        return true;
    }
}
