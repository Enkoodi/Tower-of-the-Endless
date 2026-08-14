using UnityEngine;

/// <summary>
/// 神秘商人购买脚本 — 挂载在神秘商人NPC上。
/// 与商人对话结束后，玩家点击选项触发购买（由 DialogueTrigger 的 OnChoice1/OnChoice2 绑定）。
/// 消耗金币，并按配置发放道具（可多种、每种多个）。
/// </summary>
public class MerchantPurchase : MonoBehaviour
{
    [Header("购买消耗")]
    [SerializeField] private int goldCost = 100;

    [Header("获得道具（可配置多种、每种多个）")]
    [SerializeField] private PurchaseItem[] items;

    /// <summary>单条购买奖励</summary>
    [System.Serializable]
    public class PurchaseItem
    {
        [Tooltip("道具预制体（KeyPickup / StatBoostPickup / BlessingPickup 等）")]
        public GameObject prefab;

        [Tooltip("获得数量")]
        [Min(1)] public int quantity = 1;
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

        if (!player.SpendGold(goldCost))
        {
            Debug.LogWarning($"[MerchantPurchase] 金币不足：需要 {goldCost}，当前 {player.Gold}");
            return false;
        }

        if (items != null)
        {
            foreach (PurchaseItem item in items)
            {
                GiveItem(player, item);
            }
        }

        Debug.Log($"[MerchantPurchase] 购买成功，消耗 {goldCost} 金币");
        return true;
    }

    /// <summary>根据预制体上的组件类型发放单个道具</summary>
    private void GiveItem(PlayerData player, PurchaseItem item)
    {
        if (item == null || item.prefab == null)
        {
            Debug.LogWarning("[MerchantPurchase] 道具预制体为空，跳过");
            return;
        }

        int quantity = Mathf.Max(1, item.quantity);

        // 钥匙
        KeyPickup key = item.prefab.GetComponent<KeyPickup>();
        if (key != null)
        {
            player.AddKey(key.KeyType, quantity);
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
            player.ApplyStatBoost(stat.Data.boostType, stat.Data.value * quantity);
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
            player.AddUpTeleporter(quantity);
            return;
        }

        // 下楼传送器
        FloorDownTeleporter downTeleporter = item.prefab.GetComponent<FloorDownTeleporter>();
        if (downTeleporter != null)
        {
            player.AddDownTeleporter(quantity);
            return;
        }

        Debug.LogWarning($"[MerchantPurchase] 未识别的道具类型：{item.prefab.name}");
    }
}
