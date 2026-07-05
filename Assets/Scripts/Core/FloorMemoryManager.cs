using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 楼层记忆管理器 — 单例，跨楼层保留已清除的敌人/物品状态。
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
        }
        else
        {
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
}
