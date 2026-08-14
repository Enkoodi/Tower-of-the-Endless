using UnityEngine;

/// <summary>
/// NPC移除触发器 — 挂载在一次性对话NPC上（逻辑只能执行一次的NPC）。
/// 与NPC对话结束后玩家点击选项触发（由 DialogueTrigger 的 OnChoice1/OnChoice2 绑定）。
/// 移除自身并写入楼层记忆，重返该楼层时该NPC不再生成。
/// </summary>
public class NpcRemover : MonoBehaviour
{
    /// <summary>在地图网格中的坐标（由MapGenerator生成时设置，或运行时从DialogueTrigger同步）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号</summary>
    [HideInInspector] public int floorNumber;

    /// <summary>供 UnityEvent 绑定的无参入口：记录记忆并销毁自身</summary>
    public void RemoveSelf()
    {
        SyncCoordinatesFromTrigger();

        FloorState state = FloorMemoryManager.Instance?.GetOrCreateState(floorNumber);
        state?.MarkNpcRemoved(gridPosition);

        Debug.Log($"[NpcRemover] 已移除NPC（第 {floorNumber} 层，坐标 ({gridPosition.x}, {gridPosition.y})）");
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
            Debug.LogWarning($"[NpcRemover] 未找到 DialogueTrigger，沿用当前坐标（第 {floorNumber} 层，({gridPosition.x},{gridPosition.y})）");
        }
    }
}
