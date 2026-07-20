using UnityEngine;

/// <summary>
/// 根据楼层自动切换背景。
/// 每个映射项直接拖入对应的背景 GameObject，切换时只激活匹配的那一个，其余隐藏。
/// 通过 MapGenerator.onFloorLoaded 事件自动响应楼层切换。
/// </summary>
public class BackgroundSwitcher : MonoBehaviour
{
    [Header("地图生成器（自动订阅楼层加载事件）")]
    [SerializeField] private MapGenerator mapGenerator;

    [Header("楼层 → 背景映射（直接拖入背景节点）")]
    [SerializeField] private FloorBgMapping[] mappings;

    private int activeIndex = -1;

    private void Start()
    {
        // 初始全部隐藏
        for (int i = 0; i < mappings.Length; i++)
        {
            if (mappings[i].background != null)
                mappings[i].background.SetActive(false);
        }

        // 订阅楼层加载事件
        if (mapGenerator != null)
        {
            mapGenerator.onFloorLoaded.AddListener(OnFloorLoaded);

            if (mapGenerator.CurrentFloor != -1)
                SwitchBackground(mapGenerator.CurrentFloor);
        }
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
            mapGenerator.onFloorLoaded.RemoveListener(OnFloorLoaded);
    }

    private void OnFloorLoaded(MapData data)
    {
        SwitchBackground(data.floor);
    }

    /// <summary>根据楼层编号切换背景（支持负数楼层）</summary>
    public void SwitchBackground(int floor)
    {
        int index = GetMappedIndex(floor);
        if (index < 0 || index >= mappings.Length)
        {
            Debug.Log($"[BackgroundSwitcher] 楼层 {floor} 无匹配背景");
            return;
        }

        if (index == activeIndex) return;

        if (activeIndex >= 0 && activeIndex < mappings.Length && mappings[activeIndex].background != null)
            mappings[activeIndex].background.SetActive(false);

        if (mappings[index].background != null)
        {
            mappings[index].background.SetActive(true);
            Debug.Log($"[BackgroundSwitcher] 楼层 {floor} → {mappings[index].background.name}");
        }

        activeIndex = index;
    }

    private int GetMappedIndex(int floor)
    {
        for (int i = 0; i < mappings.Length; i++)
        {
            if (floor >= mappings[i].floorMin && floor <= mappings[i].floorMax)
                return i;
        }
        return -1;
    }
}

/// <summary>楼层范围 → 背景节点 的映射（支持负数楼层）</summary>
[System.Serializable]
public class FloorBgMapping
{
    [Tooltip("起始楼层（含），支持负数")]
    public int floorMin;

    [Tooltip("结束楼层（含），支持负数")]
    public int floorMax;

    [Tooltip("对应背景节点（直接拖入）")]
    public GameObject background;
}
