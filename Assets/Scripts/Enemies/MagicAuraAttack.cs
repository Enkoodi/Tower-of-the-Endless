using UnityEngine;

/// <summary>
/// 魔力伤害光环 — 挂载在敌人身上。
/// 玩家移动到该敌人上下左右四个相邻格子时，对玩家造成伤害。
/// 伤害 = 敌人魔力上限（ManaMax），不消耗魔力充能，无限次数。
/// 预留 IMagicDamageImmune 接口供玩家后期免疫。
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class MagicAuraAttack : MonoBehaviour
{
    [Header("伤害配置")]
    [Tooltip("伤害倍率（百分比），100 = 魔力上限的100%")]
    [SerializeField] private int damagePercent = 100;

    private EnemyController enemyController;
    private Vector2Int lastPlayerGridPos;
    private bool hasLastPlayerPos;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
    }

    private void Update()
    {
        if (enemyController == null || enemyController.IsDefeated) return;

        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen?.CurrentMap == null) return;

        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null || player.IsDead) return;

        // 计算玩家网格坐标
        int width  = mapGen.CurrentMap.width;
        int height = mapGen.CurrentMap.height;
        float offsetX = -(width  - 1) / 2f;
        float offsetY =  (height - 1) / 2f;

        Vector3 worldPos = player.transform.position;
        Vector2Int playerGridPos = new Vector2Int(
            Mathf.RoundToInt(worldPos.x - offsetX),
            Mathf.RoundToInt(offsetY - worldPos.y)
        );

        // 仅在玩家移动到新位置时检测（避免每帧重复触发）
        if (hasLastPlayerPos && playerGridPos == lastPlayerGridPos) return;

        lastPlayerGridPos = playerGridPos;
        hasLastPlayerPos = true;

        // 检查是否处于上下左右四个相邻格子之一
        if (!IsAdjacent(playerGridPos)) return;

        // 免疫检查
        if (IsPlayerImmune(player)) return;

        // 造成伤害（不消耗魔力充能，无限次数）
        int damage = enemyController.ManaMax * damagePercent / 100;
        player.SubtractHP(damage);
        Debug.Log($"[MagicAura] {enemyController.EnemyName} 的魔力攻击对玩家造成 {damage} 点伤害！" +
                  $"（魔力上限={enemyController.ManaMax}，倍率={damagePercent}%）");
    }

    /// <summary>检查玩家是否在上下左右四个相邻格子之一</summary>
    private bool IsAdjacent(Vector2Int playerPos)
    {
        Vector2Int diff = playerPos - enemyController.gridPosition;
        return Mathf.Abs(diff.x) + Mathf.Abs(diff.y) == 1;
    }

    /// <summary>检查玩家是否免疫魔力伤害</summary>
    private bool IsPlayerImmune(PlayerData player)
    {
        return player.GetComponent<PlayerImmunity>() != null;
    }
}

/// <summary>
/// 魔力伤害免疫接口 — 挂载在玩家身上即可免疫 MagicAuraAttack 的伤害。
/// 供玩家后期获得免疫能力时使用。
/// </summary>
public interface IMagicDamageImmune
{
    bool IsImmuneToMagicDamage { get; }
}
