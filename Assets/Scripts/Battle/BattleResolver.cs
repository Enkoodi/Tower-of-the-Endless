using UnityEngine;

/// <summary>结算推进到哪个阶段。一个阶段 = 一次「演」的最小单位。</summary>
public enum BattleStepKind
{
    Sneak,          // 敌人先手偷袭（只在敌人速度更高时发生一次）
    TurnStart,      // 回合开始：祝福结算 → 重算伤害 → 检查敌人是否被提前打死
    PlayerAttack,   // 玩家攻击 + 魔力消耗 + 吸血 + 敌人反伤
    EnemyCounter,   // 敌人反击 + 敌人吸血 + 玩家反伤
    TurnEnd,        // 回合结束：祝福结算 + 回合上限判定
}

/// <summary>战斗结束原因。</summary>
public enum BattleEndReason
{
    None,              // 还没结束
    EnemyDefeated,     // 击败了敌人
    PlayerDefeated,    // 玩家阵亡
    EnemyUnkillable,   // 伤不到敌人（自己打不出伤害，连反伤也造不成伤害）→ 提前收场
    Stalemate,         // 到回合上限仍未分出胜负
}

/// <summary>刚推进的那一步发生了什么，供表现层演出。</summary>
public struct BattleStep
{
    public BattleStepKind Kind;
    public int Turn;

    /// <summary>这一步敌人实际掉了多少血（已过敌人减伤）</summary>
    public int DamageToEnemy;
    /// <summary>这一步玩家实际掉了多少血（已过玩家减伤）</summary>
    public int DamageToPlayer;
    /// <summary>玩家吸血量（精确回血，未乘任何系数）</summary>
    public int PlayerHeal;
    /// <summary>敌人吸血量</summary>
    public int EnemyHeal;

    /// <summary>本回合是否消耗了魔力（灵知的祝福会免掉）</summary>
    public bool ManaConsumed;

    /// <summary>这一步结束时敌人是否阵亡。被祝福提前打死时也可能为 true</summary>
    public bool EnemyDied;
    /// <summary>这一步结束时玩家是否阵亡</summary>
    public bool PlayerDied;

    // ------------------------------------------------------------
    //  这一步「出手方」的伤害构成，供「无法破防 / 未能破防」那条日志显示。
    //  必须在这里快照 —— 步骤末尾会重算下一回合的伤害，表现层再去读内核属性就晚了。
    // ------------------------------------------------------------
    public int AttackerAttackCount;
    public int AttackerPhysical;
    public int AttackerManaCost;
    public int AttackerRawDamage;
}

/// <summary>
/// 战斗结算内核 —— **实战与图鉴共用同一份**。只算，不碰 UI、不 yield、不播动画。
///
/// 用法（两边必须都是「一步一停」，不能先算完再回放：
/// 实战的祝福会在回合之间改状态，先算完就意味着先把玩家的血改掉再播动画）：
///
///   实战：while (r.Step()) { PlayStep(r.LastStep); yield return WaitForSeconds(...); }
///   图鉴：r.RunToEnd();
///
/// 祝福等外部效果通过回调注入：
///   · 实战接 BlessingManager（并且必须在内核里调用，不能挪到表现层——
///     工匠的濒死回复要在死亡判定之前生效）
///   · 图鉴一律不设，所以图鉴是「白板对打」，不含祝福
/// </summary>
public class BattleResolver
{
    private enum Phase { Sneak, TurnStart, PlayerAttack, EnemyCounter, TurnEnd }

    private readonly IBattleUnit player;
    private readonly IBattleUnit enemy;
    private readonly int magicPercent;   // 魔力增幅器倍率，100 = 无增幅

    // ------------------------------------------------------------
    //  可选挂钩。不设则跳过（图鉴就是这么用的）。
    // ------------------------------------------------------------
    public System.Action OnTurnStart;
    public System.Action OnTurnEnd;
    public System.Func<bool> ShouldConsumeMana;
    public System.Action<int> OnPlayerDealDamage;
    public System.Action<int> OnEnemyTakeDamage;
    public System.Action<int> OnEnemyDealDamage;
    public System.Action<int> OnPlayerTakeDamage;

