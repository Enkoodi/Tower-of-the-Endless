using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Newtonsoft.Json;

/// <summary>
/// 地图生成器 — 解析 floor_XX.json 并在场景中实例化对应的 Prefab。
/// 支持按楼层编号或直接传入 TextAsset 加载，加载完成后触发 onFloorLoaded 事件。
/// </summary>
public class MapGenerator : MonoBehaviour
{
    // ========================================================================
    //  Inspector 字段
    // ========================================================================

    [Header("地图挂载点")]
    [SerializeField] private Transform mapContainer;

    [Header("Prefab 映射表（ID → Prefab）")]
    [SerializeField] private PrefabEntry[] terrainPrefabs;
    [SerializeField] private PrefabEntry[] objectPrefabs;
    [SerializeField] private PrefabEntry[] enemyPrefabs;
    [SerializeField] private PrefabEntry[] itemPrefabs;

    [Header("玩家")]
    [SerializeField] private Transform player;

    [Header("相机自动适配")]
    [SerializeField] private bool autoFitCamera = true;
    [SerializeField] private float marginCells = 3f;

    [Header("测试（拖入 JSON 文件直接加载）")]
    [SerializeField] private TextAsset testMap;

    [Header("事件")]
    [SerializeField] private UnityEvent<MapData> onFloorLoaded;

    // ========================================================================
    //  运行时数据
    // ========================================================================

    /// <summary>当前已加载的地图数据（只读）</summary>
    public MapData CurrentMap { get; private set; }

    /// <summary>当前楼层编号（-1 表示未加载）</summary>
    public int CurrentFloor => CurrentMap?.floor ?? -1;

    // ID → PrefabEntry 运行时查找表（由数组构建）
    private Dictionary<int, PrefabEntry> terrainMap;
    private Dictionary<int, PrefabEntry> objectMap;
    private Dictionary<int, PrefabEntry> enemyMap;
    private Dictionary<int, PrefabEntry> itemMap;

    // ========================================================================
    //  Unity 生命周期
    // ========================================================================

    private void Awake()
    {
        BuildLookupTables();
    }

    private void Start()
    {
        if (testMap != null)
        {
            LoadFloor(testMap);
        }
    }

    // ========================================================================
    //  公开接口
    // ========================================================================

    /// <summary>从 TextAsset（JSON）加载地图</summary>
    public void LoadFloor(TextAsset jsonFile)
    {
        if (jsonFile == null)
        {
            Debug.LogError("[MapGenerator] 地图 JSON 为空！");
            return;
        }

        if (mapContainer == null)
        {
            Debug.LogError("[MapGenerator] MapContainer 未设置！");
            return;
        }

        MapData data = ParseJson(jsonFile);
        if (data == null) return;

        if (!ValidateMapData(data)) return;

        ApplyMapData(data);
    }

    /// <summary>
    /// 按楼层编号加载（从 Resources/ 查找 floor_{编号:D2}.json）
    /// </summary>
    public void LoadFloor(int floorNumber)
    {
        string path = $"floor_{floorNumber:D2}";
        TextAsset jsonFile = Resources.Load<TextAsset>(path);

        if (jsonFile != null)
        {
            LoadFloor(jsonFile);
        }
        else
        {
            Debug.LogError($"[MapGenerator] 找不到地图文件：{path}.json（请确认文件放在 Assets/Resources/ 下）");
        }
    }

    // ========================================================================
    //  内部流程
    // ========================================================================

    /// <summary>解析 JSON → MapData</summary>
    private MapData ParseJson(TextAsset jsonFile)
    {
        try
        {
            return JsonConvert.DeserializeObject<MapData>(jsonFile.text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MapGenerator] JSON 解析失败：{e.Message}\n文件：{jsonFile.name}");
            return null;
        }
    }

