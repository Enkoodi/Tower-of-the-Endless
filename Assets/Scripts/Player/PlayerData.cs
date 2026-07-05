using UnityEngine;

/// <summary>
/// 玩家数据 — 挂载在玩家 GameObject 上。
/// 持有钥匙、战斗属性、金币，并提供战斗逻辑。
/// 实现 IKeyInventory 供门系统查询钥匙。
/// 实现 IPlayerHealth 供 Psyche 生命之门扣除 HP。
/// </summary>
public class PlayerData : MonoBehaviour, IKeyInventory, IPlayerHealth
{
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

    // ============================================================
    //  公开只读属性
    // ============================================================
    public int Attack        => attack;
    public int Defense       => defense;
    public int HP            => hp;
    public int AttackCount   => attackCount;
    public int LifeSteal     => lifeSteal;
    public int ReflectDamage => reflectDamage;
    public int DamageReduction => damageReduction;
    public int ManaCharge    { get => manaCharge; set => manaCharge = value; }
    public int ManaMax       => manaMax;
    public int Speed         => speed;
    public int Gold          => gold;
    public bool IsDead       => hp <= 0;

    // 系数
    public int GoldMultiplier    => goldMultiplier;
    public int HPMultiplier      => hpMultiplier;
    public int AttackMultiplier  => attackMultiplier;
    public int DefenseMultiplier => defenseMultiplier;

    // ============================================================
    //  战斗系统
    // ============================================================

    /// <summary>
    /// 与敌人战斗。返回 true 表示胜利，false 表示失败/逃跑。
    /// 魔塔规则：玩家攻击力 > 敌人防御力时，造成 (攻-防) 伤害，否则无法破防。
    /// </summary>
    public bool TryFight(EnemyController enemy)
    {
        if (enemy == null || enemy.IsDefeated)
        {
            Debug.Log("[PlayerData] 敌人无效或已击败");
            return true;
        }

        int enemyAtk  = enemy.Attack;
        int enemyHp   = enemy.HP;
        
        Debug.Log($"[战斗] 遭遇 {enemy.EnemyName}！");
        Debug.Log($"[战斗] 我方 攻:{attack} 防:{defense} 血:{hp}");
        Debug.Log($"[战斗] 敌方 攻:{enemyAtk} 防:{enemy.Defense} 血:{enemyHp}");

        // 先手判定：敌人速度更高则先手
        if (enemy.Speed > speed)
        {
            Debug.Log($"[战斗] {enemy.EnemyName} 速度更快，先手攻击！");
            TakeDamage(enemy.Attack);
        }

        // 玩家攻击：物理伤害 = (攻-防) * 段数，魔力加成 = min(魔力充能, 魔力上限)
        int physicalDamage = (attack - enemy.Defense) * attackCount;
        int manaBonus = manaCharge < manaMax ? manaCharge : manaMax;
        int damageToEnemy = physicalDamage + manaBonus;
        if (damageToEnemy <= 0)
        {
            Debug.Log($"[战斗] 无法破防！物理伤害({physicalDamage}) + 魔力加成({manaBonus}) <= 0");
            return false;
        }

        // 敌人伤害：同公式
        int enemyPhysicalDamage = (enemyAtk - defense) * enemy.AttackCount;
        int enemyManaBonus = enemy.ManaCharge < enemy.ManaMax ? enemy.ManaCharge : enemy.ManaMax;
        int enemyDamageToPlayer = enemyPhysicalDamage + enemyManaBonus;

        // 计算需要几回合击杀敌人
        int turnsToKill = Mathf.CeilToInt((float)enemyHp / damageToEnemy);

        // 每回合玩家先攻，敌人反击
        for (int turn = 0; turn < turnsToKill; turn++)
        {
            // 玩家攻击
            enemyHp -= damageToEnemy;
            Debug.Log($"[战斗] 第 {turn + 1} 回合：对 {enemy.EnemyName} 造成 {damageToEnemy} 伤害（剩余 {Mathf.Max(0, enemyHp)}）");

            // 吸血：物理伤害 * 吸血系数 / 100
            int rawPhysical = Mathf.Max(0, physicalDamage);
            int steal = rawPhysical * lifeSteal / 100;
            if (steal > 0) Heal(steal);
            // 反伤：物理伤害 * 敌人反伤系数 / 100
            int reflect = rawPhysical * enemy.ReflectDamage / 100;
            if (reflect > 0) SubtractHP(reflect);

            if (enemyHp <= 0)
            {
                Debug.Log($"[战斗] {enemy.EnemyName} 被击败！");
                return FightResult(true, enemy);
            }

            // 敌人反击
            SubtractHP(enemyDamageToPlayer);

            // 敌人吸血：敌人物理伤害 * 敌人吸血系数 / 100
            int enemySteal = enemyPhysicalDamage * enemy.LifeSteal / 100;
            if (enemySteal > 0) enemy.Heal(enemySteal);
            // 玩家反伤：敌人物理伤害 * 玩家反伤系数 / 100
            int playerReflect = enemyPhysicalDamage * reflectDamage / 100;
            if (playerReflect > 0) enemy.TakeRawDamage(playerReflect);
        }

        return FightResult(true, enemy);
    }

