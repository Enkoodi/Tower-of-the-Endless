using UnityEngine;

/// <summary>
/// 玩家数据 — 挂载在玩家 GameObject 上。
/// 持有钥匙、战斗属性、金币，并提供战斗逻辑。
/// 实现 IKeyInventory 供 DoorController 查询钥匙。
/// </summary>
public class PlayerData : MonoBehaviour, IKeyInventory
{
    // ============================================================
    //  Inspector 字段
    // ============================================================

    [Header("战斗属性")]
    [SerializeField] private int attack = 10;
    [SerializeField] private int defense = 5;
    [SerializeField] private int hp = 100;
    [SerializeField] private int maxHp = 100;

    [Header("金币")]
    [SerializeField] private int gold = 0;

    [Header("钥匙数量")]
    [SerializeField] private int yellowKeys = 1;
    [SerializeField] private int blueKeys = 0;
    [SerializeField] private int redKeys = 0;
    [SerializeField] private int scarletKeys = 0;
    [SerializeField] private int aeonKeys = 0;

    // ============================================================
    //  公开只读属性
    // ============================================================
    public int Attack => attack;
    public int Defense => defense;
    public int HP => hp;
    public int MaxHP => maxHp;
    public int Gold => gold;
    public bool IsDead => hp <= 0;

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

        int playerAtk = attack;
        int playerDef = defense;
        int enemyAtk  = enemy.Attack;
        int enemyDef  = enemy.Defense;
        int enemyHp   = enemy.HP;

        Debug.Log($"[战斗] 遭遇 {enemy.EnemyName}！");
        Debug.Log($"[战斗] 我方 攻:{playerAtk} 防:{playerDef} 血:{hp}");
        Debug.Log($"[战斗] 敌方 攻:{enemyAtk} 防:{enemyDef} 血:{enemyHp}");

        // 先手攻击
        if (enemy.FirstStrike)
        {
            Debug.Log($"[战斗] {enemy.EnemyName} 先手攻击！");
            TakeDamage(enemyAtk);
        }

        // 玩家攻击阶段
        int damageToEnemy = playerAtk - enemyDef;
        if (damageToEnemy <= 0)
        {
            Debug.Log($"[战斗] 无法破防！攻击力({playerAtk}) <= 敌人防御力({enemyDef})");
            return false;
        }

        // 计算需要几回合击杀敌人
        int turnsToKill = Mathf.CeilToInt((float)enemyHp / damageToEnemy);

        // 每回合玩家先攻（除非敌人先手已处理），敌人反击
        for (int turn = 0; turn < turnsToKill; turn++)
        {
            // 玩家攻击
            enemyHp -= damageToEnemy;
            Debug.Log($"[战斗] 第 {turn + 1} 回合：对 {enemy.EnemyName} 造成 {damageToEnemy} 伤害（剩余 {Mathf.Max(0, enemyHp)}）");

            if (enemyHp <= 0)
            {
                Debug.Log($"[战斗] {enemy.EnemyName} 被击败！");
                return FightResult(true, enemy);
            }

            // 敌人反击（最后一回合如果敌人已死则不反击）
            TakeDamage(enemyAtk);
        }

        return FightResult(true, enemy);
    }

    /// <summary>
    /// 玩家受到伤害。返回实际受到的伤害值。
    /// </summary>
    public int TakeDamage(int rawAtk)
    {
        int damage = Mathf.Max(0, rawAtk - defense);
        hp -= damage;
        if (hp < 0) hp = 0;
        return damage;
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
            case KeyType.Scarlet: return scarletKeys > 0;
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
            case KeyType.Scarlet: if (scarletKeys > 0) scarletKeys--; break;
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
            case KeyType.Scarlet: scarletKeys += amount; break;
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
            case KeyType.Scarlet: return scarletKeys;
            case KeyType.Aeon:    return aeonKeys;
            default:              return 0;
        }
    }

    // ============================================================
    //  属性修改
    // ============================================================

    public void Heal(int amount)
    {
        hp = Mathf.Min(hp + amount, maxHp);
        Debug.Log($"[PlayerData] 恢复 {amount} HP（当前 {hp}/{maxHp}）");
    }

    public void AddAttack(int amount)
    {
        attack += amount;
        Debug.Log($"[PlayerData] 攻击力 +{amount}（当前 {attack}）");
    }

    public void AddDefense(int amount)
    {
        defense += amount;
        Debug.Log($"[PlayerData] 防御力 +{amount}（当前 {defense}）");
    }

    public void AddGold(int amount)
    {
        gold += amount;
        Debug.Log($"[PlayerData] 金币 +{amount}（当前 {gold}）");
    }
}
