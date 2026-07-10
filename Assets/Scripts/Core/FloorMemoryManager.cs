using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 楼层记忆管理器 — 单例，跨楼层保留已清除的敌人/物品/掉落物状态。
/// 玩家离开后重返同一楼层时，已击败的敌人和已拾取的物品不会重新生成。
/// </summary>
public class FloorMemoryManager : MonoBehaviour
{
    public static FloorMemoryManager Instance { get; private set; }

    private Dictionary<int, FloorState> floorStates = new Dictionary<int, FloorState>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[FloorMemory] 单例已初始化，挂载在 GameObject '{gameObject.name}' 上");
        }
        else
        {
            Debug.LogWarning($"[FloorMemory] 检测到重复实例，销毁 GameObject '{gameObject.name}'。已有实例在 '{Instance.gameObject.name}'");
            Destroy(gameObject);
        }
    }

    /// <summary>获取或创建指定楼层的状态</summary>
    public FloorState GetOrCreateState(int floor)
    {
        if (!floorStates.TryGetValue(floor, out FloorState state))
        {
            state = new FloorState(floor);
            floorStates[floor] = state;
        }
        return state;
    }

    /// <summary>获取楼层状态（无则返回 null）</summary>
    public FloorState GetState(int floor)
    {
        floorStates.TryGetValue(floor, out FloorState state);
        return state;
    }

    /// <summary>清除所有楼层记忆（新游戏时调用）</summary>
    public void ResetAll()
    {
        floorStates.Clear();
        Debug.Log("[FloorMemory] 所有楼层记忆已清除");
    }

    /// <summary>清除指定楼层记忆</summary>
    public void ResetFloor(int floor)
    {
        if (floorStates.TryGetValue(floor, out FloorState state))
        {
            state.Reset();
            Debug.Log($"[FloorMemory] 第 {floor} 层记忆已清除");
        }
    }

    // ============================================================
    //  存档序列化接口
    // ============================================================

    /// <summary>导出所有楼层状态为可序列化的条目列表</summary>
    public List<FloorStateEntry> GetAllFloorEntries()
    {
        var entries = new List<FloorStateEntry>();
        foreach (var kv in floorStates)
        {
            FloorState state = kv.Value;
            var entry = new FloorStateEntry { floorNumber = state.floorNumber };

            foreach (var pos in state.defeatedEnemies)
                entry.defeatedEnemies.Add(FloorStateEntry.PosToString(pos));
            foreach (var pos in state.pickedUpItems)
                entry.pickedUpItems.Add(FloorStateEntry.PosToString(pos));
            foreach (var pos in state.openedDoors)
                entry.openedDoors.Add(FloorStateEntry.PosToString(pos));
            foreach (var pos in state.openedBattleDoors)
                entry.openedBattleDoors.Add(FloorStateEntry.PosToString(pos));
            foreach (var pos in state.activeDropItems)
                entry.activeDropItems.Add(FloorStateEntry.PosToString(pos));

            entries.Add(entry);
        }
        return entries;
    }

    /// <summary>从序列化条目恢复楼层状态（覆盖现有数据）</summary>
    public void RestoreFromEntries(List<FloorStateEntry> entries)
    {
        floorStates.Clear();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            FloorState state = new FloorState(entry.floorNumber);
            foreach (var s in entry.defeatedEnemies)
                state.defeatedEnemies.Add(FloorStateEntry.StringToPos(s));
            foreach (var s in entry.pickedUpItems)
                state.pickedUpItems.Add(FloorStateEntry.StringToPos(s));
            foreach (var s in entry.openedDoors)
                state.openedDoors.Add(FloorStateEntry.StringToPos(s));
            foreach (var s in entry.openedBattleDoors)
                state.openedBattleDoors.Add(FloorStateEntry.StringToPos(s));
            foreach (var s in entry.activeDropItems)
                state.activeDropItems.Add(FloorStateEntry.StringToPos(s));

            floorStates[entry.floorNumber] = state;
        }
        Debug.Log($"[FloorMemory] 从存档恢复了 {floorStates.Count} 个楼层的状态");
    }
}
