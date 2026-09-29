using UnityEngine;

/// <summary>
/// 神圣剑效果 — 拾取神圣剑装备时添加到玩家身上。
/// 玩家造成的魔力伤害 = 魔力输出 × 倍率（不影响魔力消耗和魔力上限）。
/// </summary>
public class HolySwordEffect : MonoBehaviour
{
    [Tooltip("魔力伤害倍率（百分比），200 = 2倍伤害")]
    [SerializeField] private int multiplierPercent = 200;

    /// <summary>魔力伤害倍率（百分比），100 = 100%（无增幅）</summary>
    public int MultiplierPercent { get => multiplierPercent; set => multiplierPercent = value; }
}
