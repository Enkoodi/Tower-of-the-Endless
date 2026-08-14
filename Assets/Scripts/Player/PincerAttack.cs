using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 夹击攻击组件 — 挂载在具有夹击能力的角色上（包括玩家和敌人）。
/// 当玩家移动后或进入楼层时，若形成三个连续横/竖排列的角色（ABA或AAA），中间角色减少50%生命值。
/// - A = 挂有本脚本的角色（夹击者，可无限次参与夹击）
/// - B = 未挂本脚本的角色
/// - 被夹击的敌人最多扣血一次（受害者追踪，持久化到 FloorMemoryManager）
/// - 玩家可重复被夹击
/// - 免疫角色（isImmune=true）在中间位置不受伤害
/// - ABABA等连续形式中，最中间的A为玩家时左右两个B都会扣血
/// </summary>
public class PincerAttack : MonoBehaviour
{
    [Header("免疫夹击")]
    [Tooltip("勾选后，当此角色位于夹击中间位置时，不会受到50%生命值减少的效果")]
    [SerializeField] private bool isImmune = false;

    /// <summary>是否免疫夹击伤害</summary>
    public bool IsImmune => isImmune;

    // ============================================================
    //  公共入口
    // ============================================================

    /// <summary>
    /// 进入楼层时：恢复已被夹击敌人的HP，然后扫描预置的夹击阵型。
    /// （由 MapGenerator 在楼层加载完成后调用）
    /// </summary>
    public static void CheckFloorEntry(PlayerData player)
    {
        if (player == null) return;

        var (enemyMap, pincerMap, playerGridPos, floor) = BuildLookupMaps(player);
        if (enemyMap == null) return;

        FloorState state = FloorMemoryManager.Instance?.GetOrCreateState(floor);
        if (state != null)
        {
            // 恢复已被夹击敌人的剩余HP
            foreach (var kv in state.pinceredEnemies)
            {
                if (enemyMap.TryGetValue(kv.Key, out EnemyController enemy))
                {
                    enemy.SetHP(kv.Value);
                    Debug.Log($"[夹击] 恢复敌人 {enemy.EnemyName} 在 ({kv.Key.x},{kv.Key.y}) 的HP为 {kv.Value}");
                }
            }
        }

        // 从每个敌人位置扫描（仅右和上，避免重复计数）
        foreach (Vector2Int pos in new List<Vector2Int>(enemyMap.Keys))
        {
            CheckTripleFrom(pos, Vector2Int.right, playerGridPos, enemyMap, pincerMap, floor);
            CheckTripleFrom(pos, Vector2Int.up, playerGridPos, enemyMap, pincerMap, floor);
        }

        // 从玩家位置扫描
        CheckTripleFrom(playerGridPos, Vector2Int.right, playerGridPos, enemyMap, pincerMap, floor);
        CheckTripleFrom(playerGridPos, Vector2Int.up, playerGridPos, enemyMap, pincerMap, floor);
    }

    /// <summary>
    /// 玩家移动后检测夹击（由 PlayerMove 在移动完成后调用）。
    /// </summary>
    public static void CheckPincerFormation(PlayerData player)
    {
        if (player == null) return;

        var (enemyMap, pincerMap, playerGridPos, floor) = BuildLookupMaps(player);
        if (enemyMap == null) return;

        PincerAttack playerPincer = player.GetComponent<PincerAttack>();

        // 情况1：玩家在一端，检测四个方向
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (Vector2Int dir in dirs)
        {
            CheckTripleFrom(playerGridPos, dir, playerGridPos, enemyMap, pincerMap, floor);
        }

        // 情况2：玩家在中间 — 仅 up/right 避免同一对敌人被双向检测触发两次
        CheckPlayerInMiddle(playerGridPos, Vector2Int.up, player, playerPincer, enemyMap, pincerMap);
        CheckPlayerInMiddle(playerGridPos, Vector2Int.right, player, playerPincer, enemyMap, pincerMap);
    }

    // ============================================================
    //  查找表构建
    // ============================================================

    private static (Dictionary<Vector2Int, EnemyController> enemyMap,
                   Dictionary<Vector2Int, PincerAttack> pincerMap,
                   Vector2Int playerGridPos,
                   int floor)
        BuildLookupMaps(PlayerData player)
    {
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen?.CurrentMap == null)
            return (null, null, Vector2Int.zero, -1);

        int floor = mapGen.CurrentFloor;
        int width = mapGen.CurrentMap.width;
        int height = mapGen.CurrentMap.height;
        float offsetX = -(width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        // 计算玩家网格坐标
        Vector3 worldPos = player.transform.position;
        Vector2Int playerGridPos = new Vector2Int(
            Mathf.RoundToInt(worldPos.x - offsetX),
            Mathf.RoundToInt(offsetY - worldPos.y)
        );

        var enemyMap = new Dictionary<Vector2Int, EnemyController>();
        var pincerMap = new Dictionary<Vector2Int, PincerAttack>();

        foreach (EnemyController enemy in FindObjectsOfType<EnemyController>())
        {
            if (enemy.IsDefeated || enemy.floorNumber != floor) continue;
            enemyMap[enemy.gridPosition] = enemy;
            PincerAttack pa = enemy.GetComponent<PincerAttack>();
            if (pa != null)
                pincerMap[enemy.gridPosition] = pa;
        }

        // 玩家也加入 pincerMap（若挂有组件）
        PincerAttack playerPincer = player.GetComponent<PincerAttack>();
        if (playerPincer != null)
            pincerMap[playerGridPos] = playerPincer;

        return (enemyMap, pincerMap, playerGridPos, floor);
    }