    /// <summary>
    /// 玩家受到伤害。返回实际受到的伤害值。
    /// </summary>
    public int TakeDamage(int rawAtk)
    {
        int damage = Mathf.Max(0, rawAtk - defense);
        int reduced = damage * (100 - damageReduction) / 100;
        hp -= reduced;
        if (hp < 0) hp = 0;
        return reduced;
    }

    /// <summary>
    /// 战斗结算：金币、击败敌人
    /// </summary>
    private bool FightResult(bool won, EnemyController enemy)
    {
        if (won)
        {
            gold += enemy.GoldReward;
            Debug.Log($"[战斗] 获得 {enemy.GoldReward} 金币（总计 {gold}）");
            enemy.Defeat();
        }
        return won;
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
    //  属性修改
    // ============================================================

    public void Heal(int amount)
    {
        int actualHeal = amount * hpMultiplier / 100;
        hp += actualHeal;
        Debug.Log($"[PlayerData] 恢复 {amount}×{hpMultiplier}%={actualHeal} HP（当前 {hp}）");
    }

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
        int reduced = amount * (100 - damageReduction) / 100;
        hp -= reduced;
        if (hp < 0) hp = 0;
        return reduced;
    }

    /// <summary>直接扣血，无视减伤和生命系数。</summary>
    public int SubtractRawHP(int amount)
    {
        int actual = Mathf.Min(amount, hp);
        hp -= amount;
        if (hp < 0) hp = 0;
        return actual;
    }

    public void AddAttack(int amount)
    {
        int actualGain = amount * attackMultiplier / 100;
        attack += actualGain;
        Debug.Log($"[PlayerData] 攻击力 +{amount}×{attackMultiplier}%={actualGain}（当前 {attack}）");
    }

    public void AddAttackCount(int amount)
    {
        attackCount += amount;
        Debug.Log($"[PlayerData] 攻击段数 +{amount}（当前 {attackCount}）");
    }

    public void AddDefense(int amount)
    {
        int actualGain = amount * defenseMultiplier / 100;
        defense += actualGain;
        Debug.Log($"[PlayerData] 防御力 +{amount}×{defenseMultiplier}%={actualGain}（当前 {defense}）");
    }

    public void AddGold(int amount)
    {
        int actualGold = amount * goldMultiplier / 100;
        gold += actualGold;
        Debug.Log($"[PlayerData] 金币 +{amount}×{goldMultiplier}%={actualGold}（当前 {gold}）");
    }

    public void AddManaMax(int amount)
    {
        manaMax += amount;
        Debug.Log($"[PlayerData] 魔力上限 +{amount}（当前 {manaMax}）");
    }

    public void AddManaCharge(int amount)
    {
        manaCharge += amount;
        Debug.Log($"[PlayerData] 魔力充能 +{amount}（当前 {manaCharge}）");
    }

    public void AddSpeed(int amount)
    {
        speed += amount;
        Debug.Log($"[PlayerData] 速度 +{amount}（当前 {speed}）");
    }

