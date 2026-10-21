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
    [SerializeField] private PrefabEntry[] npcPrefabs;

    [Header("玩家")]
    [SerializeField] private Transform player;

    [Header("相机自动适配")]
    [SerializeField] private bool autoFitCamera = true;
    [SerializeField] private float marginCells = 3f;

    [Header("测试（拖入 JSON 文件直接加载）")]
    [SerializeField] private TextAsset testMap;

    [Header("事件")]
    [SerializeField] public UnityEvent<MapData> onFloorLoaded;

    // ========================================================================
    //  运行时数据
    // ========================================================================

    /// <summary>当前已加载的地图数据（只读）</summary>
    public MapData CurrentMap { get; private set; }

    /// <summary>当前楼层编号（GameSaveData.NoFloor 表示未加载；负数是合法楼层，别拿 -1 判断）</summary>
    public int CurrentFloor => CurrentMap?.floor ?? GameSaveData.NoFloor;

    /// <summary>
    /// 世界坐标是否落在冰面上。
    /// 冰面砖块没有碰撞体（与墙共用一层，物理查询区分不出来），只能按格坐标查。
    /// </summary>
    public bool IsIceAt(Vector3 worldPos)
    {
        if (CurrentMap == null) return false;

        int gx = Mathf.RoundToInt(worldPos.x - offsetX);
        int gy = Mathf.RoundToInt(offsetY - worldPos.y);   // y 轴是翻转的
        return iceCells.Contains(new Vector2Int(gx, gy));
    }

    // ID → PrefabEntry 运行时查找表（由数组构建）
    private Dictionary<int, PrefabEntry> terrainMap;
    private Dictionary<int, PrefabEntry> objectMap;
    private Dictionary<int, PrefabEntry> enemyMap;
    private Dictionary<int, PrefabEntry> itemMap;
    private Dictionary<int, PrefabEntry> npcMap;

    /// <summary>当前楼层已生成的地形物体（网格坐标 → GameObject），用于移除墙等操作</summary>
    private Dictionary<Vector2Int, GameObject> terrainObjects = new Dictionary<Vector2Int, GameObject>();

    /// <summary>本次加载的进入方向（决定出生在哪个楼梯）</summary>
    private EntryDirection entryDirection;

    /// <summary>当前楼层所有冰面格（地形预制体上挂 IceTile 标记的格子）</summary>
    private readonly HashSet<Vector2Int> iceCells = new HashSet<Vector2Int>();

    /// <summary>挡路的地形 id（由 terrainPrefabs 的碰撞体启用状态推出，见 BuildBlockedTerrainIds）</summary>
    private readonly HashSet<int> blockedTerrainIds = new HashSet<int>();

    /// <summary>当前楼层的地图原点偏移 —— 世界坐标与网格坐标互转用，必须存字段</summary>
    private float offsetX;
    private float offsetY;

    // ========================================================================
    //  Unity 生命周期
    // ========================================================================

    private void Awake()
    {
        BuildLookupTables();
    }

    private void Start()
    {
        // 本局模式：走过开场菜单就已经定了；直接在 Game 场景 Play 时从存档推断
        GameSession.EnsureMode();

        // 挑战模式：开局继承全局存档里「上一次阵亡」的记录（必须在读档之前，读档会用存档里的覆盖）
        AnthroposBlessing.BeginRun();

        // 挑战模式：按移涌之钥数量定格本局的敌人倍率（必须在生成楼层之前）
        EnemyScaling.BeginRun();

        // 挑战模式：清空额外祝福的发放进度（读档时会被存档那份覆盖）
        ChallengeBonus.BeginRun();

        // 优先读取自动存档（继续游戏）；无自动存档时加载测试/初始楼层
        bool loadAuto = SaveManager.LoadAutoSaveOnStart;
        SaveManager.LoadAutoSaveOnStart = true; // 消费标记并复位

        if (loadAuto && SaveManager.Instance != null && SaveManager.Instance.LoadAutoGame())
        {
            return;
        }

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

    /// <summary>取 itemPrefabs 里的 prefab（定点生成的地图道具用）。</summary>
    public GameObject GetItemPrefab(int id)
    {
        return itemMap != null && itemMap.TryGetValue(id, out PrefabEntry entry) ? entry.prefab : null;
    }

    /// <summary>
    /// 在指定格生成一个拾取物 —— 与地图道具走同一条初始化路径，
    /// 楼层与网格坐标统一由 DropManager.SetPickupInfo 写。
    /// </summary>
    public GameObject SpawnPickupAt(GameObject prefab, Vector2Int gridPos, int floor)
    {
        if (prefab == null || mapContainer == null)
        {
            Debug.LogWarning("[MapGenerator] SpawnPickupAt：prefab 或 mapContainer 为空");
            return null;
        }

        GameObject obj = Instantiate(prefab, GridToWorld(gridPos), Quaternion.identity, mapContainer);
        DropManager.SetPickupInfo(obj, gridPos, floor);
        return obj;
    }

    /// <summary>世界坐标 → 网格坐标（与生成时的换算互逆，y 轴翻转）。</summary>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x - offsetX),
            Mathf.RoundToInt(offsetY - worldPos.y));
    }

    /// <summary>网格坐标 → 世界坐标（与 ApplyMapData 里的 cellPos 同一公式）。</summary>
    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x + offsetX, -gridPos.y + offsetY, 0f);
    }

    // ========================================================================
    //  静态地图查询（只读 map 数据，不看运行时的门/敌人状态）
    //  口径 = 「新一局」这一格的样子：新局 FloorMemory 清空，门全关、怪与道具全在。
    // ========================================================================

    /// <summary>这一格的地形能不能站人。</summary>
    public bool IsCellWalkable(Vector2Int pos)
    {
        if (!InBounds(pos)) return false;

        int terrain = GetCell(CurrentMap.terrain, pos.x, pos.y);
        return terrain == 0 || !blockedTerrainIds.Contains(terrain);
    }

    /// <summary>完全空：能站人，且物体/敌人/道具/NPC 四层都没有东西。</summary>
    public bool IsCellEmptyForSpawn(Vector2Int pos)
    {
        if (!IsCellWalkable(pos)) return false;

        return GetCell(CurrentMap.objects, pos.x, pos.y) == 0
            && GetCell(CurrentMap.enemies, pos.x, pos.y) == 0
            && GetCell(CurrentMap.items, pos.x, pos.y) == 0
            && GetCell(CurrentMap.npcs, pos.x, pos.y) == 0;
    }

    /// <summary>
    /// 可站：能站人，且没有门/楼梯/敌人/NPC（道具与战斗触发器允许同格）。
    /// 用在「完全空」找不到时的退让档。
    /// </summary>
    public bool IsCellFreeForSpawn(Vector2Int pos)
    {
        if (!IsCellWalkable(pos)) return false;

        return !IsBlockingObjectId(GetCell(CurrentMap.objects, pos.x, pos.y))
            && GetCell(CurrentMap.enemies, pos.x, pos.y) == 0
            && GetCell(CurrentMap.npcs, pos.x, pos.y) == 0;
    }

    /// <summary>本层所有楼梯格（上楼梯 8 / 下楼梯 9）。</summary>
    public List<Vector2Int> GetStairCells()
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (CurrentMap?.objects == null) return result;

        for (int y = 0; y < CurrentMap.objects.Count; y++)
        {
            List<int> row = CurrentMap.objects[y];
            if (row == null) continue;

            for (int x = 0; x < row.Count; x++)
            {
                int id = row[x];
                if (id == 8 || id == 9) result.Add(new Vector2Int(x, y));
            }
        }
        return result;
    }

    private bool InBounds(Vector2Int pos)
    {
        return CurrentMap != null
            && pos.x >= 0 && pos.y >= 0
            && pos.x < CurrentMap.width && pos.y < CurrentMap.height;
    }

    /// <summary>物体层里会占位的 id：1-3 彩色门 / 4-6 魂门 / 7 与 21-27 移涌门 / 8-9 楼梯。</summary>
    private static bool IsBlockingObjectId(int id)
    {
        return (id >= 1 && id <= 9) || (id >= 21 && id <= 27);
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

        offsetX = -(data.width  - 1) / 2f;
        offsetY =  (data.height - 1) / 2f;

        // 逐格生成四层内容
        int floor = data.floor;
        bool hasMemory = FloorMemoryManager.Instance != null;
        Debug.Log($"[MapGenerator] 开始生成楼层 {floor}，FloorMemoryManager = {(hasMemory ? "存在" : "NULL（记忆功能不可用）")}");
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                Vector3 cellPos = new Vector3(x + offsetX, -y + offsetY, 0f);
                Vector2Int gridPos = new Vector2Int(x, y);

                SpawnTerrain(GetCell(data.terrain, x, y), cellPos, gridPos, floor);
                SpawnObject( GetCell(data.objects,  x, y), cellPos, gridPos, floor);
                SpawnEnemy(  GetCell(data.enemies,  x, y), cellPos, gridPos, floor);
                SpawnItem(   GetCell(data.items,    x, y), cellPos, gridPos, floor);
                SpawnNpc(    GetCell(data.npcs,     x, y), cellPos, gridPos, floor);
            }
        }

        // 根据进入方向自动定位对应的楼梯作为出生点
        AutoSetSpawnFromStairs(data);

        // 设置玩家出生点
        SetPlayerPosition(data.player_spawn, offsetX, offsetY);

        // 换层：移动轨迹从本层出生点重新开始（阵亡时沿轨迹回退要用）
        if (player != null)
            player.GetComponent<PlayerMove>()?.ResetTrail(player.position);

        // 自动适配相机
        if (autoFitCamera)
        {
            SetupCamera();
        }

        Debug.Log($"[MapGenerator] 地图加载完成：{data.name}（{data.width}×{data.height}，楼层 {data.floor}）");

        // 玩家日志：楼层段标题。必须排在下面的夹击扫描之前 ——
        // 进楼层时被夹击扣掉的血要记在这一段里面。
        PlayerLog.Section($"进入 {data.floor}F");

        // 挑战模式：在上一次阵亡的位置生成『安特罗波斯』的祝福（其它模式/没有记录时不做事）。
        // ⚠️ 必须排在 Section 之后 —— 否则「出现」那行会落到上一层的小节里。
        AnthroposBlessing.TrySpawnOnFloor(this, data);

        onFloorLoaded?.Invoke(data);

        // 标记楼层为已访问（用于快速跳层功能）
        FloorMemoryManager.Instance?.MarkFloorVisited(data.floor);

        // 特殊祝福生命周期：进入楼层
        BlessingManager.Instance?.OnEnterFloor(
            FindAnyObjectByType<PlayerData>(),
            data.floor,
            FindAnyObjectByType<BattleUI>()
        );

        // 夹击阵型扫描：进入楼层时检测预置的 ABA/AAA 站位
        PincerAttack.CheckFloorEntry(FindAnyObjectByType<PlayerData>());

        // 挑战模式：按掉落楼层 + 开局钥匙数发放额外祝福。
        // 必须排在最后 —— 前面可能改 HP / 开会话面板，弹祝福要等这些都结束。
        // 延后一帧：BlessingManager.OnPanelOpen 是 PlayerMove.Start 里订阅的，
        // 与这里的 Start 顺序不保证，同步弹会出现「面板开着但玩家还能走」。
        StartCoroutine(TriggerChallengeBonusNextFrame(data.floor));
    }

    /// <summary>
    /// 延迟一帧再发挑战奖励 —— 等 PlayerMove / BlessingManager 的 Start 订阅都就位。
    /// 期间如果又换了层，直接放弃这次。
    /// </summary>
    private System.Collections.IEnumerator TriggerChallengeBonusNextFrame(int floor)
    {
        yield return null;

        if (CurrentMap == null || CurrentMap.floor != floor) yield break;
        ChallengeBonus.TryTriggerOnFloor(floor);
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

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[MapGenerator] 未找到 Main Camera，跳过相机适配");
            return;
        }

        // 固定相机大小（与 Main Camera 编辑器中设置保持一致）
        cam.orthographicSize = 6.5f;

        // 居中
        Vector3 pos = cam.transform.position;
        cam.transform.position = new Vector3(0f, 0f, pos.z);

        Debug.Log($"[MapGenerator] 相机适配：size={cam.orthographicSize:F2}，位置=(0, 0, {pos.z})");
    }

    // ========================================================================
    //  生成四层内容（查字典 → 实例化）
    // ========================================================================

    private void SpawnTerrain(int id, Vector3 pos, Vector2Int gridPos, int floor)
    {
        // 地形层：0 = 空地（不生成），1+ 查表
        if (id == 0) return;

        // 若该墙已被移除（记忆），跳过生成
        FloorState state = FloorMemoryManager.Instance?.GetState(floor);
        if (state != null && state.IsWallRemoved(gridPos))
            return;

        GameObject obj = SpawnFromMap(terrainMap, id, pos);
        if (obj != null)
        {
            terrainObjects[gridPos] = obj;

            // 冰面：由预制体上的 IceTile 标记声明，登记格坐标供玩家滑行查询
            if (obj.GetComponent<IceTile>() != null)
                iceCells.Add(gridPos);
        }
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

        ApplyFloorInfo(obj, gridPos, floor);
    }

    private void SpawnEnemy(int id, Vector3 worldPos, Vector2Int gridPos, int floor)
    {
        if (id == 0) return;

        FloorMemoryManager mgr = FloorMemoryManager.Instance;
        FloorState state = mgr?.GetState(floor);

        if (state != null && state.IsEntityCleared(gridPos))
        {
            // 敌人已击败 — 通过 DropManager 复活尚未被拾取的掉落物
            if (enemyMap != null && enemyMap.TryGetValue(id, out PrefabEntry entry) && entry.prefab != null)
            {
                DropManager.Instance?.RespawnDropsForEnemy(entry.prefab, worldPos, floor, state, mapContainer);
            }
            return;
        }

        if (enemyMap == null || !enemyMap.TryGetValue(id, out PrefabEntry entry2))
        {
            Debug.LogWarning($"[MapGenerator] 未注册的敌人 ID={id}");
            return;
        }

        GameObject obj = Instantiate(entry2.prefab, worldPos, Quaternion.identity, mapContainer);
        obj.name = entry2.displayName;

        ApplyFloorInfo(obj, gridPos, floor);
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

        // 「楼层 + 网格坐标」的写入收敛到 DropManager.SetPickupInfo 一处。
        // 原先这里和 DropManager 各维护一份拾取物类型清单，掉落物走的是 DropManager 那份、
        // 地图预置物走的是这份，两边一不同步就会出现「掉落物重返楼层重复刷出」。
        DropManager.SetPickupInfo(obj, gridPos, floor);
    }

    private void SpawnNpc(int id, Vector3 worldPos, Vector2Int gridPos, int floor)
    {
        if (id == 0) return;

        if (npcMap == null || !npcMap.TryGetValue(id, out PrefabEntry entry))
        {
            Debug.LogWarning($"[MapGenerator] 未注册的NPC ID={id}");
            return;
        }

        // 若该NPC已被移除（记忆），跳过生成；但需复活尚未被拾取的掉落物
        FloorState state = FloorMemoryManager.Instance?.GetState(floor);
        if (state != null && state.IsEntityCleared(gridPos))
        {
            if (entry.prefab != null)
            {
                DropManager.Instance?.RespawnDropsForEnemy(entry.prefab, worldPos, floor, state, mapContainer);
            }
            return;
        }

        GameObject obj = Instantiate(entry.prefab, worldPos, Quaternion.identity, mapContainer);
        obj.name = entry.displayName;

        ApplyFloorInfo(obj, gridPos, floor);
    }

    /// <summary>
    /// 把「我在第几层、第几格」写给对象上**所有**实现 IFloorEntity 的组件（含子物体）。
    ///
    /// 这是全工程唯一的楼层坐标写入口。原先每个 Spawn 方法各维护一份组件清单，
    /// 清单之间互不相通 —— 预制体一旦跨层使用（如 enemies 层的 VampireLord 挂的是
    /// NpcBattler 而非 EnemyController）就会漏写，坐标停在默认 (0,0) / 0，
    /// 击败后往第 0 层记了一笔，重返本层就「复活」。
    /// </summary>
    private static void ApplyFloorInfo(GameObject obj, Vector2Int gridPos, int floor)
    {
        if (obj == null) return;

        foreach (IFloorEntity entity in obj.GetComponentsInChildren<IFloorEntity>(true))
            entity.SetFloorInfo(gridPos, floor);
    }

    /// <summary>从查找表中取出对应的 PrefabEntry 并实例化，返回实例化的 GameObject（失败返回 null）</summary>
    private GameObject SpawnFromMap(Dictionary<int, PrefabEntry> map, int id, Vector3 pos)
    {
        if (map == null)
        {
            Debug.LogWarning($"[MapGenerator] 查找表未初始化，跳过 ID={id}");
            return null;
        }

        if (map.TryGetValue(id, out PrefabEntry entry))
        {
            return InstantiatePrefab(entry.prefab, pos, entry.displayName);
        }
        else
        {
            Debug.LogWarning($"[MapGenerator] 未注册的 ID={id}，请在 Inspector 中补充对应 PrefabEntry");
            return null;
        }
    }

    /// <summary>实际的 Instantiate 操作，返回实例化的 GameObject（失败返回 null）</summary>
    private GameObject InstantiatePrefab(GameObject prefab, Vector3 pos, string displayName)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[MapGenerator] Prefab 为空：{displayName}");
            return null;
        }

        GameObject obj = Instantiate(prefab, pos, Quaternion.identity, mapContainer);
        if (!string.IsNullOrEmpty(displayName))
        {
            obj.name = displayName;
        }
        return obj;
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
        npcMap     = BuildTable(npcPrefabs);
        BuildBlockedTerrainIds();
    }

    /// <summary>
    /// 哪些地形 id 挡路：看预制体上有没有**启用**的碰撞体，不写死 id。
    /// 地板砖（草地/浅草地/冰面）的碰撞体是关的；墙、冰块、大理石、秘术砖是开的。
    /// </summary>
    private void BuildBlockedTerrainIds()
    {
        blockedTerrainIds.Clear();
        if (terrainPrefabs == null) return;

        foreach (PrefabEntry entry in terrainPrefabs)
        {
            if (entry == null || entry.prefab == null) continue;

            foreach (Collider2D col in entry.prefab.GetComponentsInChildren<Collider2D>(true))
            {
                if (!col.enabled) continue;
                blockedTerrainIds.Add(entry.id);
                break;
            }
        }
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

        Vector3 worldPos = new Vector3(gridPos.x + offsetX, -gridPos.y + offsetY, 0f);

        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, mapContainer);
        obj.name = $"{prefab.name}_({gridPos.x},{gridPos.y})";

        ApplyFloorInfo(obj, gridPos, floor);

        BattleDoorController door = obj.GetComponent<BattleDoorController>();
        if (door != null)
        {
            door.Initialize();
        }
        else
        {
            Debug.LogWarning($"[MapGenerator] SpawnBattleDoor: Prefab {prefab.name} 上未找到 BattleDoorController");
        }
    }

    // ========================================================================
    //  怪物手册接口
    // ========================================================================

    /// <summary>根据敌人ID获取EnemyStats（从prefab上读取），用于怪物手册</summary>
    public EnemyStats GetEnemyStatsByID(int enemyId)
    {
        if (enemyMap == null || !enemyMap.TryGetValue(enemyId, out PrefabEntry entry) || entry.prefab == null)
            return null;

        EnemyController ec = entry.prefab.GetComponent<EnemyController>();
        return ec != null ? ec.Stats : null;
    }

    /// <summary>获取当前楼层中所有仍存活的唯一敌人ID列表（去重、排除0、排除已击败）</summary>
    public List<int> GetCurrentFloorUniqueEnemyIDs()
    {
        List<int> ids = new List<int>();
        if (CurrentMap?.enemies == null) return ids;

        FloorState state = FloorMemoryManager.Instance?.GetState(CurrentMap.floor);
        HashSet<int> seen = new HashSet<int>();

        for (int y = 0; y < CurrentMap.enemies.Count; y++)
        {
            var row = CurrentMap.enemies[y];
            if (row == null) continue;
            for (int x = 0; x < row.Count; x++)
            {
                int id = row[x];
                if (id == 0 || seen.Contains(id)) continue;

                // 该格子的敌人已被击败则跳过；同一类型其它格子可能仍存活
                Vector2Int pos = new Vector2Int(x, y);
                if (state != null && state.IsEntityCleared(pos)) continue;

                seen.Add(id);
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>
    /// 获取当前楼层所有仍存活的、带 NpcBattler 脚本的 NPC 敌人数据资产（去重）。
    /// 用于怪物手册显示对话战斗类敌人。
    /// </summary>
    public List<EnemyStats> GetCurrentFloorNpcBattleStats()
    {
        List<EnemyStats> result = new List<EnemyStats>();
        if (CurrentMap?.npcs == null) return result;

        FloorState state = FloorMemoryManager.Instance?.GetState(CurrentMap.floor);
        HashSet<EnemyStats> seen = new HashSet<EnemyStats>();

        for (int y = 0; y < CurrentMap.npcs.Count; y++)
        {
            var row = CurrentMap.npcs[y];
            if (row == null) continue;
            for (int x = 0; x < row.Count; x++)
            {
                int id = row[x];
                if (id == 0) continue;
                if (npcMap == null || !npcMap.TryGetValue(id, out PrefabEntry entry) || entry.prefab == null)
                    continue;

                NpcBattler battler = entry.prefab.GetComponentInChildren<NpcBattler>();
                if (battler == null || battler.Stats == null || seen.Contains(battler.Stats)) continue;

                // 该格子的NPC已被移除（战斗胜利后消失）则跳过
                Vector2Int pos = new Vector2Int(x, y);
                if (state != null && state.IsEntityCleared(pos)) continue;

                seen.Add(battler.Stats);
                result.Add(battler.Stats);
            }
        }
        return result;
    }

    /// <summary>销毁挂载点下的所有子物体</summary>
    private void ClearMap()
    {
        if (mapContainer == null) return;

        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapContainer.GetChild(i).gameObject);
        }

        terrainObjects.Clear();
        iceCells.Clear();
        Debug.Log("[MapGenerator] 地图已清空");
    }

    /// <summary>
    /// 移除指定网格坐标的墙（变为可通行）。仅移除当前已生成的地形物体，
    /// 记忆记录由调用方（如 WallRemover）通过 FloorMemoryManager 完成。
    /// </summary>
    public void RemoveWallAt(Vector2Int gridPos)
    {
        if (terrainObjects.TryGetValue(gridPos, out GameObject obj))
        {
            if (obj != null)
                Destroy(obj);
            terrainObjects.Remove(gridPos);
            Debug.Log($"[MapGenerator] 已移除墙 ({gridPos.x}, {gridPos.y})");
        }
        else
        {
            Debug.LogWarning($"[MapGenerator] 坐标 ({gridPos.x}, {gridPos.y}) 没有已生成的地形物体");
        }
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
