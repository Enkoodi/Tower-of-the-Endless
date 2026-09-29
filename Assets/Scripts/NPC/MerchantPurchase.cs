using UnityEngine;

/// <summary>
/// 神秘商人交易脚本 — 挂载在神秘商人NPC上。
/// 与商人对话结束后，玩家点击选项触发交易（由 DialogueTrigger 的 OnChoice1/OnChoice2 绑定）。
/// 金币为正时消耗金币、为负时获得金币；道具数量为正时发放、为负时出售（回收）。
///
/// 道具一律按 <see cref="IPurchasable"/> 接口取 —— 不再逐个类型枚举。
/// 新增可交易道具只要让它实现该接口，本脚本一行都不用改。
/// 想确认某个道具能不能卖，看它实现的 <c>CanSell</c>。
/// </summary>
public class MerchantPurchase : MonoBehaviour
{
    [Header("金币（正=消耗，负=获得）")]
    [SerializeField] private int goldCost = 100;

    [Header("获得道具（可配置多种、每种多个）")]
    [SerializeField] private PurchaseItem[] items;

    /// <summary>单条购买奖励</summary>
    [System.Serializable]
    public class PurchaseItem
    {
        [Tooltip("道具预制体（需实现 IPurchasable；如 KeyPickup / AlePickup / ManaEssencePickup 等）")]
        public GameObject prefab;

        [Tooltip("数量：正=发放，负=出售（回收）")]
        public int quantity = 1;
    }

    /// <summary>供 UnityEvent 绑定的无参入口</summary>
    public void TryPurchase()
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null)
        {
            Debug.LogError("[MerchantPurchase] 未找到 PlayerData");
            return;
        }

        TryPurchase(player);
    }

    /// <summary>执行购买：扣金币并发放道具。返回是否购买成功。</summary>
    public bool TryPurchase(PlayerData player)
    {
        if (player == null)
        {
            Debug.LogError("[MerchantPurchase] player 为 null");
            return false;
        }

        // 出售前先校验玩家是否持有足够道具
        if (!ValidateItems(player))
        {
            return false;
        }

        // 金币结算：正=消耗，负=获得
        if (goldCost >= 0)
        {
            if (!player.SpendGold(goldCost))
            {
                Debug.LogWarning($"[MerchantPurchase] 金币不足：需要 {goldCost}，当前 {player.Gold}");
                return false;
            }
        }
        else
        {
            // 出售获得的金币为固定值，不受金币系数影响
            player.SetGold(player.Gold - goldCost);
        }

        if (items != null)
        {
            foreach (PurchaseItem item in items)
            {
                ApplyItem(player, item);
            }
        }

        Debug.Log($"[MerchantPurchase] 交易成功（金币变动 {goldCost}，当前 {player.Gold}）");
        return true;
    }

    /// <summary>
    /// 交易前校验：每个条目都必须实现 IPurchasable；出售项还要保证玩家持有足够数量。
    ///
    /// ⚠️ 这个校验必须**同时覆盖发放方向**，不能只管出售 ——
    /// 因为 TryPurchase 是「先扣金币、再 ApplyItem」，只要有一条发放不了，
    /// 玩家就是「金币照扣、东西不给」。历史上漏掉麦酒 / 魔力精华 / 圣水时正是这个表现。
    /// </summary>
    private bool ValidateItems(PlayerData player)
    {
        if (items == null) return true;

        foreach (PurchaseItem item in items)
        {
            if (item == null || item.prefab == null) continue;

            if (!item.prefab.TryGetComponent(out IPurchasable purchasable))
            {
                Debug.LogWarning($"[MerchantPurchase] {item.prefab.name} 未实现 IPurchasable，无法交易");
                return false;
            }

            // 只有出售方向才需要校验持有量；发放方向由实现自己保证
            if (item.quantity < 0 && !purchasable.CanSell(player, -item.quantity))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 按预制体上的 IPurchasable 实现发放或回收单个道具。
    /// 原先这里是一张手写的组件清单，新增道具忘了同步就会静默「扣钱不给货」。
    /// </summary>
    private void ApplyItem(PlayerData player, PurchaseItem item)
    {
        if (item == null || item.prefab == null)
        {
            Debug.LogWarning("[MerchantPurchase] 道具预制体为空，跳过");
            return;
        }

        if (item.quantity == 0) return;

        if (!item.prefab.TryGetComponent(out IPurchasable purchasable))
        {
            Debug.LogWarning($"[MerchantPurchase] {item.prefab.name} 未实现 IPurchasable，跳过");
            return;
        }

        purchasable.ApplyPurchase(player, item.quantity);
    }
}
