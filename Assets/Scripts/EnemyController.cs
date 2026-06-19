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
    [SerializeField] private int attack = 10;
    [SerializeField] private int defense = 5;
    [SerializeField] private int hp = 50;
    [SerializeField] private int goldReward = 5;

    [Header("特殊属性")]
    [SerializeField] private bool firstStrike = false;
    [SerializeField] private bool rangedAttack = false;

    private bool isDefeated = false;

    // ============================================================
    //  公开只读属性
    // ============================================================
    public string EnemyName  => enemyName;
    public int Attack        => attack;
    public int Defense       => defense;
    public int HP            => hp;
    public int GoldReward    => goldReward;
    public bool FirstStrike  => firstStrike;
    public bool RangedAttack => rangedAttack;
    public bool IsDefeated   => isDefeated;

    void Awake()
    {
        // 如果设置了 EnemyData 资产，自动填充属性
        if (enemyData != null)
        {
            enemyName    = enemyData.enemyName;
            enemySprite  = enemyData.enemySprite;
            attack       = enemyData.attack;
            defense      = enemyData.defense;
            hp           = enemyData.hp;
            goldReward   = enemyData.goldReward;
            firstStrike  = enemyData.firstStrike;
            rangedAttack = enemyData.rangedAttack;
        }

        // 初始化外观
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (enemySprite != null && sr != null)
            sr.sprite = enemySprite;
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