    // ------------------------------------------------------------
    //  本回合的中间量（也对外暴露，供「无法破防」那条日志显示伤害构成）
    // ------------------------------------------------------------
    private int playerPhysical, playerMagicDamage, damageToEnemy, manaCost;
    private int enemyPhysical, enemyDamageToPlayer, enemyManaCost;

    /// <summary>玩家本回合的物理伤害（未过敌人减伤）</summary>
    public int PlayerPhysical => playerPhysical;
    /// <summary>玩家本回合消耗的魔力（未乘增幅倍率）</summary>
    public int PlayerManaCost => manaCost;
    /// <summary>玩家本回合的魔力伤害（已乘增幅倍率）</summary>
    public int PlayerMagicDamage => playerMagicDamage;
    /// <summary>敌人本回合的物理伤害（未过玩家减伤）</summary>
    public int EnemyPhysical => enemyPhysical;
    /// <summary>敌人本回合消耗的魔力</summary>
    public int EnemyManaCost => enemyManaCost;
    /// <summary>敌人本回合的总伤害（未过玩家减伤）</summary>
    public int EnemyRawDamage => enemyDamageToPlayer;

    /// <summary>敌人速度更高 → 会先偷袭一次</summary>
    public bool WillSneak { get; }

    /// <summary>
    /// 玩家有没有办法伤到敌人（否则提前收场判负，对应实战的 damageToEnemy &lt;= 0 分支）。
    ///
    /// **两条路，任意一条成立就算有**：
    ///   ① 玩家自己打得出伤害（物理 + 魔力）；
    ///   ② 自己打不动，但**反伤**打得动 —— 玩家挨打时会把敌人的**物理**伤害按反伤系数弹回去，
    ///      而敌人每回合必然会反击，所以「这一击打 0」完全不代表「赢不了」。
    ///      反伤这条路要求：敌人打得出物理伤害 + 玩家反伤系数 &gt; 0 + 反伤过了敌人减伤之后仍为正。
    ///
    /// ⚠️ **构造时定格**，绝不能在回合重算后跟着变 ——
    /// 魔力用光之后 damageToEnemy 会归零，但那只是「这一击没魔力了」，不是「打不出伤害」。
    /// 实战也是只在开打前判这一次。
    /// </summary>
    public bool CanDamageEnemy { get; }

    /// <summary>
    /// 玩家靠反伤每回合能对敌人造成的实际伤害（已过敌人减伤，构造时定格）。
    ///
    /// 反伤吃的是**敌人这一击的物理伤害**：敌人打不出物理伤害、玩家反伤系数为 0、
    /// 或者反伤过了敌人减伤后归零/变负（减伤 ≥ 100% 时反伤反而给敌人回血），
    /// 这条路就不成立，值为 0。
    /// </summary>
    public int ReflectToEnemy { get; }

    public BattleStep LastStep { get; private set; }
    public BattleEndReason EndReason { get; private set; } = BattleEndReason.None;
    public bool IsFinished => EndReason != BattleEndReason.None;

    private int turnCount = 1;
    private Phase phase;

    public BattleResolver(IBattleUnit player, IBattleUnit enemy, int magicAmplifierPercent)
    {
        this.player = player;
        this.enemy = enemy;
        magicPercent = magicAmplifierPercent;

        WillSneak = enemy.Speed > player.Speed;

        // 和实战一样，先算一轮（偷袭用的是这一份）
        ComputeRound();

        // 定格「还有没有办法伤到敌人」—— 之后每回合重算 damageToEnemy 都不再影响这个判断。
        // 自己打不出伤害时，反伤是一条独立的路（敌人每回合必反击 → 玩家的反伤必然被触发）。
        ReflectToEnemy = ComputeReflectToEnemy();
        CanDamageEnemy = damageToEnemy > 0 || ReflectToEnemy > 0;

        // ⚠️ 这里**不能**因为「伤不到敌人」就直接判结束 ——
        // 旧实现的顺序是「先挨偷袭，再收场」，提前判会凭空少掉偷袭那一次伤害。
        // 所以判定放在偷袭之后（见 Step 与调用方）。
        phase = WillSneak ? Phase.Sneak : Phase.TurnStart;
    }

