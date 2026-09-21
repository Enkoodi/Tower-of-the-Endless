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
    [SerializeField] private float logDelay = 1f;
    [Tooltip("角色动画/敌人特效的最大加速倍率（跳过档也会被压到这个上限）")]
    [SerializeField] private float maxPlaybackSpeed = 4f;
    [Tooltip("日志间隔小于等于该值时视为「跳过」档：战斗直接结算，不打开战斗界面（设置里的跳过档为 0.01）")]
    [SerializeField] private float skipDelayThreshold = 0.05f;

    /// <summary>
    /// 单场战斗的最大回合数。双方都打不动对方时（例如敌人减伤 100%、我方攻击被完全抵消，
    /// 而敌人也打不出伤害）循环永远不会退出 —— 正常档只是拖时间，跳过档会一帧跑完直接卡死编辑器。
    /// 图鉴的模拟也引用这个值，保证两边对「打不完」的判定一致。
    /// </summary>
    public const int MaxTurnCount = 200;

    /// <summary>
    /// 当前战斗表现倍速：由战斗日志间隔反推。
    /// 设置里四档对应的间隔是 正常=1、两倍=0.5、四倍=0.25、跳过=0.01，
    /// 所以 1/logDelay 就是倍速；跳过档太大，压到 maxPlaybackSpeed。
    /// </summary>
    public float PlaybackSpeed
    {
        get
        {
            if (logDelay <= 0f) return 1f;
            return Mathf.Clamp(1f / logDelay, 1f, Mathf.Max(1f, maxPlaybackSpeed));
        }
    }

    /// <summary>
    /// 是否处于「跳过」档：战斗只结算结果，不打开战斗界面，也不播任何动画/特效。
    /// 用阈值判断而非精确比较，避免存档里的浮点误差；正常/两倍/四倍档（1 / 0.5 / 0.25）都远在阈值之上。
    /// </summary>
    public bool IsSkipMode => logDelay <= skipDelayThreshold;

    /// <summary>跨场景缓存的战斗日志间隔（供设置界面在 BattleManager 尚未创建时也能记录）。</summary>
    private static float logDelayOverride = -1f;

    /// <summary>战斗日志逐行间隔（秒）。设置界面据此调整战斗速度。</summary>
    public float LogDelay
    {
        get => logDelay;
        set
        {
            logDelay = value;
            logDelayOverride = value;

            // 战斗中途改速度时，表现倍速也立刻跟上
            if (battleUI != null)
                battleUI.PlaybackSpeed = PlaybackSpeed;
        }
    }

    private bool isFighting = false;
    /// <summary>本场战斗是否走「跳过」通道。每次 StartBattle 都会重新赋值，不需要在别处复位。</summary>
    private bool silentBattle = false;

    /// <summary>战斗是否正在进行（战斗窗口打开时为 true），供按键输入锁定使用。</summary>
    public bool IsFighting => isFighting;

    private int turnCount = 1;
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

        // 进入游戏时从全局存档读取战斗速度；跨场景缓存作为回退
        float saved = SaveManager.LoadBattleSpeed();
        if (saved > 0f)
            logDelay = saved;
        else if (logDelayOverride > 0f)
            logDelay = logDelayOverride;
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

        // 从设置场景返回后，序列化的 battleUI 引用会因原场景对象销毁而变空，这里重新查找
        if (battleUI == null)
        {
            battleUI = FindFirstObjectByType<BattleUI>(FindObjectsInactive.Include);
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
        silentBattle = IsSkipMode;   // 跳过档：只结算，不打开界面、不播动画
        turnCount = 1;
        onBattleEnd = callback;

        // 快照战斗前魔力，战斗中消耗，战斗结束后恢复。
        // ⚠️ 必须在 BeginBattleStats / BlessingManager.OnBattleStart 之前取：
        //    真理的祝福会在 OnBattleStart 里把魔力 ×1.25，如果快照放在那之后，
        //    取到的就是加成后的值，战斗结束"恢复"会把加成固化成新的基准 → 每打一场滚一次雪球。
        int playerManaSnapshot = playerData.ManaCharge;
        int enemyManaSnapshot = enemy.ManaCharge;

        // 冻结战斗前数值：此后战斗中一切属性改动只写修正层，伤害和面板读的都是实时值。
        // 必须在 BlessingManager.OnBattleStart 之前 —— 战斗开始触发的祝福（如异乡人）也要写进修正层。
        playerData.BeginBattleStats();

        // 动画/特效倍速与日志节奏保持一致（正常=1、两倍=2、四倍=4）
        battleUI.PlaybackSpeed = PlaybackSpeed;
        // 跳过档不打开战斗窗口：下面的日志/面板更新会照常写进没显示的 UI，不影响结算结果
        if (silentBattle)
            Debug.Log($"[BattleManager] 跳过档：直接结算与 {enemy.EnemyName} 的战斗（不打开战斗界面）");
        else
            battleUI.OpenBattle(playerData, enemy);
        if (enemyHalved)
            battleUI.AddLog($"<color=#FFCC66>敌人血量减半</color>：{enemy.EnemyName} 生命值减半");
        battleUI.UpdateTurn(turnCount);
        BlessingManager.Instance?.OnBattleStart(playerData, enemy, battleUI);
        OnBattleOpen?.Invoke();
        StartCoroutine(BattleCoroutine(playerData, enemy, playerManaSnapshot, enemyManaSnapshot));
    }

    private IEnumerator BattleCoroutine(PlayerData playerData, EnemyController enemy,
                                        int playerManaSnapshot, int enemyManaSnapshot)
    {
        // 物理伤害也在 ComputeRoundDamage 里重算（战斗中的加攻/加段/降防要即时体现）
        int playerPhysical = 0, enemyPhysical = 0;

        // 每轮生成的临时变量
        int ManaCost, damageToEnemy, EnemyManaCost, enemyDamageToPlayer;
        string playerDmgStr, enemyDmgStr;

        // 根据实时属性计算一轮伤害。
        // 物理部分每轮重取 —— Attack / AttackCount / Defense 读的是 PlayerData 的战斗时实时值，
        // 战斗内修正层（爱的祝福加攻、朗基努斯加段等）都在这里被动生效。
        // 魔力部分本来就要重算，因为魔力充能每回合会被消耗。
        void ComputeRoundDamage()
        {
            playerPhysical = Mathf.Max(0, (playerData.Attack - enemy.Defense) * playerData.AttackCount);
            enemyPhysical  = Mathf.Max(0, (enemy.Attack - playerData.Defense) * enemy.AttackCount);

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
            if (!silentBattle) yield return new WaitForSeconds(logDelay);

            int actualSneak = playerData.SubtractHP(sneakDamage);
            // 被偷袭算受伤
            if (!silentBattle && actualSneak > 0)
                playerData.PlayHurtAnimation();
            if (actualSneak <= 0)
                battleUI.AddLog($"偷袭似乎不起作用");
            else
                battleUI.AddLog($"<color=#7799CC>玩家</color>受到 <color=#FF4444>{actualSneak}</color> 点伤害");

            battleUI.UpdatePlayerPanel(playerData);
            if (!silentBattle) yield return new WaitForSeconds(logDelay);

            if (playerData.IsDead)
            {
                EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }
        }
        else
        {
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
        }
        if (damageToEnemy <= 0)
        {
            battleUI.AddLog($"<color=#7799CC>玩家</color>攻击，造成 {playerDmgStr} 点伤害");
            playerData.PlayAttackAnimation();
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
            EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
            yield break;
        }

        while (!enemy.IsDefeated && !playerData.IsDead)
        {
            // —— 特殊祝福生命周期：回合开始 ——
            BlessingManager.Instance?.OnTurnStart(playerData, enemy, battleUI);

            // 回合开始时生效的加攻/加段/减伤已写进修正层，本回合伤害立刻按最新实时数值重算
            ComputeRoundDamage();

            // 检查深渊等 Effect 是否提前杀死敌人
            if (enemy.HP <= 0)
            {
                battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                if (!silentBattle) yield return new WaitForSeconds(logDelay);
                EndBattle(true, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }

            // 玩家攻击
            int actualToEnemy = enemy.TakeRawDamage(damageToEnemy);
            playerData.PlayAttackAnimation();
            // 敌人受击表现：等头像冲到敌人面前才抖一下并叠出斩击（时序在 BattleUI 内控制）
            if (actualToEnemy > 0)
                battleUI.PlayEnemyHit(playerData.AttackCount);
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
            // 吸血 = 真实物理伤害 × 吸血系数，精确回血（不乘 hpMultiplier）
            int steal = playerPhysical * playerData.LifeSteal / 100;
            if (steal > 0) playerData.HealRaw(steal);
            // 反伤仅在敌人未因本次攻击死亡时触发（吸血已先结算）
            // 反伤是玩家自己攻击的代价，不算「被攻击」，不播受伤动画
            if (enemy.HP > 0)
            {
                int reflect = playerPhysical * enemy.ReflectDamage / 100;
                if (reflect > 0) playerData.SubtractHP(reflect);
            }
            ComputeRoundDamage();

            battleUI.UpdatePlayerPanel(playerData);
            if (!silentBattle) yield return new WaitForSeconds(logDelay);

            // 反伤也可能把玩家打死：这里必须立刻收场，否则敌人会对着一具 0 血的身体再补一刀
            // （日志会出现「敌人反击，造成 X 点伤害」，还要多播一次受伤动画）
            if (playerData.IsDead)
            {
                EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }

            if (enemy.HP <= 0)
            {
                battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                if (!silentBattle) yield return new WaitForSeconds(logDelay);
                EndBattle(true, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }

            // 敌人反击
            int actualToPlayer = playerData.SubtractHP(enemyDamageToPlayer);
            // 被反击算受伤
            if (!silentBattle && actualToPlayer > 0)
                playerData.PlayHurtAnimation();
            // 敌人完全破不了防（伤害为 0）时用 enemyDmgStr 显示伤害构成，
            // 与玩家侧「无法破防」那条日志对称 —— 否则只报一个 0，看不出是物理是 0 还是魔力是 0
            if (enemyDamageToPlayer <= 0)
                battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 反击，未能破防（{enemyDmgStr}）");
            else
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
            if (!silentBattle) yield return new WaitForSeconds(logDelay);

            if (playerData.IsDead)
            {
                EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }

            turnCount++;
            battleUI.UpdateTurn(turnCount);
            BlessingManager.Instance?.OnTurnEnd(playerData, enemy, battleUI);

            // 僵持保护：双方都打不动对方时循环不会自然退出。
            // 正常档只是拖时间，跳过档一帧跑完，没有这道闸会直接卡死编辑器。
            if (turnCount > MaxTurnCount)
            {
                Debug.LogWarning($"[BattleManager] 与 {enemy.EnemyName} 的战斗超过 {MaxTurnCount} 回合仍未分出胜负，强制收场");
                battleUI.AddLog($"战斗陷入僵持，与 <color=#779977>{enemy.EnemyName}</color> 的战斗中止");
                if (!silentBattle) yield return new WaitForSeconds(logDelay);
                EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
                yield break;
            }
        }

        EndBattle(!playerData.IsDead, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
    }

    private void EndBattle(bool won, PlayerData playerData, EnemyController enemy, int playerManaSnapshot, int enemyManaSnapshot)
    {
        isFighting = false;

        // 特殊祝福生命周期：战斗结束（恢复加攻等）
        BlessingManager.Instance?.OnBattleEnd(playerData, enemy, battleUI, won);

        // 恢复魔力到战斗前
        playerData.ManaCharge = playerManaSnapshot;
        if (enemy != null)
            enemy.ManaCharge = enemyManaSnapshot;

        // 清空战斗内属性修正层：攻/防/段数/减伤回到战斗前的数值。
        // 放在 OnBattleEnd 之后 —— 祝福的战终回调里还能读到带加成的实时值用于播日志。
        playerData.EndBattleStats();

        if (won && enemy != null)
        {
            battleUI.AddLog($"战斗胜利！获得 {enemy.GoldReward} 金币");
            playerData.AddGold(enemy.GoldReward);
            enemy.Defeat();
            // 敌人阵亡表现：轻轻抖一下、往下沉一点，同时淡出消失
            battleUI.PlayEnemyDeath();
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
        // 跳过档没有演出来看，不用留结算时间；其余档保留这 1 秒给玩家看清金币收益
        if (!silentBattle)
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
