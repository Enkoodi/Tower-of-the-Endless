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

    /// <summary>本次加载的进入方向（决定出生在哪个楼梯）</summary>
    private EntryDirection entryDirection;

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

    /// <summary>从 TextAsset（JSON）加载地图（默认出生点，用于首次加载）</summary>
    public void LoadFloor(TextAsset jsonFile)
    {
        LoadFloorInternal(jsonFile, EntryDirection.Default);
    }

    /// <summary>
    /// 按楼层编号加载（从 Resources/ 查找 floor_{编号:D2}.json），默认出生点。
    /// </summary>
    public void LoadFloor(int floorNumber)
    {
        LoadFloor(floorNumber, EntryDirection.Default);
    }

    /// <summary>
    /// 按楼层编号加载，并指定进入方向。
    /// FromBelow = 从下层上楼梯进入 → 出生在下楼梯(9)
    /// FromAbove = 从上层下楼梯进入 → 出生在上楼梯(8)
    /// </summary>
    public void LoadFloor(int floorNumber, EntryDirection entryDir)
    {
        string path = $"floor_{floorNumber:D2}";
        TextAsset jsonFile = Resources.Load<TextAsset>(path);

        if (jsonFile != null)
        {
            LoadFloorInternal(jsonFile, entryDir);
        }
        else
        {
            Debug.LogError($"[MapGenerator] 找不到地图文件：{path}.json（请确认文件放在 Assets/Resources/ 下）");
        }
    }

    /// <summary>内部统一加载入口</summary>
    private void LoadFloorInternal(TextAsset jsonFile, EntryDirection entryDir)
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

        entryDirection = entryDir;
        ApplyMapData(data);
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
        int floor = data.floor;
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                Vector3 cellPos = new Vector3(x + offsetX, -y + offsetY, 0f);
                Vector2Int gridPos = new Vector2Int(x, y);

                SpawnTerrain(GetCell(data.terrain, x, y), cellPos);
                SpawnObject( GetCell(data.objects,  x, y), cellPos, gridPos, floor);
                SpawnEnemy(  GetCell(data.enemies,  x, y), cellPos, gridPos, floor);
                SpawnItem(   GetCell(data.items,    x, y), cellPos, gridPos, floor);
            }
        }

        // 根据进入方向自动定位对应的楼梯作为出生点
        AutoSetSpawnFromStairs(data);

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
    /// 根据进入方向自动定位楼梯作为出生点：
    /// FromBelow（踩上楼梯上来）→ 找下楼梯(9)
    /// FromAbove（踩下楼梯下来）→ 找上楼梯(8)
    /// Default（首次加载）→ 使用 JSON 中的 player_spawn
    /// </summary>
    private void AutoSetSpawnFromStairs(MapData data)
    {
        if (data.objects == null) return;

        int targetId = entryDirection switch
        {
            EntryDirection.FromBelow => 9, // 从下层上来 → 出生在下楼梯
            EntryDirection.FromAbove => 8, // 从上层下来 → 出生在上楼梯
            _ => -1                       // 默认使用 JSON 出生点
        };

        if (targetId < 0)
        {
            Debug.Log($"[MapGenerator] 默认出生点：({data.player_spawn?.x}, {data.player_spawn?.y})");
            return;
        }

        for (int y = 0; y < data.objects.Count; y++)
        {
            var row = data.objects[y];
            if (row == null) continue;

            for (int x = 0; x < row.Count; x++)
            {
                if (row[x] == targetId)
                {
                    data.player_spawn = new PlayerSpawnPos { x = x, y = y };
                    string stairName = targetId == 9 ? "下楼梯" : "上楼梯";
                    Debug.Log($"[MapGenerator] 从{(entryDirection == EntryDirection.FromBelow ? "下层" : "上层")}进入 → 出生在{stairName}：({x}, {y})");
                    return;
                }
            }
        }

        // 目标楼梯不存在时回退到 JSON 出生点
        Debug.LogWarning($"[MapGenerator] 未找到目标楼梯(ID={targetId})，回退到 JSON 出生点：({data.player_spawn?.x}, {data.player_spawn?.y})");
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

    private void SpawnObject(int id, Vector3 worldPos, Vector2Int gridPos, int floor)
    {
        if (id == 0) return;

        // 查找 PrefabEntry 以便检查是否为门
        if (objectMap == null || !objectMap.TryGetValue(id, out PrefabEntry entry))
        {
            Debug.LogWarning($"[MapGenerator] 未注册的对象 ID={id}");
            return;
        }

        // 如果是门且已在楼层记忆中被开启，跳过生成
        if (entry.prefab != null && entry.prefab.GetComponent<DoorController>() != null)
        {
            FloorState state = FloorMemoryManager.Instance?.GetState(floor);
            if (state != null && state.IsDoorOpened(gridPos))
                return;
        }

        // 如果是战斗门且已被开启，跳过生成
        if (entry.prefab != null && entry.prefab.GetComponent<BattleDoorController>() != null)
        {
            FloorState state = FloorMemoryManager.Instance?.GetState(floor);
            if (state != null && state.IsBattleDoorOpened(gridPos))
                return;
        }

        GameObject obj = Instantiate(entry.prefab, worldPos, Quaternion.identity, mapContainer);
        obj.name = entry.displayName;

        // 如果是门，设置网格坐标和楼层编号
        DoorController door = obj.GetComponent<DoorController>();
        if (door != null)
        {
            door.gridPosition = gridPos;
            door.floorNumber = floor;
        }

        // 如果是战斗门，设置网格坐标和楼层编号
        BattleDoorController battleDoor = obj.GetComponent<BattleDoorController>();
        if (battleDoor != null)
        {
            battleDoor.gridPosition = gridPos;
            battleDoor.floorNumber = floor;
        }

        // 如果是战斗触发器，设置楼层编号
        BattleTrigger battleTrigger = obj.GetComponent<BattleTrigger>();
        if (battleTrigger != null)
        {
            battleTrigger.floorNumber = floor;
        }
    }

    private void SpawnEnemy(int id, Vector3 worldPos, Vector2Int gridPos, int floor)
    {
        if (id == 0) return;

        FloorState state = FloorMemoryManager.Instance?.GetState(floor);
        if (state != null && state.IsEnemyDefeated(gridPos))
            return;

        if (enemyMap == null || !enemyMap.TryGetValue(id, out PrefabEntry entry))
        {
            Debug.LogWarning($"[MapGenerator] 未注册的敌人 ID={id}");
            return;
        }

        GameObject obj = Instantiate(entry.prefab, worldPos, Quaternion.identity, mapContainer);
        obj.name = entry.displayName;

        EnemyController ec = obj.GetComponent<EnemyController>();
        if (ec != null)
        {
            ec.gridPosition = gridPos;
            ec.floorNumber = floor;
        }
    }

    private void SpawnItem(int id, Vector3 worldPos, Vector2Int gridPos, int floor)
    {
        if (id == 0) return;

        FloorState state = FloorMemoryManager.Instance?.GetState(floor);
        if (state != null && state.IsItemPickedUp(gridPos))
            return;

        if (itemMap == null || !itemMap.TryGetValue(id, out PrefabEntry entry))
        {
            Debug.LogWarning($"[MapGenerator] 未注册的道具 ID={id}");
            return;
        }

        GameObject obj = Instantiate(entry.prefab, worldPos, Quaternion.identity, mapContainer);
        obj.name = entry.displayName;

        KeyPickup kp = obj.GetComponent<KeyPickup>();
        if (kp != null) { kp.gridPosition = gridPos; kp.floorNumber = floor; }

        StatBoostPickup sb = obj.GetComponent<StatBoostPickup>();
        if (sb != null) { sb.gridPosition = gridPos; sb.floorNumber = floor; }

        BlessingPickup bp = obj.GetComponent<BlessingPickup>();
        if (bp != null) { bp.gridPosition = gridPos; bp.floorNumber = floor; }
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

    // ========================================================================
    //  战斗门动态生成（由 BattleTrigger 调用）
    // ========================================================================

    /// <summary>
    /// 在指定网格坐标动态生成一扇战斗门。
    /// 由 BattleTrigger 在玩家触发时调用。
    /// 会自动检查 FloorMemory 跳过已开启的门。
    /// </summary>
    public void SpawnBattleDoor(GameObject prefab, Vector2Int gridPos, int floor)
    {
        if (prefab == null)
        {
            Debug.LogError("[MapGenerator] SpawnBattleDoor: prefab 为空");
            return;
        }

        // 检查是否已开启
        FloorState state = FloorMemoryManager.Instance?.GetState(floor);
        if (state != null && state.IsBattleDoorOpened(gridPos))
        {
            Debug.Log($"[MapGenerator] 战斗门 ({gridPos.x},{gridPos.y}) 已开启，跳过生成");
            return;
        }

        if (CurrentMap == null)
        {
            Debug.LogError("[MapGenerator] SpawnBattleDoor: 当前无地图数据");
            return;
        }

        float offsetX = -(CurrentMap.width - 1) / 2f;
        float offsetY = (CurrentMap.height - 1) / 2f;
        Vector3 worldPos = new Vector3(gridPos.x + offsetX, -gridPos.y + offsetY, 0f);

        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, mapContainer);
        obj.name = $"{prefab.name}_({gridPos.x},{gridPos.y})";

        BattleDoorController door = obj.GetComponent<BattleDoorController>();
        if (door != null)
        {
            door.gridPosition = gridPos;
            door.floorNumber = floor;
            door.Initialize();
        }
        else
        {
            Debug.LogWarning($"[MapGenerator] SpawnBattleDoor: Prefab {prefab.name} 上未找到 BattleDoorController");
        }
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

/// <summary>
/// 楼层进入方向 — 决定玩家在新楼层的出生位置
/// </summary>
public enum EntryDirection
{
    Default,    // 首次加载，使用 JSON 中的 player_spawn
    FromBelow,  // 从下层通过上楼梯进入 → 出生在目标层的下楼梯(9)
    FromAbove   // 从上层通过下楼梯进入 → 出生在目标层的上楼梯(8)
}
