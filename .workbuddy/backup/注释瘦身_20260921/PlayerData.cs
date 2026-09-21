using UnityEngine;

/// <summary>
/// 玩家数据 — 挂载在玩家 GameObject 上。
/// 持有钥匙、战斗属性、金币。
/// 战斗结算本身在 BattleManager，本类只提供属性的读写接口：
///   · 战斗外改属性：AddAttack / AddDefense / AddHP ...（会乘对应的 Multiplier）
///   · 战斗中改属性：AddBattle* 系列（写「战斗内修正层」，基数恒为战斗前快照）
/// 实现 IKeyInventory 供门系统查询钥匙。
/// 实现 IPlayerHealth 供魂之门扣除 HP。
/// </summary>
public class PlayerData : MonoBehaviour, IKeyInventory, IPlayerHealth, IBattleUnit
{
    // ============================================================
    //  初始化
    // ============================================================

    void Start()
    {
        // 从全局存档读取 Aeon 钥匙数量
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.ApplyGlobalAeonKeys();
            // 神圣火花同为全局道具，一起从全局存档覆盖
            SaveManager.Instance.ApplyGlobalDivineSpark();
        }
    }

    // ============================================================
    //  Inspector 字段
    // ============================================================

    [Header("战斗属性")]
    [SerializeField] private int hp = 500;
    [SerializeField] private int attack = 10;
    [SerializeField] private int defense = 5;
    [SerializeField] private int attackCount = 1;
    [SerializeField] private int lifeSteal = 0;
    [SerializeField] private int reflectDamage = 0;
    [SerializeField] private int damageReduction = 0;
    [SerializeField] private int manaCharge = 10;
    [SerializeField] private int manaMax = 100;
    [SerializeField] private int speed = 100;

    [Header("属性系数（百分比，100=100%）")]
    [SerializeField] private int goldMultiplier = 100;
    [SerializeField] private int hpMultiplier = 100;
    [SerializeField] private int attackMultiplier = 100;
    [SerializeField] private int defenseMultiplier = 100;

    [Header("金币")]
    [SerializeField] private int gold = 0;

    [Header("钥匙数量")]
    [SerializeField] private int yellowKeys = 0;
    [SerializeField] private int blueKeys = 0;
    [SerializeField] private int redKeys = 0;
    [SerializeField] private int psycheKeys = 0;
    [SerializeField] private int aeonKeys = 0; 

    [Header("全局道具（跨存档保留）")]
    [Tooltip("神圣火花：数量 > 0 时解锁开场菜单的「无尽模式」。\n" +
             "运行中可直接在 Inspector 里改；与 save/global.json 的同步规则和 Aeon 钥匙完全一致——\n" +
             "进场景时由全局存档覆盖，存档（P）时再由这里写回全局存档。")]
    [SerializeField] private int divineSpark = 0;

    [Header("传送器数量")]
    [SerializeField] private int upTeleporterCount = 0;
    [SerializeField] private int downTeleporterCount = 0;

    [Header("圣水数量")]
    [SerializeField] private int enemyHalveItemCount = 0;
    [SerializeField] private int pendingEnemyHalveBattles = 0;

    // ============================================================
    //  公开只读属性
    //
    //  Attack / Defense / AttackCount / DamageReduction 在战斗中返回「战斗时实时值」
    //  （战斗前快照 + 战斗内修正层），战斗外返回真实字段。规则见下方「战斗内属性修正层」。
    //
    //  取整口径（2026-09-20 定稿）：**取整方向一律偏向玩家**。
    //    · 加成（有符号增量，可能是正也可能为负）→ Mathf.CeilToInt：
    //      向 +∞ 取整。正增量变大（+0.7 → +1），负增量变小（-0.7 → 0）。
    //    · 以正数表示的「损失量」（如爱的祝福扣血）→ Mathf.FloorToInt：
    //      向 0 取整，玩家少扣（1.5 → 1）。
    //  别用整数除法 `/ 100` —— 那是向下截断，对正加成等于少给，
    //  基础值小的时候会把加成整个抹掉（攻 7 的 +10% 会变成 0）。
    // ============================================================
    public int Attack        => inBattle
        ? battleBaseAttack + battleFlatAttack
          + Mathf.CeilToInt(battleBaseAttack * battlePercentAttack / 100f)
        : attack;
    public int Defense       => inBattle
        ? battleBaseDefense + battleFlatDefense
          + Mathf.CeilToInt(battleBaseDefense * battlePercentDefense / 100f)
        : defense;
    public int HP            => hp;
    public int AttackCount   => inBattle ? battleBaseAttackCount + battleFlatAttackCount : attackCount;
    public int LifeSteal     => lifeSteal;
    public int ReflectDamage => reflectDamage;
    public int DamageReduction => inBattle ? battleBaseDamageReduction + battleFlatDamageReduction : damageReduction;
    public int ManaCharge    { get => manaCharge; set => manaCharge = value; }
    public int ManaMax       => manaMax;
    public int Speed         => speed;
    public int Gold          => gold;
    public bool IsDead       => hp <= 0;

    public int UpTeleporterCount   => upTeleporterCount;
    public int DownTeleporterCount => downTeleporterCount;

    public int EnemyHalveItemCount => enemyHalveItemCount;
    public int PendingEnemyHalveBattles => pendingEnemyHalveBattles;

    /// <summary>神圣火花数量（全局道具，跨存档保留；&gt;0 时解锁无尽模式）</summary>
    public int DivineSpark => divineSpark;

    // 系数
    public int GoldMultiplier    => goldMultiplier;
    public int HPMultiplier      => hpMultiplier;
    public int AttackMultiplier  => attackMultiplier;
    public int DefenseMultiplier => defenseMultiplier;

    // ============================================================
    //  战斗内属性修正层
    //
    //  规则（与策划约定）：
    //   1. 战斗开始时把真实字段快照为 base，战斗中的一切属性改动只写修正层，
    //      绝不碰真实字段 —— 战斗前值和战斗时值是两份数据。
    //   2. 实时值 = base + Σ固定值 + base × Σ百分比 / 100。
    //      百分比之间是「加法叠加」：先 +10% 再 +20% → base×1.30，
    //      不是 base×1.1×1.2=1.32。
    //   3. 战斗内的固定值不乘 attackMultiplier / defenseMultiplier ——
    //      那是战斗外商店、祝福的成长系数，战斗内临时加成再乘一次会失控。
    //   4. 战斗结束 EndBattleStats() 整体清零，不做「记账式负号抵消」，
    //      所以战斗异常中断也不会把加成残留在玩家身上。
    //
    //  战斗中要改属性一律走 AddBattle* 系列；AddAttack / AddDefense 等是战斗外接口。
    // ============================================================

    private bool inBattle;

    // 战斗前快照
    private int battleBaseAttack;
    private int battleBaseDefense;
    private int battleBaseAttackCount;
    private int battleBaseDamageReduction;

    // 战斗内修正
    private int battleFlatAttack;
    private int battleFlatDefense;
    private int battleFlatAttackCount;
    private int battleFlatDamageReduction;
    private int battlePercentAttack;
    private int battlePercentDefense;

    /// <summary>是否处于战斗内（修正层生效中）。</summary>
    public bool InBattle => inBattle;

    /// <summary>战斗中累计的固定值加攻（供祝福日志展示）。</summary>
    public int BattleFlatAttack => battleFlatAttack;

    /// <summary>战斗前真实数值，存档等地方应读这组而不是 Attack / Defense 等。</summary>
    public int BaseAttack          => attack;
    public int BaseDefense         => defense;
    public int BaseAttackCount     => attackCount;
    public int BaseDamageReduction => damageReduction;

    /// <summary>
    /// 冻结战斗前数值并清空修正层。
    /// 必须在 BlessingManager.OnBattleStart 之前调用 —— 战斗开始触发的祝福也要写进修正层。
    /// </summary>
    public void BeginBattleStats()
    {
        inBattle = true;

        battleBaseAttack          = attack;
        battleBaseDefense         = defense;
        battleBaseAttackCount     = attackCount;
        battleBaseDamageReduction = damageReduction;

        battleFlatAttack = battleFlatDefense = battleFlatAttackCount = battleFlatDamageReduction = 0;
        battlePercentAttack = battlePercentDefense = 0;

        Debug.Log($"[PlayerData] 战斗快照：攻{battleBaseAttack} 防{battleBaseDefense} " +
                  $"段{battleBaseAttackCount} 减伤{battleBaseDamageReduction}%");
    }

    /// <summary>清空修正层，属性回到战斗前的数值。</summary>
    public void EndBattleStats()
    {
        inBattle = false;

        battleFlatAttack = battleFlatDefense = battleFlatAttackCount = battleFlatDamageReduction = 0;
        battlePercentAttack = battlePercentDefense = 0;
    }

    /// <summary>战斗中：攻击力 +固定值（不乘攻击系数）。</summary>
    public void AddBattleAttackFlat(int amount)
    {
        if (!RequireBattle(nameof(AddBattleAttackFlat))) return;
        battleFlatAttack += amount;
    }

    /// <summary>战斗中：攻击力 +百分比，基数恒为战斗前快照。</summary>
    public void AddBattleAttackPercent(int percent)
    {
        if (!RequireBattle(nameof(AddBattleAttackPercent))) return;
        battlePercentAttack += percent;
    }

    /// <summary>战斗中：防御力 +固定值（不乘防御系数）。</summary>
    public void AddBattleDefenseFlat(int amount)
    {
        if (!RequireBattle(nameof(AddBattleDefenseFlat))) return;
        battleFlatDefense += amount;
    }

    /// <summary>战斗中：防御力 +百分比，基数恒为战斗前快照。</summary>
    public void AddBattleDefensePercent(int percent)
    {
        if (!RequireBattle(nameof(AddBattleDefensePercent))) return;
        battlePercentDefense += percent;
    }

    /// <summary>战斗中：攻击段数 +固定值。</summary>
    public void AddBattleAttackCountFlat(int amount)
    {
        if (!RequireBattle(nameof(AddBattleAttackCountFlat))) return;
        battleFlatAttackCount += amount;
    }

    /// <summary>战斗中：减伤系数 +固定值（百分点）。</summary>
    public void AddBattleDamageReductionFlat(int amount)
    {
        if (!RequireBattle(nameof(AddBattleDamageReductionFlat))) return;
        battleFlatDamageReduction += amount;
    }

    // ============================================================
    //  属性写入口分成两族，各自有越界守卫（2026-09-20 定稿）
    //
    //  【战前接口】Add* / Apply* —— 写真实字段（= 战斗前数值）
    //      守卫 RequireOutOfBattle：战斗中调用直接拦下并警告，一个字节都不写。
    //      唯一例外是 AddGold —— 战斗奖励和恩惠的祝福都在战斗结算时加金币，必须能用。
    //      另一例外是 Set* —— 那是读档恢复路径，静默失败比污染更糟，所以不拦（只在读档时调）。
    //
    //  【战斗时接口】AddBattle* —— 写战斗内修正层，基数恒为战斗前快照
    //      守卫 RequireBattle：战斗外调用直接拦下并警告。
    //
    //  两族之外还能改玩家数据的只剩 Heal / HealRaw / SetHP / SubtractHP /
    //  SubtractRawHPKeepAlive —— 它们改的是 hp，而 HP 是活资源、不做战斗快照，
    //  战斗中本来就该能改。
    //
    //  新增任何「战斗中改属性」的效果时，一律加 AddBattle*；
    //  不要新建绕过守卫的直写路径。
    // ============================================================

    /// <summary>战斗内接口的越界保护：战斗外调用只报警告并忽略，避免污染真实字段。</summary>
    private bool RequireBattle(string caller)
    {
        if (inBattle) return true;

        Debug.LogWarning($"[PlayerData] {caller} 在战斗外被调用，已忽略。" +
                         "战斗外改属性请用 AddAttack / AddDefense 等直接改真实字段。");
        return false;
    }

    /// <summary>
    /// 战斗外接口的越界保护：战斗中调用直接拦下，一个字节都不写。
    /// 返回 true = 当前不在战斗中，可以继续。
    ///
    /// 为什么是拦截而不是只警告：这些方法写的是**真实字段**，也就是「战斗前数值」本身。
    /// 在战斗中调用它们两头不讨好 ——
    /// 本场战斗读的是 BeginBattleStats 的快照（所以当场不生效），
    /// 而 EndBattleStats 只清修正层、不回滚真实字段（所以改动会永久留下，变成下一场的基准）。
    /// 既然是注定错误的用法，就不该让它改到数据。
    /// </summary>
    private bool RequireOutOfBattle(string caller)
    {
        if (!inBattle) return true;

        Debug.LogWarning($"[PlayerData] {caller} 在战斗中被调用，已拦截。" +
                         "战斗中改属性请走 AddBattle* 接口（战斗内修正层）");
        return false;
    }

    // ============================================================
    //  动画（受伤 / 攻击）
    //
    //  参数说明：Player.controller 与 PlayerUI.controller 的 isHurt / isAttack
    //  都是 Trigger 型（m_Type: 9），只能用 SetTrigger 触发，触发一次即被状态机消费，
    //  不需要手动复位；用 SetBool 会直接失效并打警告。
    //
    //  受伤动画的触发规则（与策划约定）：
    //    算受伤 —— 战斗中被偷袭、战斗中被反击、夹击扣血、魔力光环
    //    不算  —— 玩家攻击后被反伤（那是自己攻击的代价）
    // ============================================================

    private static readonly int IsHurtHash = Animator.StringToHash("isHurt");

    private Animator bodyAnimator;   // 玩家本体的 Animator（与本脚本同对象，懒解析）

    /// <summary>玩家本体的 Animator（Player.controller：PlayerIdle / Player_Hurt）。</summary>
    private Animator BodyAnimator
    {
        get
        {
            if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();
            return bodyAnimator;
        }
    }

    /// <summary>
    /// 玩家受伤：战斗界面头像白闪（PlayerUI.controller 的 isHurt）。
    /// 只在玩家「被攻击」掉血时调用 —— 战斗中被偷袭 / 被反击、夹击扣血、魔力光环。
    /// 玩家攻击后受到的反伤不算受伤，不要调用本方法。
    ///
    /// 游戏场景里的角色本体不闪：Player.controller 的 Player_Hurt 状态目前没有入口，
    /// 所以下面这句 SetTrigger 是空触发，不会产生任何画面变化。
    /// 以后想恢复本体白闪，只要在 Animator 里给 PlayerIdle → Player_Hurt 连一条
    /// isHurt 条件的转移即可，代码这里不用改。
    /// </summary>
    public void PlayHurtAnimation()
    {
        if (BodyAnimator != null)
            BodyAnimator.SetTrigger(IsHurtHash);

        BattleUI.Instance?.PlayPlayerHurt();
    }

    /// <summary>
    /// 玩家攻击。目前只有战斗界面头像有攻击动作（Player.controller 里没有 isAttack 参数，
    /// 世界精灵也没有对应的攻击片段），所以这里只驱动头像。
    /// </summary>
    public void PlayAttackAnimation()
    {
        BattleUI.Instance?.PlayPlayerAttack();
    }

    // ============================================================
    //  IKeyInventory 接口（供 DoorController 调用）
    // ============================================================

    public bool HasKey(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  return yellowKeys > 0;
            case KeyType.Blue:    return blueKeys > 0;
            case KeyType.Red:     return redKeys > 0;
            case KeyType.Psyche: return psycheKeys > 0;
            case KeyType.Aeon:    return aeonKeys > 0;
            default:              return false;
        }
    }

    public void UseKey(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  if (yellowKeys > 0) yellowKeys--; break;
            case KeyType.Blue:    if (blueKeys > 0) blueKeys--; break;
            case KeyType.Red:     if (redKeys > 0) redKeys--; break;
            case KeyType.Psyche: if (psycheKeys > 0) psycheKeys--; break;
            case KeyType.Aeon:    if (aeonKeys > 0) aeonKeys--; break;
        }
        Debug.Log($"[PlayerData] 使用 {keyType} 钥匙（剩余 {GetKeyCount(keyType)}）");
    }

    public void AddKey(KeyType keyType, int amount = 1)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  yellowKeys += amount; break;
            case KeyType.Blue:    blueKeys += amount; break;
            case KeyType.Red:     redKeys += amount; break;
            case KeyType.Psyche: psycheKeys += amount; break;
            case KeyType.Aeon:    aeonKeys += amount; break;
        }
        Debug.Log($"[PlayerData] 获得 {amount} 把 {keyType} 钥匙（总计 {GetKeyCount(keyType)}）");
    }

    public int GetKeyCount(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  return yellowKeys;
            case KeyType.Blue:    return blueKeys;
            case KeyType.Red:     return redKeys;
            case KeyType.Psyche: return psycheKeys;
            case KeyType.Aeon:    return aeonKeys;
            default:              return 0;
        }
    }

    // ============================================================
    //  传送器
    // ============================================================

    public void AddUpTeleporter(int amount = 1)
    {
        upTeleporterCount += amount;
        Debug.Log($"[PlayerData] 获得 {amount} 个上楼传送器（总计 {upTeleporterCount}）");
    }

    public void AddDownTeleporter(int amount = 1)
    {
        downTeleporterCount += amount;
        Debug.Log($"[PlayerData] 获得 {amount} 个下楼传送器（总计 {downTeleporterCount}）");
    }

    /// <summary>使用一个上楼传送器，数量不足时返回 false。</summary>
    public bool UseUpTeleporter()
    {
        if (upTeleporterCount <= 0) return false;
        upTeleporterCount--;
        Debug.Log($"[PlayerData] 使用上楼传送器（剩余 {upTeleporterCount}）");
        return true;
    }

    /// <summary>使用一个下楼传送器，数量不足时返回 false。</summary>
    public bool UseDownTeleporter()
    {
        if (downTeleporterCount <= 0) return false;
        downTeleporterCount--;
        Debug.Log($"[PlayerData] 使用下楼传送器（剩余 {downTeleporterCount}）");
        return true;
    }

    // ============================================================
    //  敌人减半道具
    // ============================================================

    public void AddEnemyHalveItem(int amount = 1)
    {
        enemyHalveItemCount += amount;
        Debug.Log($"[PlayerData] 获得 {amount} 个圣水（总计 {enemyHalveItemCount}）");
    }

    /// <summary>使用一个敌人减半道具：下一场战斗敌人血量减半。数量不足时返回 false。</summary>
    public bool UseEnemyHalveItem()
    {
        if (enemyHalveItemCount <= 0) return false;
        enemyHalveItemCount--;
        pendingEnemyHalveBattles++;
        Debug.Log($"[PlayerData] 使用圣水（剩余 {enemyHalveItemCount}，待生效 {pendingEnemyHalveBattles} 场）");
        return true;
    }

    /// <summary>战斗开始时消耗一次减半效果。返回 true 表示本场敌人血量应减半。</summary>
    public bool ConsumeEnemyHalve()
    {
        if (pendingEnemyHalveBattles <= 0) return false;
        pendingEnemyHalveBattles--;
        Debug.Log($"[PlayerData] 敌人减半效果生效（剩余 {pendingEnemyHalveBattles} 场）");
        return true;
    }

    // ============================================================
    //  属性修改
    //
    //  取整口径（2026-09-20 定稿，与战斗内修正层同一规则）：**取整方向一律偏向玩家**。
    //  带系数的加成一律 Mathf.CeilToInt(amount * xxxMultiplier / 100f)，不要用整数除法 ——
    //  那是向下截断，等于少给，基础值小的时候还会把加成整个抹掉。
    //  AddAttack / AddDefense / AddHP / AddGold 都返回「实际增量」，调用方要显示数字就用它，
    //  不要自己再抄一遍公式（抄了就会和真实值漂移）。
    // ============================================================

    /// <summary>
    /// 恢复生命值。传入的是「基础治疗量」，会再乘一次生命系数 hpMultiplier ——
    /// 这是给祝福、道具这类「成长性回复」用的（和 AddHP 同口径）。
    /// ⚠️ 按公式算出来的回复（吸血、按实际伤害量回复等）不要走这里，要用 HealRaw，
    ///    否则会被 hpMultiplier 放大，而扣血那侧并没有对应的缩小，两边不对称。
    /// </summary>
    public void Heal(int amount)
    {
        int actualHeal = Mathf.CeilToInt(amount * hpMultiplier / 100f);
        hp += actualHeal;
        Debug.Log($"[PlayerData] 恢复 {amount}×{hpMultiplier}%={actualHeal} HP（当前 {hp}）");
    }

    /// <summary>
    /// 精确回血：加多少就是多少，不乘 hpMultiplier。
    /// 用于吸血这类「伤害 × 系数」算出来的回复量，与 SubtractHP（同样不乘 hpMultiplier）对称。
    /// </summary>
    public void HealRaw(int amount)
    {
        if (amount <= 0) return;
        hp += amount;
    }

    /// <summary>
    /// IBattleUnit 实现：按减伤结算伤害。等价于 SubtractHP，
    /// 但换个明确的名字 —— 敌人的同名方法语义正好相反（那边是真实伤害、不过减伤）。
    /// </summary>
    int IBattleUnit.ReceiveDamage(int amount) => SubtractHP(amount);

    /// <summary>直接设置生命值（用于濒死回复等机制）。</summary>
    public void SetHP(int value)
    {
        hp = Mathf.Max(0, value);
    }

    /// <summary>
    /// 直接扣血，不计算防御（用于反伤等机制）。返回实际扣除的HP。
    /// </summary>
    public int SubtractHP(int amount)
    {
        // 读属性而非字段：战斗中的减伤加成（如异乡人）要走战斗内修正层
        int reduced = amount * (100 - DamageReduction) / 100;
        hp -= reduced;
        if (hp < 0) hp = 0;
        return reduced;
    }

    /// <summary>
    /// 直接扣血但不低于 1 点生命值，且无视减伤系数。
    /// 用于一切「不该致死的扣血」：夹击、爱的祝福的每回合扣血等。
    /// 返回实际扣除的HP。
    /// </summary>
    public int SubtractRawHPKeepAlive(int amount)
    {
        int maxLoss = Mathf.Max(0, hp - 1);
        int actual = Mathf.Min(amount, maxLoss);
        hp -= actual;
        return actual;
    }

    /// <summary>加攻击力，返回实际增加量（已乘攻击系数）。</summary>
    public int AddAttack(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddAttack))) return 0;
        int actualGain = Mathf.CeilToInt(amount * attackMultiplier / 100f);
        attack += actualGain;
        Debug.Log($"[PlayerData] 攻击力 +{amount}×{attackMultiplier}%={actualGain}（当前 {attack}）");
        return actualGain;
    }

    public void AddAttackCount(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddAttackCount))) return;
        attackCount += amount;
        Debug.Log($"[PlayerData] 攻击段数 +{amount}（当前 {attackCount}）");
    }

    /// <summary>加防御力，返回实际增加量（已乘防御系数）。</summary>
    public int AddDefense(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddDefense))) return 0;
        int actualGain = Mathf.CeilToInt(amount * defenseMultiplier / 100f);
        defense += actualGain;
        Debug.Log($"[PlayerData] 防御力 +{amount}×{defenseMultiplier}%={actualGain}（当前 {defense}）");
        return actualGain;
    }

    /// <summary>
    /// 加金币。刻意**不加** RequireOutOfBattle —— 它正好是战斗中要用的：
    /// 战斗奖励（BattleManager.EndBattle）和恩惠的祝福都在战斗结算时加金币。
    /// </summary>
    public int AddGold(int amount)
    {
        int actualGold = Mathf.CeilToInt(amount * goldMultiplier / 100f);
        gold += actualGold;
        Debug.Log($"[PlayerData] 金币 +{amount}×{goldMultiplier}%={actualGold}（当前 {gold}）");
        return actualGold;
    }

    /// <summary>
    /// 花费金币。返回是否成功（金币不足时失败）。
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        Debug.Log($"[PlayerData] 金币 -{amount}（剩余 {gold}）");
        return true;
    }

    public void AddManaMax(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddManaMax))) return;
        manaMax += amount;
        Debug.Log($"[PlayerData] 魔力上限 +{amount}（当前 {manaMax}）");
    }

    public void AddManaCharge(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddManaCharge))) return;
        manaCharge += amount;
        Debug.Log($"[PlayerData] 魔力充能 +{amount}（当前 {manaCharge}）");
    }

    public void AddSpeed(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddSpeed))) return;
        speed += amount;
        Debug.Log($"[PlayerData] 速度 +{amount}（当前 {speed}）");
    }

    public void AddDamageReduction(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddDamageReduction))) return;
        damageReduction += amount;
        Debug.Log($"[PlayerData] 减伤系数 +{amount}%（当前 {damageReduction}%）");
    }

    public void AddAttackMultiplier(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddAttackMultiplier))) return;
        attackMultiplier += amount;
        Debug.Log($"[PlayerData] 攻击系数 +{amount}%（当前 {attackMultiplier}%）");
    }

    public void AddDefenseMultiplier(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddDefenseMultiplier))) return;
        defenseMultiplier += amount;
        Debug.Log($"[PlayerData] 防御系数 +{amount}%（当前 {defenseMultiplier}%）");
    }

    /// <summary>加生命值，返回实际增加量（已乘生命系数）。</summary>
    public int AddHP(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddHP))) return 0;
        int actualGain = Mathf.CeilToInt(amount * hpMultiplier / 100f);
        hp += actualGain;
        Debug.Log($"[PlayerData] 生命值 +{amount}×{hpMultiplier}%={actualGain}（当前 {hp}）");
        return actualGain;
    }

    public void AddHPMultiplier(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddHPMultiplier))) return;
        hpMultiplier += amount;
        Debug.Log($"[PlayerData] 生命系数 +{amount}%（当前 {hpMultiplier}%）");
    }

    public void AddGoldMultiplier(int amount)
    {
        if (!RequireOutOfBattle(nameof(AddGoldMultiplier))) return;
        goldMultiplier += amount;
        Debug.Log($"[PlayerData] 金币系数 +{amount}%（当前 {goldMultiplier}%）");
    }

    /// <summary>
    /// 统一属性增益接口，供 StatBoostPickup / MerchantPurchase 调用。
    /// 属【战前接口】：战斗中调用会被拦下。
    /// </summary>
    public void ApplyStatBoost(StatBoostType type, int value)
    {
        if (!RequireOutOfBattle(nameof(ApplyStatBoost))) return;

        switch (type)
        {
            case StatBoostType.HP:             AddHP(value);          break;
            case StatBoostType.Attack:     AddAttack(value);      break;
            case StatBoostType.Defense:    AddDefense(value);     break;
            case StatBoostType.ManaMax:    AddManaMax(value);     break;
            case StatBoostType.ManaCharge: AddManaCharge(value);  break;
            case StatBoostType.Speed:      AddSpeed(value);       break;
            case StatBoostType.DamageReduction: AddDamageReduction(value); break;
            case StatBoostType.AttackMultiplier:   AddAttackMultiplier(value);   break;
            case StatBoostType.DefenseMultiplier:  AddDefenseMultiplier(value);  break;
            case StatBoostType.HPMultiplier:       AddHPMultiplier(value);       break;
            case StatBoostType.GoldMultiplier:     AddGoldMultiplier(value);     break;
        }
    }

    /// <summary>
    /// 应用祝福加成，由 BlessingManager 在选择后调用。
    /// 属【战前接口】：入口处就拦一道，这样 ApplyStatBonus / ApplyPercentBonus
    /// 里那些直写字段的代码不会成为绕过守卫的第三条路径。
    /// </summary>
    public void ApplyBlessing(BlessingData blessing)
    {
        if (blessing == null) return;
        if (!RequireOutOfBattle(nameof(ApplyBlessing))) return;

        // 登记「已获得祝福」记录（存档用）。放在守卫之后：战斗中调用被拦时不应计数。
        BlessingManager.Instance?.RecordObtainedBlessing(blessing.id);

        switch (blessing.type)
        {
            case BlessingType.DirectBonus:
                ApplyStatBonus(blessing);
                ApplyPercentBonus(blessing);
                break;

            case BlessingType.Conditional:
                ApplyConditional(blessing);
                break;
        }
    }

    private void ApplyConditional(BlessingData b)
    {
        BlessingEffect effect = BlessingEffect.Create(b.id);

        if (effect == null)
        {
            Debug.LogWarning($"[PlayerData] 未注册的特殊祝福 ID：{b.id}");
            return;
        }

        string effectId = b.id.ToString();
        BlessingManager manager = BlessingManager.Instance;

        // 先判断是首次获得还是叠加升级：重复选择 = 升级，
        // 升级路径由 AddEffect 内部对 existing 实例做 AddLevel + OnLevelUp。
        bool firstTime = manager == null || !manager.HasEffect(effectId);

        manager?.AddEffect(effectId, effect, this);

        // OnAcquired = 一生一次的开荒加成，只在首次获得时调用。
        // 注意：升级时 effect 是刚 new 出来、随即被丢弃的实例（真正生效的是 activeEffects 里那个），
        // 所以这里绝不能无条件调用 —— 『智慧』升一级会变成 +1000(OnLevelUp) 再 +1000(OnAcquired)。
        if (firstTime)
            effect.OnAcquired(this);

        // 日志取真正生效的实例，否则升级时会打印 Lv.1 的描述
        BlessingEffect live = manager != null ? (manager.GetEffect<BlessingEffect>(effectId) ?? effect) : effect;
        int level = live != null ? live.Level : 1;
        Debug.Log($"[PlayerData] {(firstTime ? "获得" : "升级")}特殊祝福「{b.blessingName}」Lv.{level}（{live.GetEffectDescription()}）");
    }

    private void ApplyStatBonus(BlessingData b)
    {
        attack        += Mathf.CeilToInt(b.attackBonus * attackMultiplier / 100f);
        defense       += Mathf.CeilToInt(b.defenseBonus * defenseMultiplier / 100f);
        manaMax       += b.manaMaxBonus;
        manaCharge    += b.manaChargeBonus;
        speed         += b.speedBonus;
        int hpChange = Mathf.CeilToInt(b.hpBonus * hpMultiplier / 100f);
        hp += hpChange;
        // 祝福扣血最低只会扣到 1 点生命值，不会致命
        if (hpChange < 0 && hp < 1) hp = 1;
        attackCount   += b.attackCountBonus;
        lifeSteal     += b.lifeStealBonus;
        reflectDamage += b.reflectDamageBonus;
        damageReduction   += b.damageReductionBonus;
        attackMultiplier  += b.attackMultiplierBonus;
        defenseMultiplier += b.defenseMultiplierBonus;
        hpMultiplier      += b.hpMultiplierBonus;
        goldMultiplier    += b.goldMultiplierBonus;
        yellowKeys  += b.yellowKeyBonus;
        blueKeys    += b.blueKeyBonus;
        redKeys     += b.redKeyBonus;
        psycheKeys  += b.psycheKeyBonus;
        aeonKeys    += b.aeonKeyBonus;

        Debug.Log($"[PlayerData] 获得祝福「{b.blessingName}」攻+{b.attackBonus} 防+{b.defenseBonus} 速+{b.speedBonus} HP+{b.hpBonus} 段数+{b.attackCountBonus} 吸血+{b.lifeStealBonus} 反伤+{b.reflectDamageBonus} 减伤+{b.damageReductionBonus}%");
    }

    private void ApplyPercentBonus(BlessingData b)
    {
        // 向上取整（偏向玩家）：基础值小的时候，向下截断会把加成整个抹掉
        int hpGain      = Mathf.CeilToInt(hp * b.hpPercentBonus / 100f);
        int atkGain     = Mathf.CeilToInt(attack * b.attackPercentBonus / 100f);
        int defGain     = Mathf.CeilToInt(defense * b.defensePercentBonus / 100f);

        hp      += hpGain;
        attack  += atkGain;
        defense += defGain;

        Debug.Log($"[PlayerData] 获得百分比加成「{b.blessingName}」HP+{b.hpPercentBonus}%({hpGain}) 攻+{b.attackPercentBonus}%({atkGain}) 防+{b.defensePercentBonus}%({defGain})");
    }

    // ============================================================
    //  存档恢复接口（直接设置字段，供 SaveManager 读档使用）
    // ============================================================

    public void SetAttack(int v) => attack = v;
    public void SetDefense(int v) => defense = v;
    public void SetAttackCount(int v) => attackCount = v;
    public void SetLifeSteal(int v) => lifeSteal = v;
    public void SetReflectDamage(int v) => reflectDamage = v;
    public void SetDamageReduction(int v) => damageReduction = v;
    public void SetManaMax(int v) => manaMax = v;
    public void SetSpeed(int v) => speed = v;
    public void SetGold(int v) => gold = v;
    public void SetGoldMultiplier(int v) => goldMultiplier = v;
    public void SetHPMultiplier(int v) => hpMultiplier = v;
    public void SetAttackMultiplier(int v) => attackMultiplier = v;
    public void SetDefenseMultiplier(int v) => defenseMultiplier = v;

    /// <summary>直接设置 Aeon 钥匙数量（供全局存档覆盖）</summary>
    public void SetAeonKeys(int v) => aeonKeys = v;

    /// <summary>直接设置神圣火花数量（供全局存档覆盖 / Inspector 调试）</summary>
    public void SetDivineSpark(int v) => divineSpark = v;

    /// <summary>神圣火花 +amount（拾取时调用；落盘由 SaveManager 负责）</summary>
    public void AddDivineSpark(int amount = 1)
    {
        divineSpark += amount;
        Debug.Log($"[PlayerData] 获得 {amount} 个神圣火花（总计 {divineSpark}）");
    }

    /// <summary>直接设置上楼传送器数量（供读档恢复）</summary>
    public void SetUpTeleporterCount(int v) => upTeleporterCount = v;

    /// <summary>直接设置下楼传送器数量（供读档恢复）</summary>
    public void SetDownTeleporterCount(int v) => downTeleporterCount = v;

    /// <summary>直接设置敌人减半道具数量（供读档恢复）</summary>
    public void SetEnemyHalveItemCount(int v) => enemyHalveItemCount = v;

    /// <summary>直接设置待生效的敌人减半场次（供读档恢复）</summary>
    public void SetPendingEnemyHalveBattles(int v) => pendingEnemyHalveBattles = v;

    /// <summary>直接设置指定类型钥匙数量（供读档恢复）</summary>
    public void SetKeyCountDirect(KeyType keyType, int count)
    {
        switch (keyType)
        {
            case KeyType.Yellow: yellowKeys = count; break;
            case KeyType.Blue:   blueKeys   = count; break;
            case KeyType.Red:    redKeys    = count; break;
            case KeyType.Psyche: psycheKeys = count; break;
            case KeyType.Aeon:   aeonKeys   = count; break;
        }
    }
}
