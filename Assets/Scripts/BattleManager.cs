using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗管理器 — 单例，驱动战斗 UI 流程。
/// 战斗时打开 BattleUI，逐行显示战斗日志，结束后回调结果。
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("UI 引用")]
    [SerializeField] private BattleUI battleUI;

    [Header("动画参数")]
    [SerializeField] private float logDelay = 0.4f;

    private bool isFighting = false;
    private System.Action<bool> onBattleEnd;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 触发一场战斗。结果通过 callback 返回：true=胜利，false=失败/逃跑。
    /// </summary>
    public void StartBattle(PlayerData playerData, EnemyController enemy, System.Action<bool> callback)
    {
        if (isFighting)
        {
            Debug.LogWarning("[BattleManager] 当前已有战斗在进行中");
            callback?.Invoke(false);
            return;
        }

        if (battleUI == null)
        {
            Debug.LogError("[BattleManager] BattleUI 未设置！请在 Inspector 中绑定");
            callback?.Invoke(false);
            return;
        }

        if (playerData == null || enemy == null || enemy.IsDefeated)
        {
            callback?.Invoke(true);
            return;
        }

        isFighting = true;
        onBattleEnd = callback;

        battleUI.OpenBattle(playerData, enemy);
        StartCoroutine(BattleCoroutine(playerData, enemy));
    }

    private IEnumerator BattleCoroutine(PlayerData playerData, EnemyController enemy)
    {
        // 起手：敌人先手
        if (enemy.FirstStrike)
        {
            battleUI.AddLog($"{enemy.EnemyName} 先手攻击！");
            yield return new WaitForSeconds(logDelay);

            int damage = playerData.TakeDamage(enemy.Attack);
            if (damage <= 0)
                battleUI.AddLog($"{enemy.EnemyName} 攻击，但被玩家完全抵挡");
            else
                battleUI.AddLog($"{enemy.EnemyName} 攻击，造成 {damage} 伤害");

            battleUI.UpdatePlayerPanel(playerData);
            yield return new WaitForSeconds(logDelay);

            if (playerData.IsDead)
            {
                EndBattle(false, playerData, enemy);
                yield break;
            }
        }
        else
        {
            battleUI.AddLog("玩家发起攻击！");
            yield return new WaitForSeconds(logDelay);
        }

        // 计算对敌伤害
        int damageToEnemy = playerData.Attack - enemy.Defense;
        if (damageToEnemy <= 0)
        {
            battleUI.AddLog($"无法破防！玩家攻击力({playerData.Attack}) <= 敌人防御力({enemy.Defense})");
            yield return new WaitForSeconds(logDelay);
            EndBattle(false, playerData, enemy);
            yield break;
        }

        while (!enemy.IsDefeated && !playerData.IsDead)
        {
            // 玩家攻击
            int damageDealt = enemy.TakeDamage(playerData.Attack);
            battleUI.AddLog($"玩家攻击，造成 {damageDealt} 伤害（敌人剩余 {enemy.HP}）");
            battleUI.UpdateEnemyPanel(enemy);
            yield return new WaitForSeconds(logDelay);

            if (enemy.HP <= 0)
            {
                battleUI.AddLog($"{enemy.EnemyName} 被击败！");
                yield return new WaitForSeconds(logDelay);
                EndBattle(true, playerData, enemy);
                yield break;
            }

            // 敌人反击
            int damageTaken = playerData.TakeDamage(enemy.Attack);
            if (damageTaken <= 0)
                battleUI.AddLog($"{enemy.EnemyName} 反击，但被玩家完全抵挡");
            else
                battleUI.AddLog($"{enemy.EnemyName} 反击，造成 {damageTaken} 伤害");

            battleUI.UpdatePlayerPanel(playerData);
            yield return new WaitForSeconds(logDelay);

            if (playerData.IsDead)
            {
                EndBattle(false, playerData, enemy);
                yield break;
            }
        }

        EndBattle(!playerData.IsDead, playerData, enemy);
    }

    private void EndBattle(bool won, PlayerData playerData, EnemyController enemy)
    {
        isFighting = false;

        if (won && enemy != null)
        {
            battleUI.AddLog($"战斗胜利！获得 {enemy.GoldReward} 金币");
            playerData.AddGold(enemy.GoldReward);
            enemy.Defeat();
        }
        else
        {
            battleUI.AddLog("战斗失败...");
        }

        StartCoroutine(CloseAfterDelay());
        onBattleEnd?.Invoke(won);
        onBattleEnd = null;
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        battleUI?.CloseBattle();
    }
}