    // ============================================================
    //  通用三元检测（pos0 为一端）
    //    pos0(A) — mid — far(A)
    //    两端必须是 A，中间角色减少50%HP
    //    夹击者（两端A）可无限次参与；受害者（中间）敌人最多扣血一次（持久化）
    // ============================================================
    private static void CheckTripleFrom(
        Vector2Int pos0, Vector2Int dir, Vector2Int playerGridPos,
        Dictionary<Vector2Int, EnemyController> enemyMap,
        Dictionary<Vector2Int, PincerAttack> pincerMap,
        int floor)
    {
        // pos0 必须是 A（夹击者）
        if (!pincerMap.TryGetValue(pos0, out PincerAttack pincer0)) return;

        Vector2Int midPos = pos0 + dir;
        Vector2Int farPos = pos0 + dir * 2;

        // 远端必须是 A（夹击者）
        if (!pincerMap.TryGetValue(farPos, out PincerAttack farPincer)) return;

        // 中间免疫检查
        bool midIsA = pincerMap.TryGetValue(midPos, out PincerAttack midPincer);
        string pattern = midIsA ? "AAA" : "ABA";

        // 对中间角色造成伤害
        if (midPos == playerGridPos)
        {
            // 玩家免疫：PincerAttack.isImmune 或护身符 PlayerImmunity
            if ((midIsA && midPincer.IsImmune) || IsPlayerPincerImmune())
            {
                Debug.Log($"[夹击] {pattern} 形成但玩家免疫夹击");
                return;
            }
            // 玩家是受害者 — 可重复被夹击
            ApplyDamageToPlayer(pattern);
        }
        else if (enemyMap.TryGetValue(midPos, out EnemyController midEnemy))
        {
            // 敌人是受害者 — 最多扣血一次（从 FloorState 查询）
            FloorState state = FloorMemoryManager.Instance?.GetOrCreateState(floor);
            if (state != null && state.IsEnemyPincered(midPos))
            {
                Debug.Log($"[夹击] {pattern} 形成但 {midEnemy.EnemyName} 已被夹击过，跳过");
                return;
            }

            ApplyPincerDamage(midEnemy, pattern);

            // 持久化：记录被夹击敌人的位置和剩余HP
            if (state != null && !midEnemy.IsDefeated)
            {
                state.MarkEnemyPincered(midPos, midEnemy.HP);
            }
        }
    }

    // ============================================================
    //  玩家在中间：敌人L(A) — 玩家 — 敌人R(A)
    //    两端敌人必须是A，玩家减少50%HP（可重复，可免疫）
    //    夹击者（两端A）不限制次数
    // ============================================================
    private static void CheckPlayerInMiddle(
        Vector2Int playerPos, Vector2Int dir,
        PlayerData player, PincerAttack playerPincer,
        Dictionary<Vector2Int, EnemyController> enemyMap,
        Dictionary<Vector2Int, PincerAttack> pincerMap)
    {
        Vector2Int leftPos = playerPos - dir;
        Vector2Int rightPos = playerPos + dir;

        if (!enemyMap.TryGetValue(leftPos, out EnemyController leftEnemy)) return;
        if (!enemyMap.TryGetValue(rightPos, out EnemyController rightEnemy)) return;

        // 两端必须是A（夹击者）
        if (!pincerMap.TryGetValue(leftPos, out PincerAttack leftPincer)) return;
        if (!pincerMap.TryGetValue(rightPos, out PincerAttack rightPincer)) return;

        bool playerIsA = playerPincer != null;
        string pattern = playerIsA ? "AAA" : "ABA";

        // 玩家免疫：PincerAttack.isImmune 或护身符 PlayerImmunity
        if ((playerIsA && playerPincer.IsImmune) || IsPlayerPincerImmune())
        {
            Debug.Log($"[夹击] {pattern} 形成但玩家免疫夹击");
            return;
        }

        // 玩家是受害者 — 可重复，不持久化
        ApplyDamageToPlayer(pattern);
    }

    // ============================================================
    //  免疫检查
    // ============================================================

    /// <summary>检查玩家是否拥有护身符免疫（PlayerImmunity 组件）</summary>
    private static bool IsPlayerPincerImmune()
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        return player != null && player.GetComponent<PlayerImmunity>() != null;
    }

    // ============================================================
    //  伤害应用
    // ============================================================

    private static void ApplyDamageToPlayer(string pattern)
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null) return;

        int hpBefore = player.HP;
        int damage = Mathf.CeilToInt(hpBefore * 0.5f);
        player.SubtractHP(damage);
        Debug.Log($"[夹击] {pattern} 形成！玩家生命值减少50%（-{damage}，HP {hpBefore} → {player.HP}）");
    }

    /// <summary>
    /// 对敌人应用夹击伤害（50%当前生命值）。若HP归零则触发击败。
    /// </summary>
    private static void ApplyPincerDamage(EnemyController enemy, string pattern)
    {
        if (enemy.IsDefeated) return;

        int hpBefore = enemy.HP;
        int damage = Mathf.CeilToInt(hpBefore * 0.5f);
        enemy.SubtractHP(damage);
        Debug.Log($"[夹击] {pattern} 形成！{enemy.EnemyName} 生命值减少50%（伤害 {damage}，HP {hpBefore} → {enemy.HP}）");

        // HP 归零则击败
        if (enemy.HP <= 0 && !enemy.IsDefeated)
        {
            enemy.Defeat();
            Debug.Log($"[夹击] {enemy.EnemyName} 被夹击击败！");
        }
    }
}
