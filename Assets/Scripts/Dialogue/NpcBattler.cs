using UnityEngine;

/// <summary>
/// NPC战斗触发器 — 挂载在对话NPC上（要与该NPC战斗）。
/// 与NPC对话结束后玩家点击选项触发（由 DialogueTrigger 的 OnChoice1/OnChoice2 绑定）。
/// 战斗流程与正常战斗完全一致（BattleManager），胜利后NPC被击败并消失，
/// 记录楼层记忆，重返该楼层时不再生成。
/// </summary>
public class NpcBattler : MonoBehaviour
{
    [Header("敌人数据（与正常敌人Prefab配置方式一致，右键 Create → MagicTower → Enemy Stats）")]
    [SerializeField] private EnemyStats enemyStats;

    /// <summary>敌人数据资产（供怪物手册读取）</summary>
    public EnemyStats Stats => enemyStats;

    /// <summary>在地图网格中的坐标（由MapGenerator生成时设置，或运行时从DialogueTrigger同步）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号</summary>
    [HideInInspector] public int floorNumber;

    /// <summary>NPC被击败时触发，参数为被击败的NPC自身</summary>
    public event System.Action<NpcBattler> OnDefeated;

    /// <summary>供 UnityEvent 绑定的无参入口：触发与NPC的战斗</summary>
    public void StartBattleWithPlayer()
    {
        SyncCoordinatesFromTrigger();

        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null)
        {
            Debug.LogError("[NpcBattler] 未找到 PlayerData");
            return;
        }

        if (enemyStats == null)
        {
            Debug.LogError($"[NpcBattler] {name} 的 EnemyStats 未设置！");
            return;
        }

        if (BattleManager.Instance == null)
        {
            Debug.LogError("[NpcBattler] BattleManager 不存在");
            return;
        }

        // 确保自身带有 EnemyController，并注入对话配置的敌人数据
        EnemyController enemy = GetComponent<EnemyController>();
        if (enemy == null)
            enemy = gameObject.AddComponent<EnemyController>();
        enemy.InitWithStats(enemyStats);
        enemy.isScriptedEnemy = true; // 击败时不记录普通敌人记忆（由本脚本处理NPC记忆），掉落物仍正常生成
        enemy.floorNumber = floorNumber;
        enemy.gridPosition = gridPosition;

        Debug.Log($"[NpcBattler] 触发战斗（第 {floorNumber} 层，坐标 ({gridPosition.x}, {gridPosition.y})）");
        BattleManager.Instance.StartBattle(player, enemy, OnBattleResult);
    }

    /// <summary>战斗结果回调：胜利后NPC消失并写入楼层记忆</summary>
    private void OnBattleResult(bool won)
    {
        if (!won) return;

        // 再次同步坐标，确保使用最新正确值（防御 Awake 时序问题）
        SyncCoordinatesFromTrigger();

        FloorState state = FloorMemoryManager.Instance?.GetOrCreateState(floorNumber);
        state?.MarkNpcRemoved(gridPosition);

        // 通知订阅者（如战斗门）
        OnDefeated?.Invoke(this);

        Debug.Log($"[NpcBattler] 战斗胜利，NPC消失（第 {floorNumber} 层，坐标 ({gridPosition.x}, {gridPosition.y})）");
        Destroy(gameObject);
    }

    /// <summary>
    /// 从同一物体（或父物体）上的 DialogueTrigger 同步坐标。
    /// 这是最可靠的坐标来源，因为 MapGenerator 一定会在生成时设置 DialogueTrigger。
    /// </summary>
    private void SyncCoordinatesFromTrigger()
    {
        DialogueTrigger trigger = GetComponentInParent<DialogueTrigger>();
        if (trigger != null)
        {
            gridPosition = trigger.gridPosition;
            floorNumber = trigger.floorNumber;
        }
        else
        {
            Debug.LogWarning($"[NpcBattler] 未找到 DialogueTrigger，沿用当前坐标（第 {floorNumber} 层，({gridPosition.x},{gridPosition.y})）");
        }
    }
}
