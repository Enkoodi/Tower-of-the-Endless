using UnityEngine;

/// <summary>
/// 利维坦之髓效果 — 拾取「利维坦之髓」装备时添加到玩家身上。
/// 持有时每隔固定回合数（默认 5）冻结敌人一回合：该回合敌人无法进行**主动攻击**
/// （物理 + 魔力），但反伤等**被动效果**照常。
///
/// 冻结由 BattleResolver 的 EnemyCanAct 挂钩执行，本类只回答「第 N 回合要不要冻结敌人」。
/// 回合号由结算内核维护，从 1 开始 —— 所以 5 表示第 5、10、15…回合。
/// </summary>
public class LeviathansMarrowEffect : MonoBehaviour
{
    [Tooltip("每隔多少个回合冻结敌人一回合（5 = 第 5、10、15…回合）")]
    [SerializeField] private int freezeInterval = 5;

    /// <summary>冻结间隔（回合）。最小 1（每回合都冻结）。</summary>
    public int FreezeInterval
    {
        get => Mathf.Max(1, freezeInterval);
        set => freezeInterval = Mathf.Max(1, value);
    }

    /// <summary>第 turn 回合是否冻结敌人（turn 从 1 开始）。</summary>
    public bool FreezesOnTurn(int turn) => turn > 0 && turn % FreezeInterval == 0;
}
