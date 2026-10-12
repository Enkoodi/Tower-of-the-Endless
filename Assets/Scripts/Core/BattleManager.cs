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

    /// <summary>最近一场战斗是否胜利。供需要按结果分支的系统读取（如 NPC 对话的战后续接）。</summary>
    public bool LastBattleWon { get; private set; }

    private int turnCount = 1;
    private System.Action<bool> onBattleEnd;

    // 玩家日志：战斗前的 HP / 金币快照，战斗结束时算差值用
    private int logHpBefore;
    private int logGoldBefore;

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

        // 玩家日志：战前快照，战斗结束时算 HP / 金币差
        logHpBefore = playerData.HP;
        logGoldBefore = playerData.Gold;

        // 快照战斗前魔力，战斗中消耗，战斗结束后恢复。
        // ⚠️ 必须在 BeginBattleStats / BlessingManager.OnBattleStart 之前取：
        //    真理的祝福会在 OnBattleStart 里把魔力 ×1.25，如果快照放在那之后，
        //    取到的就是加成后的值，战斗结束"恢复"会把加成固化成新的基准 → 每打一场滚一次雪球。
        int playerManaSnapshot = playerData.ManaCharge;
        int enemyManaSnapshot = enemy.ManaCharge;

        // 冻结战斗前数值：此后战斗中一切属性改动只写修正层，伤害和面板读的都是实时值。
        // 必须在 BlessingManager.OnBattleStart 之前 —— 战斗开始触发的祝福（如异乡人）也要写进修正层。
        playerData.BeginBattleStats();

        // 衔尾之十字：战斗开始时比较吸血与反伤，较低的一项补上较高一项的一半。
        // 加成走战斗内修正层 → EndBattleStats 整体清零，出战即恢复原样（和大部分祝福一致）。
        // ⚠️ 必须在 BeginBattleStats 之后（否则会被清零）、在 OpenBattle 之前（面板要显示加成后的值）。
        string ouroborosLog = ApplyOuroborosCrucifix(playerData);

        // 动画/特效倍速与日志节奏保持一致（正常=1、两倍=2、四倍=4）
        battleUI.PlaybackSpeed = PlaybackSpeed;
        // 跳过档不打开战斗窗口：下面的日志/面板更新会照常写进没显示的 UI，不影响结算结果
        if (silentBattle)
            Debug.Log($"[BattleManager] 跳过档：直接结算与 {enemy.EnemyName} 的战斗（不打开战斗界面）");
        else
            battleUI.OpenBattle(playerData, enemy);
        if (enemyHalved)
            battleUI.AddLog($"<color=#FFCC66>敌人血量减半</color>：{enemy.EnemyName} 生命值减半");
        if (ouroborosLog != null)
            battleUI.AddLog(ouroborosLog);
        battleUI.UpdateTurn(turnCount);
        BlessingManager.Instance?.OnBattleStart(playerData, enemy, battleUI);

        // 兑现「下一场战斗生效」的消耗品（麦酒 / 魔力精华）。
        // ⚠️ 位置不能挪：上面魔力快照与 BeginBattleStats 都已完成，缺一不可 ——
        //    · 魔力精华直接改 ManaCharge，早于快照会被一起存下来，
        //      战斗结束「恢复」成加过的值 → 变成永久加成；
        //    · 麦酒写的是战斗内修正层，早于 BeginBattleStats 会被整体清零。
        playerData.ApplyPendingBattleItems(battleUI);

        OnBattleOpen?.Invoke();
        StartCoroutine(BattleCoroutine(playerData, enemy, playerManaSnapshot, enemyManaSnapshot));
    }

    /// <summary>
    /// 衔尾之十字：战斗开始时把「吸血 / 反伤」里较低的一项补上较高一项的一半。
    /// 返回要写进战斗日志的文案；没这件装备 / 两项相等 / 加成算出来是 0 时返回 null。
    /// </summary>
    private static string ApplyOuroborosCrucifix(PlayerData playerData)
    {
        OuroborosCrucifixEffect effect = playerData.GetComponent<OuroborosCrucifixEffect>();
        if (effect == null) return null;

        int lifeSteal = playerData.LifeSteal;           // 战斗内 = 快照值（修正层刚清零）
        int reflectDamage = playerData.ReflectDamage;

        effect.ComputeBonus(lifeSteal, reflectDamage, out int bonusLs, out int bonusRf);

        if (bonusLs > 0)
        {
            playerData.AddBattleLifeStealFlat(bonusLs);
            return $"<color=#CC66FF>衔尾之十字</color>：吸血 +{bonusLs}（{lifeSteal} → {lifeSteal + bonusLs}）";
        }
        if (bonusRf > 0)
        {
            playerData.AddBattleReflectDamageFlat(bonusRf);
            return $"<color=#CC66FF>衔尾之十字</color>：反伤 +{bonusRf}（{reflectDamage} → {reflectDamage + bonusRf}）";
        }
        return null;
    }

    private IEnumerator BattleCoroutine(PlayerData playerData, EnemyController enemy,
                                        int playerManaSnapshot, int enemyManaSnapshot)
    {
        // 神圣剑倍率（拾取神圣剑后生效；只放大魔力伤害，不影响魔力消耗）
        HolySwordEffect amp = playerData.GetComponent<HolySwordEffect>();
        int magicPercent = amp != null ? amp.MultiplierPercent : 100;

        // 贝希摩斯之牙：撕裂（Rend）。参数在**构造时**交给内核 —— 这样「伤不到敌人」的判定
        // 也能把撕裂这条路算进去，否则打不出普通伤害时会被提前判负。
        BehemothsFangEffect fang = playerData.GetComponent<BehemothsFangEffect>();
        RendSpec rendSpec = fang != null ? fang.ToRendSpec() : default;
        if (fang != null)
            Debug.Log($"[BattleManager] 贝希摩斯之牙生效：每次攻击叠 {fang.StacksPerAttack} 层撕裂" +
                      $"（上限 {fang.MaxStacks} 层），每层按实时攻击力 {fang.PercentPerStack}% 结算真伤");

        // 结算内核 —— 实战与图鉴共用同一份，这里只负责把每一步「演」出来。
        // 祝福必须挂在回调里、由内核在正确时机调用，不能挪到表现层：
        // 工匠的濒死回复要在死亡判定之前生效。
        BattleResolver resolver = new BattleResolver(playerData, enemy, magicPercent, rendSpec)
        {
            OnTurnStart        = () => BlessingManager.Instance?.OnTurnStart(playerData, enemy, battleUI),
            OnTurnEnd          = () => BlessingManager.Instance?.OnTurnEnd(playerData, enemy, battleUI),
            ShouldConsumeMana  = () => BlessingManager.Instance?.ShouldConsumeMana(playerData) ?? true,
            OnPlayerDealDamage = d => BlessingManager.Instance?.OnPlayerDealDamage(playerData, enemy, battleUI, d),
            OnEnemyTakeDamage  = d => BlessingManager.Instance?.OnEnemyTakeDamage(playerData, enemy, battleUI, d),
            OnEnemyDealDamage  = d => BlessingManager.Instance?.OnEnemyDealDamage(playerData, enemy, battleUI, d),
            OnPlayerTakeDamage = d => BlessingManager.Instance?.OnPlayerTakeDamage(playerData, enemy, battleUI, d),
        };

        // 利维坦之髓：持有时每隔若干回合冻结敌人一回合。
        // 只封锁敌人的**主动攻击**，反伤等被动效果照常（见 BattleResolver 的行动封锁挂钩）。
        LeviathansMarrowEffect marrow = playerData.GetComponent<LeviathansMarrowEffect>();
        if (marrow != null)
        {
            resolver.EnemyCanAct = turn => !marrow.FreezesOnTurn(turn);
            Debug.Log($"[BattleManager] 利维坦之髓生效：每隔 {marrow.FreezeInterval} 回合冻结敌人一回合（第 " +
                      $"{marrow.FreezeInterval}、{marrow.FreezeInterval * 2}…回合）");
        }

        // —— 先手偷袭 ——（伤害在内核的 Sneak 步里结算，这里先播一句预告）
        // 偷袭也是敌人的一次主动攻击：被冻结时连偷袭都不发生，
        // 此时内核那一步会被主循环静默消费（主循环没有 Sneak 分支）。
        if (resolver.WillSneak && resolver.EnemyCanActThisTurn)
        {
            battleUI.AddLog($"受到<color=#779977>{enemy.EnemyName}</color>偷袭！");
            if (!silentBattle) yield return new WaitForSeconds(logDelay);

            resolver.Step();
            PlaySneak(resolver.LastStep, playerData);
            battleUI.UpdatePlayerPanel(playerData);
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
        }
        else
        {
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
        }

        // 被偷袭打死 → 判负
        if (resolver.IsFinished)
        {
            EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
            yield break;
        }

        // 完全伤不到敌人（自己打不出伤害，且反伤也造不成伤害）→ 立刻收场判负。
        // 用伤害构成而不是一个光秃秃的数字，让玩家看出为什么打不动。
        // ⚠️ 判据是 CanDamageEnemy 而不是「这一击打了 0」——
        //    玩家可能打 0 但靠反伤磨死敌人，那种情况必须照常进主循环。
        if (!resolver.CanDamageEnemy)
        {
            battleUI.AddLog($"<color=#7799CC>玩家</color>攻击，造成 {FormatDamage(playerData.AttackCount, resolver.PlayerPhysical, resolver.PlayerManaCost, resolver.PlayerPhysical + resolver.PlayerMagicDamage)} 点伤害");
            playerData.PlayAttackAnimation();
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
            EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
            yield break;
        }

        // —— 主循环：内核推进一步，这里演一步 ——
        while (resolver.Step())
        {
            BattleStep step = resolver.LastStep;

            switch (step.Kind)
            {
                case BattleStepKind.TurnStart:
                    // 回合开始本身没有演出，也不停顿 —— 停了每回合会平白多出两拍。
                    // 只有撕裂真伤、或被深渊/静默/撕裂提前打死时才说一句。
                    if (step.RendDamage > 0)
                    {
                        battleUI.AddLog($"<color=#CC66FF>撕裂</color>：造成 <color=#FF4444>{step.RendDamage}</color> 点真实伤害（{step.RendStacks} 层）");
                        battleUI.UpdateEnemyPanel(enemy);
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    if (step.EnemyDied)
                    {
                        battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;

                case BattleStepKind.PlayerAttack:
                    PlayPlayerAttack(step, playerData, enemy);
                    if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    // 打死敌人的宣告往后挪一拍，保留「先看到伤害、再看到击败」的原节奏
                    if (step.EnemyDied)
                    {
                        battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;

                case BattleStepKind.EnemyCounter:
                    PlayEnemyCounter(step, playerData, enemy);
                    if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    if (step.EnemyDied)
                    {
                        battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;

                case BattleStepKind.TurnEnd:
                    battleUI.UpdateTurn(step.Turn);
                    // 僵持保护：双方都打不动对方时内核会判 Stalemate
                    if (resolver.EndReason == BattleEndReason.Stalemate)
                    {
                        Debug.LogWarning($"[BattleManager] 与 {enemy.EnemyName} 的战斗超过 {MaxTurnCount} 回合仍未分出胜负，强制收场");
                        battleUI.AddLog($"战斗陷入僵持，与 <color=#779977>{enemy.EnemyName}</color> 的战斗中止");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;
            }

            if (resolver.IsFinished) break;
        }

        EndBattle(resolver.EndReason == BattleEndReason.EnemyDefeated,
                  playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
    }

    // ============================================================
    //  演出 —— 把内核推进的一步变成日志 / 动画 / 面板刷新。
    //  只做表现，不改任何战斗数值；所有数字都从 BattleStep 里取。
    // ============================================================

    private void PlaySneak(BattleStep step, PlayerData playerData)
    {
        // 被偷袭算受伤
        if (!silentBattle && step.DamageToPlayer > 0)
            playerData.PlayHurtAnimation();

        if (step.DamageToPlayer <= 0)
            battleUI.AddLog($"偷袭似乎不起作用");
        else
            battleUI.AddLog($"<color=#7799CC>玩家</color>受到 <color=#FF4444>{step.DamageToPlayer}</color> 点伤害");
    }

    private void PlayPlayerAttack(BattleStep step, PlayerData playerData, EnemyController enemy)
    {
        // 被冻结：跳过主动攻击 —— 没有伤害、没有动画（反伤等被动效果不受影响）
        if (step.AttackerFrozen)
        {
            battleUI.AddLog($"<color=#7799CC>玩家</color>被<color=#66CCFF>冻结</color>，本回合无法行动");
            battleUI.UpdatePlayerPanel(playerData);
            return;
        }

        playerData.PlayAttackAnimation();
        // 敌人受击表现：等头像冲到敌人面前才抖一下并叠出斩击（时序在 BattleUI 内控制）
        if (step.DamageToEnemy > 0)
            battleUI.PlayEnemyHit(playerData.AttackCount);
        battleUI.AddLog($"<color=#7799CC>玩家</color>攻击，造成 <color=#FF4444>{step.DamageToEnemy} </color>点伤害");
        battleUI.UpdateEnemyPanel(enemy);

        // 撕裂：不论是否破防都会叠层
        if (step.RendStacksAdded > 0)
            battleUI.AddLog($"<color=#CC66FF>撕裂</color> +{step.RendStacksAdded} 层（当前 {step.RendStacks} 层）");

        if (!step.ManaConsumed)
            battleUI.AddLog($"<color=#88CCFF>灵知的祝福</color>：本回合不消耗魔力充能");

        battleUI.UpdatePlayerPanel(playerData);
    }

    private void PlayEnemyCounter(BattleStep step, PlayerData playerData, EnemyController enemy)
    {
        // 被冻结：敌人跳过主动攻击 —— 没有伤害、没有动画（敌人反伤等被动效果不受影响）
        if (step.AttackerFrozen)
        {
            battleUI.AddLog($"<color=#66CCFF>利维坦之髓</color>：冻结敌人一回合");
            battleUI.UpdateEnemyPanel(enemy);
            return;
        }

        // 被反击算受伤
        if (!silentBattle && step.DamageToPlayer > 0)
            playerData.PlayHurtAnimation();

        // 敌人完全破不了防（伤害为 0）时显示伤害构成，
        // 与玩家侧「无法破防」那条日志对称 —— 否则只报一个 0，看不出是物理是 0 还是魔力是 0
        if (step.AttackerRawDamage <= 0)
            battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 反击，未能破防（{FormatDamage(step.AttackerAttackCount, step.AttackerPhysical, step.AttackerManaCost, step.AttackerRawDamage)}）");
        else
            battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 反击，造成 <color=#FF4444>{step.DamageToPlayer}</color> 点伤害");

        battleUI.UpdateEnemyPanel(enemy);
        battleUI.UpdatePlayerPanel(playerData);
    }


    private void EndBattle(bool won, PlayerData playerData, EnemyController enemy, int playerManaSnapshot, int enemyManaSnapshot)
    {
        // ⚠️ 这里只做结算，不解锁输入：战斗界面还要再留一会儿（见 CloseAfterDelay）才真正消失。
        //    isFighting / OnBattleClose / onBattleEnd 全部挪到那之后 —— 否则玩家会在窗口还开着的
        //    时候就能走动，甚至走进旁边的敌人再开一场战斗（旧窗口的收尾协程会把新窗口关掉）。

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

        // 玩家日志：这一场的结果。战斗内逐回合战报不写（太长），只要结果与血/钱的变化
        if (enemy != null)
        {
            string gold = won ? $"，金币 +{playerData.Gold - logGoldBefore}" : "";
            PlayerLog.Note($"战斗 {enemy.EnemyName} ({enemy.gridPosition.x},{enemy.gridPosition.y})"
                         + $" → {(won ? "胜利" : "失败")}（HP {logHpBefore}→{playerData.HP}{gold}）");
        }

        StartCoroutine(CloseAfterDelay(won));
    }

    /// <summary>
    /// 战斗界面收尾：留出阅读结算的时间 → 真正关闭窗口 → **然后**才解锁输入、通知调用方。
    ///
    /// ⚠️ 解锁必须排在 CloseBattle() 之后。界面还显示着的时候玩家不能动，
    ///    否则「禁止输入」的时长会比界面存续时间短一截。
    ///    顺序上也要先 OnBattleClose（解除移动锁定）再 onBattleEnd（此时才允许玩家前进 / 移除NPC）。
    /// </summary>
    private IEnumerator CloseAfterDelay(bool won)
    {
        // 跳过档没有演出来看，不用留结算时间；其余档保留这 1 秒给玩家看清金币收益
        if (!silentBattle)
            yield return new WaitForSeconds(1f);
        battleUI?.CloseBattle();

        // 界面已经消失，这时才真正解除锁定
        isFighting = false;
        LastBattleWon = won;
        OnBattleClose?.Invoke();
        onBattleEnd?.Invoke(won);
        onBattleEnd = null;
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