    /// <summary>
    /// 反伤的实际到手伤害 = 敌人这一击的物理伤害 × 玩家反伤系数，再过一次敌人减伤。
    /// 与 `DoEnemyCounter` 里的算法保持同式 —— 两处一旦漂了，「能不能赢」的预判就会和实战不符。
    /// </summary>
    private int ComputeReflectToEnemy()
    {
        if (player.ReflectDamage <= 0) return 0;   // 玩家没反伤
        if (enemyPhysical <= 0) return 0;          // 敌人打不出物理伤害，反伤无从触发

        int reflect = enemyPhysical * player.ReflectDamage / 100;
        if (reflect <= 0) return 0;                // 整数截断后为 0

        return reflect * (100 - enemy.DamageReduction) / 100;   // ≤ 0 表示反伤伤不到敌人
    }

    /// <summary>
    /// 推进一个阶段。返回 true = 这一步执行了（调用方应该演出 LastStep 再决定是否继续）；
    /// 返回 false = 战斗已经结束，没有下一步了。
    /// </summary>
    public bool Step()
    {
        if (IsFinished) return false;

        // 偷袭之后才判「伤不到敌人」，与旧实现一致。调用方在主循环前也会主动查一次
        // （那次要打日志），这里是给 RunToEnd 兜底。
        if (phase != Phase.Sneak && !CanDamageEnemy)
        {
            EndReason = BattleEndReason.EnemyUnkillable;
            return false;
        }

        switch (phase)
        {
            case Phase.Sneak:
                DoSneak();
                phase = Phase.TurnStart;
                break;

            case Phase.TurnStart:
                DoTurnStart();
                phase = Phase.PlayerAttack;
                break;

            case Phase.PlayerAttack:
                DoPlayerAttack();
                phase = Phase.EnemyCounter;
                break;

            case Phase.EnemyCounter:
                DoEnemyCounter();
                phase = Phase.TurnEnd;
                break;

            case Phase.TurnEnd:
                DoTurnEnd();
                phase = Phase.TurnStart;
                break;
        }
        return true;
    }

    /// <summary>一把跑到底，不演出。图鉴用这个。</summary>
    public BattleEndReason RunToEnd()
    {
        while (Step()) { }
        return EndReason;
    }

    // ============================================================
    //  各阶段
    // ============================================================

    private void ComputeRound()
    {
        playerPhysical = Mathf.Max(0, (player.Attack - enemy.Defense) * player.AttackCount);
        manaCost = Mathf.Min(player.ManaCharge, player.ManaMax);
        playerMagicDamage = manaCost * magicPercent / 100;
        damageToEnemy = playerPhysical + playerMagicDamage;

        enemyPhysical = Mathf.Max(0, (enemy.Attack - player.Defense) * enemy.AttackCount);
        enemyManaCost = Mathf.Min(enemy.ManaCharge, enemy.ManaMax);
        enemyDamageToPlayer = enemyPhysical + enemyManaCost;
    }

    private void DoSneak()
    {
        int sneakDamage = enemyDamageToPlayer * 2;
        int actual = player.ReceiveDamage(sneakDamage);

        LastStep = new BattleStep
        {
            Kind = BattleStepKind.Sneak,
            Turn = turnCount,
            DamageToPlayer = actual,
            PlayerDied = player.HP <= 0,
        };

        if (LastStep.PlayerDied) EndReason = BattleEndReason.PlayerDefeated;
    }

    private void DoTurnStart()
    {
        // 祝福：爱的加攻与扣血、朗基努斯的加攻/加段、深渊与静默的真伤、异乡人的减伤…
        OnTurnStart?.Invoke();

        // 回合开始时生效的属性变动要在本回合立刻体现，所以这里重算
        ComputeRound();

        // ⚠️ 必须先用局部变量拼好再整体赋给 LastStep。
        // LastStep 是自动属性，直接写 LastStep.EnemyDied 会报 CS1612（属性返回的是副本）
        BattleStep s = new BattleStep { Kind = BattleStepKind.TurnStart, Turn = turnCount };

        // 深渊/静默可能直接把敌人打死
        if (enemy.HP <= 0)
        {
            s.EnemyDied = true;
            EndReason = BattleEndReason.EnemyDefeated;
        }

        LastStep = s;
    }

