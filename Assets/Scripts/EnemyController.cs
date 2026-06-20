using UnityEngine;

/// <summary>
/// 敌人控制器 — 挂载在敌人 Prefab 上。
/// 拖入 EnemyStats 资产即可，所有数值从资产读取。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyController : MonoBehaviour
{
    [Header("数据资产（右键 Create → MagicTower → Enemy Stats）")]
    [SerializeField] private EnemyStats stats;

    // ============================================================
    //  运行时字段
    // ============================================================
    private string enemyName = "???";
    private Sprite enemySprite;
    private int hp = 1;
    private int attack = 0;
    private int defense = 0;
    private int attackCount = 1;
    private int lifeSteal = 0;
    private int reflectDamage = 0;
    private int manaCharge = 0;
    private int manaMax = 100;
    private int speed = 5;
    private int goldReward = 0;

    private bool isDefeated = false;

    // ============================================================
    //  公开属性
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
    public Sprite EnemySprite  => enemySprite;

    // ============================================================
    //  生命周期
    // ============================================================

    private void Awake()
    {
        if (stats != null)
        {
            enemyName     = stats.enemyName;
            enemySprite   = stats.enemySprite;
            hp            = stats.hp;
            attack        = stats.attack;
            defense       = stats.defense;
            attackCount   = stats.attackCount;
            lifeSteal     = stats.lifeSteal;
            reflectDamage = stats.reflectDamage;
            manaCharge    = stats.manaCharge;
            manaMax       = stats.manaMax;
            speed         = stats.speed;
            goldReward    = stats.goldReward;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (enemySprite != null && sr != null)
            sr.sprite = enemySprite;
    }

    // ============================================================
    //  战斗接口
    // ============================================================

    public int TakeDamage(int rawAtk)
    {
        int damage = Mathf.Max(0, rawAtk - defense);
        hp -= damage;
        if (hp < 0) hp = 0;
        return damage;
    }

    public void TakeRawDamage(int amount)
    {
        hp -= amount;
        if (hp < 0) hp = 0;
    }

    public void Heal(int amount) { hp += amount; }

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
