using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗管理器 — 单例，驱动战斗 UI 流程。
/// 战斗时打开 BattleUI，逐行显示战斗日志，结束后回调结果。
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    /// <summary>战斗开始/结束事件（供 PlayerMove 等订阅以锁定/解锁移动）</summary>
    public static event System.Action OnBattleOpen;
    public static event System.Action OnBattleClose;

    [Header("UI 引用")]
    [SerializeField] private BattleUI battleUI;

    [Header("动画参数")]
    [SerializeField] private float logDelay = 1.6f;

    private bool isFighting = false;
    private int turnCount = 1;
    private int lastDamageToEnemy;   // 本回合敌人受到的实际伤害（减伤后）
    private int lastDamageToPlayer;  // 本回合玩家受到的实际伤害（减伤后）
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

        // 使用敌人减半道具后，本场敌人血量减半（普通敌人与NPC敌人都经此入口生效）
        bool enemyHalved = playerData.ConsumeEnemyHalve();
        if (enemyHalved)
        {
            enemy.SetHP(Mathf.Max(1, enemy.HP / 2));
        }

        isFighting = true;
        turnCount = 1;
        lastDamageToEnemy = 0;
        lastDamageToPlayer = 0;
        onBattleEnd = callback;

        battleUI.OpenBattle(playerData, enemy);
        if (enemyHalved)
            battleUI.AddLog($"<color=#FFCC66>敌人血量减半</color>：{enemy.EnemyName} 生命值减半");
        battleUI.UpdateTurn(turnCount);
        BlessingManager.Instance?.OnBattleStart(playerData, enemy, battleUI);
        OnBattleOpen?.Invoke();
        StartCoroutine(BattleCoroutine(playerData, enemy));
    }

    private IEnumerator BattleCoroutine(PlayerData playerData, EnemyController enemy)
    {
        // 快照战斗前魔力，战斗中消耗，战斗结束后恢复。直接操作真实属性以同步 UI
        int playerManaSnapshot = playerData.ManaCharge;
        int enemyManaSnapshot = enemy.ManaCharge;

        // 物理伤害（仅依赖攻击/防御/段数，每轮不变）
        int playerPhysical = Mathf.Max(0, (playerData.Attack - enemy.Defense) * playerData.AttackCount);
        int enemyPhysical = Mathf.Max(0, (enemy.Attack - playerData.Defense) * enemy.AttackCount);

        // 每轮生成的临时变量
        int ManaCost, damageToEnemy, EnemyManaCost, enemyDamageToPlayer;
        string playerDmgStr, enemyDmgStr;

        // 根据真实魔力（可读写属性）计算一轮伤害
        void ComputeRoundDamage()
        {
            ManaCost = playerData.ManaCharge < playerData.ManaMax ? playerData.ManaCharge : playerData.ManaMax;

            // 魔力增幅（拾取魔力增幅器后生效）
            MagicAmplifier amp = playerData.GetComponent<MagicAmplifier>();
            int playerMagicDamage = amp != null ? ManaCost * amp.MultiplierPercent / 100 : ManaCost;

            damageToEnemy = playerPhysical + playerMagicDamage;

            EnemyManaCost = enemy.ManaCharge < enemy.ManaMax ? enemy.ManaCharge : enemy.ManaMax;
            enemyDamageToPlayer = enemyPhysical + EnemyManaCost;

            playerDmgStr = FormatDamage(playerData.AttackCount, playerPhysical, ManaCost, damageToEnemy);
            enemyDmgStr = FormatDamage(enemy.AttackCount, enemyPhysical, EnemyManaCost, enemyDamageToPlayer);
        }

        ComputeRoundDamage();

        // 先手判定：谁速度高谁先手
        if (enemy.Speed > playerData.Speed)
        {
            int sneakDamage = enemyDamageToPlayer * 2;
            battleUI.AddLog($"受到<color=#779977>{enemy.EnemyName}</color>偷袭！");
            yield return new WaitForSeconds(logDelay);

            int actualSneak = playerData.SubtractHP(sneakDamage);
            lastDamageToPlayer = actualSneak;
            if (actualSneak <= 0)
                battleUI.AddLog($"偷袭似乎不起作用");
            else
                battleUI.AddLog($"<color=#7799CC>玩家</color>受到 <color=#FF4444>{actualSneak}</color> 点伤害");

            battleUI.UpdatePlayerPanel(playerData);
            yield return new WaitForSeconds(logDelay);

            if (playerData.IsDead)
            {
                EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }
        }
        else
        {
            yield return new WaitForSeconds(logDelay);
        }
        if (damageToEnemy <= 0)
        {
            battleUI.AddLog($"<color=#7799CC>玩家</color>攻击，造成 {playerDmgStr} 点伤害");
            yield return new WaitForSeconds(logDelay);
            EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
            yield break;
        }

        while (!enemy.IsDefeated && !playerData.IsDead)
        {
            // —— 特殊祝福生命周期：回合开始 ——
            BlessingManager.Instance?.OnTurnStart(playerData, enemy, battleUI);

            // 检查深渊等 Effect 是否提前杀死敌人
            if (enemy.HP <= 0)
            {
                battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                yield return new WaitForSeconds(logDelay);
                EndBattle(true, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }

            // 玩家攻击
            int actualToEnemy = enemy.TakeRawDamage(damageToEnemy);
            lastDamageToEnemy = actualToEnemy;
            battleUI.AddLog($"<color=#7799CC>玩家</color>攻击，造成 <color=#FF4444>{actualToEnemy} </color>点伤害");
            battleUI.UpdateEnemyPanel(enemy);
            BlessingManager.Instance?.OnPlayerDealDamage(playerData, enemy, battleUI, actualToEnemy);
            BlessingManager.Instance?.OnEnemyTakeDamage(playerData, enemy, battleUI, actualToEnemy);

            // 消耗魔力 + 吸血 + 反伤
            if (BlessingManager.Instance?.ShouldConsumeMana(playerData) ?? true)
            {
                playerData.ManaCharge -= ManaCost;
            }
            else
            {
                battleUI.AddLog($"<color=#88CCFF>灵知的祝福</color>：本回合不消耗魔力充能");
            }
            int steal = playerPhysical * playerData.LifeSteal / 100;
            if (steal > 0) playerData.Heal(steal);
            // 反伤仅在敌人未因本次攻击死亡时触发（吸血已先结算）
            if (enemy.HP > 0)
            {
                int reflect = playerPhysical * enemy.ReflectDamage / 100;
                if (reflect > 0) playerData.SubtractHP(reflect);
            }
            ComputeRoundDamage();

            battleUI.UpdatePlayerPanel(playerData);
            yield return new WaitForSeconds(logDelay);

            if (enemy.HP <= 0)
            {
                battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                yield return new WaitForSeconds(logDelay);
                EndBattle(true, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }

            // 敌人反击
            int actualToPlayer = playerData.SubtractHP(enemyDamageToPlayer);
            lastDamageToPlayer = actualToPlayer;
            battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 反击，造成 <color=#FF4444>{actualToPlayer}</color> 点伤害");
            BlessingManager.Instance?.OnEnemyDealDamage(playerData, enemy, battleUI, actualToPlayer);
            BlessingManager.Instance?.OnPlayerTakeDamage(playerData, enemy, battleUI, actualToPlayer);

            // 消耗魔力 + 敌人吸血 + 玩家反伤
            enemy.ManaCharge -= EnemyManaCost;
            int enemySteal = enemyPhysical * enemy.LifeSteal / 100;
            if (enemySteal > 0) enemy.Heal(enemySteal);
            // 反伤仅在玩家未因本次伤害死亡时触发（吸血已先结算）
            if (playerData.HP > 0)
            {
                int playerReflect = enemyPhysical * playerData.ReflectDamage / 100;
                if (playerReflect > 0) enemy.TakeRawDamage(playerReflect);
            }
            ComputeRoundDamage();

            battleUI.UpdateEnemyPanel(enemy);
            battleUI.UpdatePlayerPanel(playerData);
            yield return new WaitForSeconds(logDelay);

            if (playerData.IsDead)
            {
                EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }

            turnCount++;
            battleUI.UpdateTurn(turnCount);
            BlessingManager.Instance?.OnTurnEnd(playerData, enemy, battleUI);
        }

        EndBattle(!playerData.IsDead, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
    }

    private void EndBattle(bool won, PlayerData playerData, EnemyController enemy, int playerManaSnapshot, int enemyManaSnapshot)
    {
        isFighting = false;
        lastDamageToEnemy = 0;
        lastDamageToPlayer = 0;

        // 特殊祝福生命周期：战斗结束（恢复加攻等）
        BlessingManager.Instance?.OnBattleEnd(playerData, enemy, battleUI, won);

        // 恢复魔力到战斗前
        playerData.ManaCharge = playerManaSnapshot;
        if (enemy != null)
            enemy.ManaCharge = enemyManaSnapshot;

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
        OnBattleClose?.Invoke();
        onBattleEnd?.Invoke(won);
        onBattleEnd = null;
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        battleUI?.CloseBattle();
    }

    private static string FormatDamage(int attackCount, int physicalDamage, int manaCost, int totalDamage)
    {
        int phys = Mathf.Max(0, physicalDamage);
        if (attackCount > 1 && manaCost > 0)
            return $"<color=#FFDD88>{attackCount}</color>×<color=#FF4444>{phys}</color>+<color=#7777AA>{manaCost}</color>";
        if (attackCount > 1)
            return $"<color=#FFDD88>{attackCount}</color>×<color=#FF4444>{phys}</color>";
        if (manaCost > 0)
            return $"<color=#FF4444>{phys}</color>+<color=#7777AA>{manaCost}</color>";
        return $"<color=#FF4444>{totalDamage}</color>";
    }
}
