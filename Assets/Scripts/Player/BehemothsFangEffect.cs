using UnityEngine;

/// <summary>
/// 贝希摩斯之牙效果 — 拾取「贝希摩斯之牙」装备时添加到玩家身上。
/// 持有时每次**主动攻击**（不论是否破防）给敌人叠一层「撕裂」（Rend），最多 <see cref="MaxStacks"/> 层；
/// 每回合开始时，敌人受到「玩家实时攻击力 × 层数 × 每层百分比 / 100」的**真实伤害**
/// （无视防御 / 无视减伤 / 不触发反伤 / 可以致死）。
///
/// 叠层与结算都在 BattleResolver 里（和冻结一样，实战与图鉴共用同一份），本类只提供参数。
/// </summary>
public class BehemothsFangEffect : MonoBehaviour
{
    [Tooltip("每次主动攻击叠加的撕裂层数")]
    [SerializeField] private int stacksPerAttack = 1;

    [Tooltip("撕裂层数上限")]
    [SerializeField] private int maxStacks = 10;

    [Tooltip("每层撕裂按「玩家实时攻击力」的百分之几结算真实伤害（1 = 1%）")]
    [SerializeField] private int percentPerStack = 1;

    /// <summary>每次主动攻击叠加的层数。</summary>
    public int StacksPerAttack
    {
        get => Mathf.Max(0, stacksPerAttack);
        set => stacksPerAttack = Mathf.Max(0, value);
    }

    /// <summary>撕裂层数上限。</summary>
    public int MaxStacks
    {
        get => Mathf.Max(0, maxStacks);
        set => maxStacks = Mathf.Max(0, value);
    }

    /// <summary>每层按攻击力的百分之几结算。</summary>
    public int PercentPerStack
    {
        get => Mathf.Max(0, percentPerStack);
        set => percentPerStack = Mathf.Max(0, value);
    }

    /// <summary>打包成结算内核用的参数（StacksPerAttack 或 MaxStacks 为 0 = 整套机制不启用）。</summary>
    public RendSpec ToRendSpec() => new RendSpec
    {
        StacksPerAttack = StacksPerAttack,
        MaxStacks = MaxStacks,
        PercentPerStack = PercentPerStack,
    };
}