    private void DoPlayerAttack()
    {
        BattleStep s = new BattleStep
        {
            Kind = BattleStepKind.PlayerAttack,
            Turn = turnCount,
            AttackerAttackCount = player.AttackCount,
            AttackerPhysical = playerPhysical,
            AttackerManaCost = manaCost,
            AttackerRawDamage = damageToEnemy,
        };

        // 玩家攻击
        s.DamageToEnemy = enemy.ReceiveDamage(damageToEnemy);
        OnPlayerDealDamage?.Invoke(s.DamageToEnemy);
        OnEnemyTakeDamage?.Invoke(s.DamageToEnemy);

        // 消耗魔力（灵知的祝福会免掉）
        s.ManaConsumed = ShouldConsumeMana?.Invoke() ?? true;
        if (s.ManaConsumed) player.ManaCharge -= manaCost;

        // 吸血 = 真实物理伤害 × 吸血系数，精确回血（不乘 hpMultiplier）
        int steal = playerPhysical * player.LifeSteal / 100;
        if (steal > 0)
        {
            player.HealRaw(steal);
            s.PlayerHeal = steal;
        }

        // 敌人反伤：仅在敌人未因本次攻击死亡时触发（吸血已先结算）
        if (enemy.HP > 0)
        {
            int reflect = playerPhysical * enemy.ReflectDamage / 100;
            if (reflect > 0) s.DamageToPlayer += player.ReceiveDamage(reflect);
        }

        ComputeRound();

        s.EnemyDied = enemy.HP <= 0;
        s.PlayerDied = player.HP <= 0;

        // 反伤也可能把玩家打死 —— 这时立刻收场，不会再挨一次反击
        if (s.PlayerDied) EndReason = BattleEndReason.PlayerDefeated;
        else if (s.EnemyDied) EndReason = BattleEndReason.EnemyDefeated;

        LastStep = s;
    }

    private void DoEnemyCounter()
    {
        BattleStep s = new BattleStep
        {
            Kind = BattleStepKind.EnemyCounter,
            Turn = turnCount,
            AttackerAttackCount = enemy.AttackCount,
            AttackerPhysical = enemyPhysical,
            AttackerManaCost = enemyManaCost,
            AttackerRawDamage = enemyDamageToPlayer,
        };

        // 敌人反击
        s.DamageToPlayer = player.ReceiveDamage(enemyDamageToPlayer);
        OnEnemyDealDamage?.Invoke(s.DamageToPlayer);
        // 工匠的濒死回复在这里生效 —— 必须在下面的死亡判定之前
        OnPlayerTakeDamage?.Invoke(s.DamageToPlayer);

        // 消耗魔力 + 敌人吸血 + 玩家反伤
        enemy.ManaCharge -= enemyManaCost;

        int enemySteal = enemyPhysical * enemy.LifeSteal / 100;
        if (enemySteal > 0)
        {
            enemy.HealRaw(enemySteal);
            s.EnemyHeal = enemySteal;
        }

        // 玩家反伤：仅在玩家未因本次伤害死亡时触发（吸血已先结算）
        // 记进 DamageToEnemy —— 玩家自己打不出伤害时，这一步就是他唯一的输出，表现层必须看得到
        if (player.HP > 0)
        {
            int playerReflect = enemyPhysical * player.ReflectDamage / 100;
            if (playerReflect > 0) s.DamageToEnemy = enemy.ReceiveDamage(playerReflect);
        }

        ComputeRound();

        s.EnemyDied = enemy.HP <= 0;
        s.PlayerDied = player.HP <= 0;

        if (s.EnemyDied) EndReason = BattleEndReason.EnemyDefeated;
        else if (s.PlayerDied) EndReason = BattleEndReason.PlayerDefeated;

        LastStep = s;
    }

    private void DoTurnEnd()
    {
        turnCount++;
        OnTurnEnd?.Invoke();

        LastStep = new BattleStep { Kind = BattleStepKind.TurnEnd, Turn = turnCount };

        // 僵持保护：双方都打不动对方时循环不会自然退出
        if (turnCount > BattleManager.MaxTurnCount)
            EndReason = BattleEndReason.Stalemate;
    }
}
