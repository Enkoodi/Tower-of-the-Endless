using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 祝福池 — 右键 Create → MagicTower → Blessing Pool 创建 .asset。
/// 存放所有可获取的祝福列表，提供随机抽取方法。
/// </summary>
[CreateAssetMenu(fileName = "NewBlessingPool", menuName = "MagicTower/Blessing Pool")]
public class BlessingPool : ScriptableObject
{
    [Tooltip("池中所有祝福")]
    public List<BlessingData> blessings;

    [Header("稀有度权重")]
    [Range(0, 100)] public int commonWeight    = 50;
    [Range(0, 100)] public int rareWeight      = 30;
    [Range(0, 100)] public int epicWeight      = 15;
    [Range(0, 100)] public int legendaryWeight = 5;

    /// <summary>
    /// 从池中随机抽取 count 个不重复的祝福。
    /// 先按权重决定稀有度，再从该稀有度池中随机选。
    /// </summary>
    public List<BlessingData> Draw(int count = 3)
    {
        List<BlessingData> results = new List<BlessingData>();
        List<BlessingData> poolCopy = new List<BlessingData>(blessings);

        if (poolCopy.Count == 0)
        {
            Debug.LogWarning("[BlessingPool] 池为空！");
            return results;
        }

        for (int i = 0; i < count && poolCopy.Count > 0; i++)
        {
            BlessingRarity targetRarity = RollRarity();

            // 找出该稀有度的候选
            List<BlessingData> candidates = poolCopy.FindAll(b => b.rarity == targetRarity);
            if (candidates.Count == 0)
            {
                // 降级：重新投骰子，直到抽到有候选的稀有度（避免退回权重为0的稀有度）
                int safety = 0;
                while (candidates.Count == 0 && safety < 100)
                {
                    targetRarity = RollRarity();
                    candidates = poolCopy.FindAll(b => b.rarity == targetRarity);
                    safety++;
                }
                // 最终还是为空时，才从剩余全池抽（但排除权重为0的稀有度）
                if (candidates.Count == 0)
                    candidates = GetFilteredPool(poolCopy);
            }

            BlessingData picked = candidates[Random.Range(0, candidates.Count)];
            poolCopy.Remove(picked);
            results.Add(picked);
        }

        return results;
    }

    /// <summary>
    /// 从池中筛掉权重为0的稀有度，作为最终降级兜底。
    /// </summary>
    private List<BlessingData> GetFilteredPool(List<BlessingData> source)
    {
        List<BlessingData> filtered = new List<BlessingData>();
        foreach (var b in source)
        {
            if (b.rarity == BlessingRarity.Common    && commonWeight    <= 0) continue;
            if (b.rarity == BlessingRarity.Rare      && rareWeight      <= 0) continue;
            if (b.rarity == BlessingRarity.Epic      && epicWeight      <= 0) continue;
            if (b.rarity == BlessingRarity.Legendary && legendaryWeight <= 0) continue;
            filtered.Add(b);
        }
        return filtered;
    }

    private BlessingRarity RollRarity()
    {
        int total = commonWeight + rareWeight + epicWeight + legendaryWeight;
        if (total <= 0) return BlessingRarity.Common;

        int roll = Random.Range(0, total);

        roll -= commonWeight;
        if (roll < 0) return BlessingRarity.Common;

        roll -= rareWeight;
        if (roll < 0) return BlessingRarity.Rare;

        roll -= epicWeight;
        if (roll < 0) return BlessingRarity.Epic;

        return BlessingRarity.Legendary;
    }
}
