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
    private int damageReduction = 0;
    private int manaCharge = 0;
    private int manaMax = 100;
    private int speed = 5;
    private int goldReward = 0;

    private bool isDefeated = false;

    /// <summary>是否为脚本敌人（对话战斗等），为 true 时击败不记录楼层记忆、不生成掉落</summary>
    [HideInInspector] public bool isScriptedEnemy;

    /// <summary>敌人被击败时触发，参数为被击败的敌人自身</summary>
    public event System.Action<EnemyController> OnDefeated;

    /// <summary>在地图网格中的坐标（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由 MapGenerator 在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    // ============================================================
    //  公开属性
    // ============================================================
    public string EnemyName    => enemyName;
    public int Attack          => attack;
    public int Defense         => defense;
    public int HP              => hp;
    public void SetHP(int value) { hp = Mathf.Max(0, value); }
    public int AttackCount     => attackCount;
    public int LifeSteal       => lifeSteal;
    public int ReflectDamage   => reflectDamage;
    public int DamageReduction => damageReduction;
    public int ManaCharge      { get => manaCharge; set => manaCharge = value; }
    public int ManaMax         => manaMax;
    public int Speed           => speed;
    public int GoldReward      => goldReward;
    public bool IsDefeated     => isDefeated;
    public Sprite EnemySprite  => enemySprite;
    public EnemyStats Stats     => stats;

    // ============================================================
    //  生命周期
    // ============================================================

    private void Awake()
    {
        LoadFromStats();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (enemySprite != null && sr != null)
            sr.sprite = enemySprite;
    }

    /// <summary>
    /// 运行时用指定数据资产初始化敌人（供对话战斗等动态生成敌人的脚本调用）。
    /// </summary>
    public void InitWithStats(EnemyStats newStats)
    {
        stats = newStats;
        LoadFromStats();

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (enemySprite != null && sr != null)
            sr.sprite = enemySprite;
    }

    private void LoadFromStats()
    {
        if (stats == null) return;

        enemyName     = stats.enemyName;
        enemySprite   = stats.enemySprite;
        hp            = stats.hp;
        attack        = stats.attack;
        defense       = stats.defense;
        attackCount   = stats.attackCount;
        lifeSteal     = stats.lifeSteal;
        reflectDamage = stats.reflectDamage;
        damageReduction = stats.damageReduction;
        manaCharge    = stats.manaCharge;
        manaMax       = stats.manaMax;
        speed         = stats.speed;
        goldReward    = stats.goldReward;
    }

    // ============================================================
    //  战斗接口
    // ============================================================

    public int TakeDamage(int rawAtk)
    {
        int damage = Mathf.Max(0, rawAtk - defense);
        int reduced = damage * (100 - damageReduction) / 100;
        hp -= reduced;
        if (hp < 0) hp = 0;
        return reduced;
    }

    public int TakeRawDamage(int amount)
    {
        int reduced = amount * (100 - damageReduction) / 100;
        hp -= reduced;
        if (hp < 0) hp = 0;
        return reduced;
    }

    /// <summary>
    /// 真实扣血 — 无视防御和减伤系数。
    /// </summary>
    public int SubtractHP(int amount)
    {
        hp -= amount;
        if (hp < 0) hp = 0;
        return amount;
    }

    public void Heal(int amount) { hp += amount; }

    public void Defeat()
    {
        if (isDefeated) return;
        isDefeated = true;

        // 普通敌人：记录到楼层记忆（脚本敌人不记录，由 NpcBattler 等处理NPC记忆）
        if (!isScriptedEnemy)
        {
            if (FloorMemoryManager.Instance != null)
                FloorMemoryManager.Instance.GetOrCreateState(floorNumber).MarkEnemyDefeated(gridPosition);
            else
                Debug.LogWarning($"[Enemy] FloorMemoryManager.Instance 为 null，无法记录击败：{enemyName} (楼层{floorNumber}, 坐标{gridPosition})");
        }

        // 生成掉落物（脚本敌人若在预制体上配置了 ItemDrop，同样会掉落，与正常战斗一致）
        DropManager.Instance?.OnEnemyDefeated(this);

        // 通知订阅者（如战斗门等）
        OnDefeated?.Invoke(this);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        gameObject.SetActive(false);

        Debug.Log($"[Enemy] {enemyName} 被击败！(楼层{floorNumber}, 坐标{gridPosition})");
    }
}
