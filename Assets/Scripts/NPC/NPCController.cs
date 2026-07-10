using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// NPC控制器 — 挂载在NPC Prefab上（商店NPC）。
/// 玩家移动碰撞检测到NPC时，停止移动并打开商店界面。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class NPCController : MonoBehaviour
{
    [Header("NPC基本信息")]
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private Sprite npcSprite;

    [Header("百分比增益（增加当前属性的百分比）")]
    [SerializeField] private int hpPercent = 10;     // +X% 当前生命值
    [SerializeField] private int atkPercent = 10;    // +X% 当前攻击力
    [SerializeField] private int defPercent = 10;    // +X% 当前防御力

    [Header("祝福")]
    [SerializeField] private BlessingPool blessingPool;

    // ============================================================
    //  全局购买计数（所有商店共通）
    // ============================================================

    private static Dictionary<string, int> purchaseCounts = new Dictionary<string, int>();
    private const string GLOBAL_PURCHASE_KEY = "GlobalShop";

    /// <summary>价格基础值</summary>
    private const int BASE_COST = 200;
    /// <summary>每次购买后价格增加量</summary>
    private const int COST_INCREMENT = 200;

    /// <summary>获取全局购买次数</summary>
    public static int GetGlobalPurchaseCount()
    {
        purchaseCounts.TryGetValue(GLOBAL_PURCHASE_KEY, out int count);
        return count;
    }

    /// <summary>计算当前购买价格（所有选项统一）</summary>
    public static int GetCurrentCost()
    {
        return BASE_COST + GetGlobalPurchaseCount() * COST_INCREMENT;
    }

    /// <summary>增加全局购买次数</summary>
    public static void IncrementPurchaseCount()
    {
        purchaseCounts.TryGetValue(GLOBAL_PURCHASE_KEY, out int count);
        purchaseCounts[GLOBAL_PURCHASE_KEY] = count + 1;
    }

    // ============================================================
    //  运行时字段
    // ============================================================

    /// <summary>在地图网格中的坐标（由MapGenerator在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由MapGenerator在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    // ============================================================
    //  公开属性
    // ============================================================

    public string NPCName => npcName;
    public int HpPercent  => hpPercent;
    public int AtkPercent => atkPercent;
    public int DefPercent => defPercent;
    public BlessingPool GetBlessingPool() => blessingPool;

    // ============================================================
    //  生命周期
    // ============================================================

    private void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (npcSprite != null && sr != null)
            sr.sprite = npcSprite;
    }
}
