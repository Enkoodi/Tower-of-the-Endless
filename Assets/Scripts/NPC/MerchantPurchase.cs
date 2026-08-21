using UnityEngine;

/// <summary>
/// 神秘商人交易脚本 — 挂载在神秘商人NPC上。
/// 与商人对话结束后，玩家点击选项触发交易（由 DialogueTrigger 的 OnChoice1/OnChoice2 绑定）。
/// 金币为正时消耗金币、为负时获得金币；道具数量为正时发放、为负时出售（回收）。
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
        [Tooltip("道具预制体（KeyPickup / StatBoostPickup / BlessingPickup 等）")]
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

    /// <summary>交易前校验：出售项需保证玩家持有足够数量。</summary>
    private bool ValidateItems(PlayerData player)
    {
        if (items == null) return true;

        foreach (PurchaseItem item in items)
        {
            if (item == null || item.prefab == null || item.quantity >= 0)
                continue;

            int sellAmount = -item.quantity;

            // 钥匙
            KeyPickup key = item.prefab.GetComponent<KeyPickup>();
            if (key != null)
            {
                int owned = player.GetKeyCount(key.KeyType);
                if (owned < sellAmount)
                {
                    Debug.LogWarning($"[MerchantPurchase] {key.KeyType} 钥匙不足：需要出售 {sellAmount}，当前 {owned}");
                    return false;
                }
                continue;
            }

            // 上楼传送器
            FloorUpTeleporter upTeleporter = item.prefab.GetComponent<FloorUpTeleporter>();
            if (upTeleporter != null)
            {
                if (player.UpTeleporterCount < sellAmount)
                {
                    Debug.LogWarning($"[MerchantPurchase] 上楼传送器不足：需要出售 {sellAmount}，当前 {player.UpTeleporterCount}");
                    return false;
                }
                continue;
            }

            // 下楼传送器
            FloorDownTeleporter downTeleporter = item.prefab.GetComponent<FloorDownTeleporter>();
            if (downTeleporter != null)
            {
                if (player.DownTeleporterCount < sellAmount)
                {
                    Debug.LogWarning($"[MerchantPurchase] 下楼传送器不足：需要出售 {sellAmount}，当前 {player.DownTeleporterCount}");
                    return false;
                }
                continue;
            }

            // 属性增益、祝福等非数量型道具无法出售
            Debug.LogWarning($"[MerchantPurchase] {item.prefab.name} 为非数量型道具，无法出售");
            return false;
        }

        return true;
    }

    /// <summary>根据预制体上的组件类型发放或回收单个道具</summary>
    private void ApplyItem(PlayerData player, PurchaseItem item)
    {
        if (item == null || item.prefab == null)
        {
            Debug.LogWarning("[MerchantPurchase] 道具预制体为空，跳过");
            return;
        }

        int quantity = item.quantity;
        if (quantity == 0) return;

        bool selling = quantity < 0;
        int amount = Mathf.Abs(quantity);

        // 钥匙
        KeyPickup key = item.prefab.GetComponent<KeyPickup>();
        if (key != null)
        {
            player.AddKey(key.KeyType, selling ? -amount : amount);
            return;
        }

        // 属性增益
        StatBoostPickup stat = item.prefab.GetComponent<StatBoostPickup>();
        if (stat != null)
        {
            if (stat.Data == null)
            {
                Debug.LogWarning($"[MerchantPurchase] {item.prefab.name} 的 StatBoostData 未设置");
                return;
            }
            player.ApplyStatBoost(stat.Data.boostType, stat.Data.value * amount);
            return;
        }

        // 祝福（弹出选择面板，数量不适用）
        BlessingPickup blessing = item.prefab.GetComponent<BlessingPickup>();
        if (blessing != null)
        {
            BlessingManager manager = BlessingManager.Instance;
            if (manager != null)
            {
                manager.ShowWithPool(player, blessing.OverridePool);
            }
            else
            {
                Debug.LogError("[MerchantPurchase] BlessingManager 不存在");
            }
            return;
        }

        // 上楼传送器
        FloorUpTeleporter upTeleporter = item.prefab.GetComponent<FloorUpTeleporter>();
        if (upTeleporter != null)
        {
            player.AddUpTeleporter(selling ? -amount : amount);
            return;
        }

        // 下楼传送器
        FloorDownTeleporter downTeleporter = item.prefab.GetComponent<FloorDownTeleporter>();
        if (downTeleporter != null)
        {
            player.AddDownTeleporter(selling ? -amount : amount);
            return;
        }

        Debug.LogWarning($"[MerchantPurchase] 未识别的道具类型：{item.prefab.name}");
    }
}