    /// <summary>验证数据完整性</summary>
    private bool ValidateMapData(MapData data)
    {
        if (data == null)
        {
            Debug.LogError("[MapGenerator] MapData 为 null");
            return false;
        }

        if (data.width <= 0 || data.height <= 0)
        {
            Debug.LogError($"[MapGenerator] 地图尺寸无效（{data.width}×{data.height}），必须大于 0");
            return false;
        }

        if (data.terrain == null || data.terrain.Count != data.height)
        {
            Debug.LogError($"[MapGenerator] 地形层行数({data.terrain?.Count})与 height({data.height})不匹配");
            return false;
        }

        for (int i = 0; i < data.height; i++)
        {
            if (data.terrain[i] == null || data.terrain[i].Count != data.width)
            {
                Debug.LogError($"[MapGenerator] 地形层第 {i} 行列数({data.terrain[i]?.Count})与 width({data.width})不匹配");
                return false;
            }
        }

        return true;
    }

    /// <summary>将解析完的数据应用到场景</summary>
    private void ApplyMapData(MapData data)
    {
        ClearMap();
        CurrentMap = data;

        float offsetX = -(data.width  - 1) / 2f;
        float offsetY =  (data.height - 1) / 2f;

        // 逐格生成四层内容
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                Vector3 cellPos = new Vector3(x + offsetX, -y + offsetY, 0f);

                SpawnTerrain(GetCell(data.terrain, x, y), cellPos);
                SpawnObject( GetCell(data.objects,  x, y), cellPos);
                SpawnEnemy(  GetCell(data.enemies,  x, y), cellPos);
                SpawnItem(   GetCell(data.items,    x, y), cellPos);
            }
        }

        // 自动定位下楼梯作为出生点（第一层没有下楼梯，使用 JSON 中的 player_spawn）
        AutoSetSpawnFromDownStairs(data);

        // 设置玩家出生点
        SetPlayerPosition(data.player_spawn, offsetX, offsetY);

        // 自动适配相机
        if (autoFitCamera)
        {
            SetupCamera(data.height, data.width);
        }

        Debug.Log($"[MapGenerator] 地图加载完成：{data.name}（{data.width}×{data.height}，楼层 {data.floor}）");
        onFloorLoaded?.Invoke(data);

        // 特殊祝福生命周期：进入楼层
        BlessingManager.Instance?.OnEnterFloor(
            FindAnyObjectByType<PlayerData>(),
            data.floor,
            FindAnyObjectByType<BattleUI>()
        );
    }

    // ========================================================================
    //  出生点 / 相机
    // ========================================================================

    private void SetPlayerPosition(PlayerSpawnPos spawn, float offsetX, float offsetY)
    {
        if (spawn == null)
        {
            Debug.LogWarning("[MapGenerator] 地图未定义 player_spawn，玩家位置保持不变");
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("[MapGenerator] 玩家未设置（player 字段为空），跳过出生点定位");
            return;
        }

        float x = spawn.x + offsetX;
        float y = -spawn.y + offsetY;
        player.position = new Vector3(x, y, 0f);
        Debug.Log($"[MapGenerator] 玩家出生点：({spawn.x}, {spawn.y}) → ({x:F1}, {y:F1})");
    }

    /// <summary>
    /// 扫描 objects 层，找到下楼梯（ID=9）的位置并设为出生点。
    /// 第一层没有下楼梯时，保留 JSON 中的 player_spawn。
    /// </summary>
    private void AutoSetSpawnFromDownStairs(MapData data)
    {
        if (data.objects == null) return;

        for (int y = 0; y < data.objects.Count; y++)
        {
            var row = data.objects[y];
            if (row == null) continue;

            for (int x = 0; x < row.Count; x++)
            {
                if (row[x] == 9) // 9 = 下楼梯
                {
                    data.player_spawn = new PlayerSpawnPos { x = x, y = y };
                    Debug.Log($"[MapGenerator] 自动定位下楼梯出生点：({x}, {y})");
                    return;
                }
            }
        }

        // 未找到下楼梯（如第一层），使用 JSON 中原有的 player_spawn
        Debug.Log($"[MapGenerator] 未找到下楼梯，使用 JSON 出生点：({data.player_spawn?.x}, {data.player_spawn?.y})");
    }

    private void SetupCamera(int mapRows, int mapCols)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[MapGenerator] 未找到 Main Camera，跳过相机适配");
            return;
        }

        // 以地图高度为准
        cam.orthographicSize = mapRows / 2f;

        // 若宽度不足，以宽度为准（左右预留边距）
        float camWidth      = cam.aspect * cam.orthographicSize * 2f;
        float requiredWidth = mapCols + 2f * marginCells;

        if (camWidth < requiredWidth)
        {
            cam.orthographicSize = requiredWidth / (cam.aspect * 2f);
        }

        // 居中
        Vector3 pos = cam.transform.position;
        cam.transform.position = new Vector3(0f, 0f, pos.z);

        Debug.Log($"[MapGenerator] 相机适配：size={cam.orthographicSize:F2}，位置=(0, 0, {pos.z})");
    }

    // ========================================================================
    //  生成四层内容（查字典 → 实例化）
    // ========================================================================

    private void SpawnTerrain(int id, Vector3 pos)
    {
        // 地形层：0 = 空地（不生成），1+ 查表
        if (id == 0) return;
        SpawnFromMap(terrainMap, id, pos);
    }

    private void SpawnObject(int id, Vector3 pos)
    {
        if (id == 0) return;
        SpawnFromMap(objectMap, id, pos);
    }

    private void SpawnEnemy(int id, Vector3 pos)
    {
        if (id == 0) return;
        SpawnFromMap(enemyMap, id, pos);
    }

    private void SpawnItem(int id, Vector3 pos)
    {
        if (id == 0) return;
        SpawnFromMap(itemMap, id, pos);
    }

    /// <summary>从查找表中取出对应的 PrefabEntry 并实例化</summary>
    private void SpawnFromMap(Dictionary<int, PrefabEntry> map, int id, Vector3 pos)
    {
        if (map == null)
        {
            Debug.LogWarning($"[MapGenerator] 查找表未初始化，跳过 ID={id}");
            return;
        }

        if (map.TryGetValue(id, out PrefabEntry entry))
        {
            InstantiatePrefab(entry.prefab, pos, entry.displayName);
        }
        else
        {
            Debug.LogWarning($"[MapGenerator] 未注册的 ID={id}，请在 Inspector 中补充对应 PrefabEntry");
        }
    }

    /// <summary>实际的 Instantiate 操作</summary>
    private void InstantiatePrefab(GameObject prefab, Vector3 pos, string displayName)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[MapGenerator] Prefab 为空：{displayName}");
            return;
        }

        GameObject obj = Instantiate(prefab, pos, Quaternion.identity, mapContainer);
        if (!string.IsNullOrEmpty(displayName))
        {
            obj.name = displayName;
        }
    }

    // ========================================================================
    //  工具方法
    // ========================================================================

    /// <summary>安全获取二维列表中的值，越界返回 0</summary>
    private static int GetCell(List<List<int>> layer, int x, int y)
    {
        if (layer == null) return 0;
        if (y < 0 || y >= layer.Count) return 0;

        List<int> row = layer[y];
        if (row == null) return 0;
        if (x < 0 || x >= row.Count) return 0;

        return row[x];
    }

    /// <summary>将 Inspector 配置的 PrefabEntry[] 数组构建为运行时字典</summary>
    private void BuildLookupTables()
    {
        terrainMap = BuildTable(terrainPrefabs);
        objectMap  = BuildTable(objectPrefabs);
        enemyMap   = BuildTable(enemyPrefabs);
        itemMap    = BuildTable(itemPrefabs);
    }

    private static Dictionary<int, PrefabEntry> BuildTable(PrefabEntry[] entries)
    {
        var dict = new Dictionary<int, PrefabEntry>();
        if (entries == null) return dict;

        foreach (PrefabEntry entry in entries)
        {
            if (entry == null) continue;
            if (dict.ContainsKey(entry.id))
            {
                Debug.LogWarning($"[MapGenerator] 重复的 ID={entry.id}，后面的条目将被忽略");
                continue;
            }
            dict[entry.id] = entry;
        }

        return dict;
    }

    /// <summary>销毁挂载点下的所有子物体</summary>
    private void ClearMap()
    {
        if (mapContainer == null) return;

        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapContainer.GetChild(i).gameObject);
        }

        Debug.Log("[MapGenerator] 地图已清空");
    }
}