    public void AddDamageReduction(int amount)
    {
        damageReduction += amount;
        Debug.Log($"[PlayerData] 减伤系数 +{amount}%（当前 {damageReduction}%）");
    }

    public void AddAttackMultiplier(int amount)
    {
        attackMultiplier += amount;
        Debug.Log($"[PlayerData] 攻击系数 +{amount}%（当前 {attackMultiplier}%）");
    }

    public void AddDefenseMultiplier(int amount)
    {
        defenseMultiplier += amount;
        Debug.Log($"[PlayerData] 防御系数 +{amount}%（当前 {defenseMultiplier}%）");
    }

    public void AddHP(int amount)
    {
        int actualGain = amount * hpMultiplier / 100;
        hp += actualGain;
        Debug.Log($"[PlayerData] 生命值 +{amount}×{hpMultiplier}%={actualGain}（当前 {hp}）");
    }

    public void AddHPMultiplier(int amount)
    {
        hpMultiplier += amount;
        Debug.Log($"[PlayerData] 生命系数 +{amount}%（当前 {hpMultiplier}%）");
    }

    public void AddGoldMultiplier(int amount)
    {
        goldMultiplier += amount;
        Debug.Log($"[PlayerData] 金币系数 +{amount}%（当前 {goldMultiplier}%）");
    }

    /// <summary>
    /// 统一属性增益接口，供 StatBoostPickup 调用。
    /// </summary>
    public void ApplyStatBoost(StatBoostType type, int value)
    {
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
    /// </summary>
    public void ApplyBlessing(BlessingData blessing)
    {
        if (blessing == null) return;

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
        BlessingEffect effect = b.id switch
        {
            BlessingID.BythosBlessing => new BythosBlessingEffect(),
            BlessingID.AgapeBlessing => new AgapeBlessingEffect(),
            BlessingID.AletheiaBlessing => new AletheiaBlessingEffect(),
            BlessingID.Allotrioi => new AllotrioiEffect(),
            BlessingID.CharisBlessing => new CharisBlessingEffect(),
            BlessingID.DemiurgeBlessing => new DemiurgeBlessingEffect(),
            BlessingID.GnosisBlessing => new GnosisBlessingEffect(),
            BlessingID.KabbalahTree => new KabbalahTreeEffect(),
            BlessingID.Longinus => new LonginusEffect(),
            BlessingID.SigeBlessing => new SigeBlessingEffect(),
            BlessingID.SophiaBlessing => new SophiaBlessingEffect(),
            // TODO: 其他特殊祝福在此注册
            _ => null,
        };

        if (effect != null)
        {
            string effectId = b.id.ToString();
            BlessingManager.Instance?.AddEffect(effectId, effect, this);
            effect.OnAcquired(this); // 永久属性加成（仅首次获得时调用）
            Debug.Log($"[PlayerData] 获得特殊祝福「{b.blessingName}」({effect.GetEffectDescription()})");
        }
        else
        {
            Debug.LogWarning($"[PlayerData] 未注册的特殊祝福 ID：{b.id}");
        }
    }

    private void ApplyStatBonus(BlessingData b)
    {
        attack        += b.attackBonus * attackMultiplier / 100;
        defense       += b.defenseBonus * defenseMultiplier / 100;
        manaMax       += b.manaMaxBonus;
        manaCharge    += b.manaChargeBonus;
        speed         += b.speedBonus;
        hp            += b.hpBonus * hpMultiplier / 100;
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
        int hpGain      = hp * b.hpPercentBonus / 100;
        int atkGain     = attack * b.attackPercentBonus / 100;
        int defGain     = defense * b.defensePercentBonus / 100;

        hp      += hpGain;
        attack  += atkGain;
        defense += defGain;

        Debug.Log($"[PlayerData] 获得百分比加成「{b.blessingName}」HP+{b.hpPercentBonus}%({hpGain}) 攻+{b.attackPercentBonus}%({atkGain}) 防+{b.defensePercentBonus}%({defGain})");
    }
}
