using UnityEngine;

/// <summary>
/// 敌人控制器 — 挂载在敌人 Prefab 上。
/// 可直接在 Inspector 中设置属性，或通过 EnemyData 资产一键填充。
/// 战斗逻辑由 PlayerData.TryFight() 处理。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyController : MonoBehaviour
{
    [Header("数据引用（可选：拖入 EnemyData 资产快速填充）")]
    [SerializeField] private EnemyData enemyData;

    [Header("基本信息")]
    [SerializeField] private string enemyName = "绿史莱姆";
    [SerializeField] private Sprite enemySprite;

    [Header("战斗属性")]
    [SerializeField] private int hp = 50;
    [SerializeField] private int attack = 10;
    [SerializeField] private int defense = 5;
    [SerializeField] private int attackCount = 1;
    [SerializeField] private int lifeSteal = 0;
    [SerializeField] private int reflectDamage = 0;
    [SerializeField] private int manaCharge = 0;
    [SerializeField] private int manaMax = 100;
    [SerializeField] private int speed = 5;
    [SerializeField] private int goldReward = 5;

    private bool isDefeated = false;

    // ============================================================
    //  公开只读属性
    // ============================================================
    public string EnemyName    => enemyName;
    public int Attack          => attack;
    public int Defense         => defense;
    public int HP              => hp;
    public int AttackCount     => attackCount;
    public int LifeSteal       => lifeSteal;
    public int ReflectDamage   => reflectDamage;
    public int ManaCharge      { get => manaCharge; set => manaCharge = value; }
    public int ManaMax         => manaMax;
    public int Speed           => speed;
    public int GoldReward      => goldReward;
    public bool IsDefeated     => isDefeated;

    void Awake()
    {
        // 如果设置了 EnemyData 资产，自动填充属性
        if (enemyData != null)
        {
            enemyName     = enemyData.enemyName;
            enemySprite   = enemyData.enemySprite;
            hp            = enemyData.hp;
            attack        = enemyData.attack;
            defense       = enemyData.defense;
            attackCount   = enemyData.attackCount;
            lifeSteal     = enemyData.lifeSteal;
            reflectDamage = enemyData.reflectDamage;
            manaCharge    = enemyData.manaCharge;
            manaMax        = enemyData.manaMax;
            speed         = enemyData.speed;
            goldReward    = enemyData.goldReward;
        }

        // 初始化外观
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (enemySprite != null && sr != null)
            sr.sprite = enemySprite;
    }

    /// <summary>
    /// 敌人受到伤害。返回实际受到的伤害值。
    /// </summary>
    public int TakeDamage(int rawAtk)
    {
        int damage = Mathf.Max(0, rawAtk - defense);
        hp -= damage;
        if (hp < 0) hp = 0;
        return damage;
    }

    /// <summary>
    /// 直接扣血，不再重复计算防御。
    /// </summary>
    public void TakeRawDamage(int amount)
    {
        hp -= amount;
        if (hp < 0) hp = 0;
    }

    /// <summary>
    /// 恢复生命值。
    /// </summary>
    public void Heal(int amount)
    {
        hp += amount;
    }

    /// <summary>
    /// 击败敌人（由 PlayerData 战斗结算时调用）。禁用碰撞体并隐藏。
    /// </summary>
    public void Defeat()
    {
        if (isDefeated) return;
        isDefeated = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        gameObject.SetActive(false);

        Debug.Log($"[Enemy] {enemyName} 被击败！");
    }
}
