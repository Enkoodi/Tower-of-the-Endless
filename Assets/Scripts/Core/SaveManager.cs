using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 存档管理器 — 单例，处理全局存档和游戏存档的保存与读取。
/// 暂定 P 键存档，O 键读档，未来改为 UI 按钮。
/// 存档路径：{项目根目录}/save/
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private string saveDir;
    private string globalSavePath;
    private string gameSavePath;
    private string autoSavePath;

    /// <summary>
    /// 进入 Game 场景时是否读取自动存档（true=继续，false=新游戏）。
    /// 新游戏流程会先设为 false，MapGenerator 消费后复位为 true。
    /// </summary>
    public static bool LoadAutoSaveOnStart { get; set; } = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 存档目录：Assets 的上级目录下的 save 文件夹
        saveDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "save");
        globalSavePath = Path.Combine(saveDir, "global.json");
        gameSavePath = Path.Combine(saveDir, "game_save.json");
        autoSavePath = Path.Combine(saveDir, "auto_save.json");

        EnsureSaveDirectory();
    }

    void Update()
    {
        // 战斗窗口打开时禁用存档/读档按键
        if (BattleManager.Instance != null && BattleManager.Instance.IsFighting) return;

        // 暂定：P 存档，O 读档。未来改为 UI 按钮
        if (Input.GetKeyDown(KeyCode.P))
        {
            SaveGame();
        }
        else if (Input.GetKeyDown(KeyCode.O))
        {
            LoadGame();
        }
    }

    // ============================================================
    //  保存
    // ============================================================

    /// <summary>保存全局存档</summary>
    public void SaveGlobal()
    {
        // 先读取现有全局存档，避免覆盖神圣火花等其它全局字段
        GlobalSaveData globalData = LoadGlobal();
        PlayerData player = FindAnyObjectByType<PlayerData>();
        globalData.aeonKeys = player != null ? player.GetKeyCount(KeyType.Aeon) : 0;

        WriteJson(globalSavePath, globalData);
        Debug.Log($"[SaveManager] 全局存档已保存 → {globalSavePath}");
    }

    /// <summary>保存主动存档（P 键 / 未来 UI 按钮），写入 game_save.json。</summary>
    public void SaveGame()
    {
        SaveGameTo(gameSavePath);
    }

    /// <summary>保存自动存档，写入 auto_save.json（不覆盖主动存档）。</summary>
    public void SaveAutoGame()
    {
        SaveGameTo(autoSavePath);
    }

    /// <summary>将当前游戏状态写入指定存档文件。</summary>
    private void SaveGameTo(string path)
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null)
        {
            Debug.LogWarning("[SaveManager] 未找到 PlayerData，无法存档");
            return;
        }

        // 构建游戏存档
        GameSaveData data = new GameSaveData
        {
            hp = player.HP,
            attack = player.Attack,
            defense = player.Defense,
            attackCount = player.AttackCount,
            lifeSteal = player.LifeSteal,
            reflectDamage = player.ReflectDamage,
            damageReduction = player.DamageReduction,
            manaCharge = player.ManaCharge,
            manaMax = player.ManaMax,
            speed = player.Speed,
            goldMultiplier = player.GoldMultiplier,
            hpMultiplier = player.HPMultiplier,
            attackMultiplier = player.AttackMultiplier,
            defenseMultiplier = player.DefenseMultiplier,
            gold = player.Gold,
            yellowKeys = player.GetKeyCount(KeyType.Yellow),
            blueKeys = player.GetKeyCount(KeyType.Blue),
            redKeys = player.GetKeyCount(KeyType.Red),
            psycheKeys = player.GetKeyCount(KeyType.Psyche),
            aeonKeys = player.GetKeyCount(KeyType.Aeon),
            upTeleporterCount = player.UpTeleporterCount,
            downTeleporterCount = player.DownTeleporterCount,
            enemyHalveItemCount = player.EnemyHalveItemCount,
            pendingEnemyHalveBattles = player.PendingEnemyHalveBattles,
            playerX = player.transform.position.x,
            playerY = player.transform.position.y,
            playerZ = player.transform.position.z
        };

        // 特殊祝福效果
        data.specialBlessings = BlessingManager.Instance != null
            ? BlessingManager.Instance.GetActiveEffectLevels()
            : new Dictionary<string, int>();

        // 特殊敌人击败信号
        data.defeatedSpecialEnemies = SpecialEnemyManager.Instance != null
            ? SpecialEnemyManager.Instance.GetDefeatedIds()
            : new List<string>();

        // 楼层状态
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        data.currentFloor = mapGen != null ? mapGen.CurrentFloor : -1;
        data.floorStates = FloorMemoryManager.Instance != null
            ? FloorMemoryManager.Instance.GetAllFloorEntries()
            : null;
        data.visitedFloors = FloorMemoryManager.Instance != null
            ? FloorMemoryManager.Instance.GetVisitedFloors()
            : null;

        WriteJson(path, data);

        // 同时保存全局存档
        SaveGlobal();

        Debug.Log($"[SaveManager] 存档已保存 → {path}");
    }

    // ============================================================
    //  读取
    // ============================================================

    /// <summary>读取全局存档，返回数据（失败则返回默认值）</summary>
    public GlobalSaveData LoadGlobal()
    {
        GlobalSaveData data = ReadJson<GlobalSaveData>(globalSavePath);
        if (data == null)
        {
            Debug.Log("[SaveManager] 全局存档不存在，使用默认值");
            data = new GlobalSaveData();
        }
        else
        {
            Debug.Log($"[SaveManager] 全局存档已读取 ← {globalSavePath}，aeonKeys = {data.aeonKeys}");
        }
        return data;
    }

    /// <summary>将全局存档中的 aeonKeys 应用到玩家</summary>
    public void ApplyGlobalAeonKeys()
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null) return;

        GlobalSaveData globalData = LoadGlobal();
        player.SetAeonKeys(globalData.aeonKeys);
    }

    /// <summary>神圣火花数量 +amount，并立即写入全局存档。</summary>
    public void AddDivineSpark(int amount = 1)
    {
        GlobalSaveData globalData = LoadGlobal();
        globalData.divineSpark += amount;
        WriteJson(globalSavePath, globalData);
        Debug.Log($"[SaveManager] 神圣火花 +{amount}（总计 {globalData.divineSpark}），已写入全局存档");
    }

    /// <summary>是否已拥有神圣火花（数量大于 0）。</summary>
    public bool HasDivineSpark() => GetDivineSparkCount() > 0;

    /// <summary>获取神圣火花数量。</summary>
    public int GetDivineSparkCount() => LoadGlobal().divineSpark;

    /// <summary>读取主动存档（O 键），从 game_save.json。</summary>
    public void LoadGame()
    {
        LoadGameFrom(gameSavePath);
    }

    /// <summary>读取自动存档，从 auto_save.json。返回是否成功读取。</summary>
    public bool LoadAutoGame()
    {
        return LoadGameFrom(autoSavePath);
    }

    /// <summary>从指定存档文件读取游戏状态。返回是否成功读取。</summary>
    private bool LoadGameFrom(string path)
    {
        GameSaveData data = ReadJson<GameSaveData>(path);
        if (data == null)
        {
            Debug.LogWarning($"[SaveManager] 存档不存在，无法读档：{path}");
            return false;
        }

        Debug.Log($"[SaveManager] 存档已读取 ← {path}");

        // 1. 恢复玩家属性
        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player != null)
        {
            player.SetHP(data.hp);
            player.SetAttack(data.attack);
            player.SetDefense(data.defense);
            player.SetAttackCount(data.attackCount);
            player.SetLifeSteal(data.lifeSteal);
            player.SetReflectDamage(data.reflectDamage);
            player.SetDamageReduction(data.damageReduction);
            player.ManaCharge = data.manaCharge;
            player.SetManaMax(data.manaMax);
            player.SetSpeed(data.speed);
            player.SetGoldMultiplier(data.goldMultiplier);
            player.SetHPMultiplier(data.hpMultiplier);
            player.SetAttackMultiplier(data.attackMultiplier);
            player.SetDefenseMultiplier(data.defenseMultiplier);
            player.SetGold(data.gold);
            player.SetKeyCountDirect(KeyType.Yellow, data.yellowKeys);
            player.SetKeyCountDirect(KeyType.Blue, data.blueKeys);
            player.SetKeyCountDirect(KeyType.Red, data.redKeys);
            player.SetKeyCountDirect(KeyType.Psyche, data.psycheKeys);
            player.SetUpTeleporterCount(data.upTeleporterCount);
            player.SetDownTeleporterCount(data.downTeleporterCount);
            player.SetEnemyHalveItemCount(data.enemyHalveItemCount);
            player.SetPendingEnemyHalveBattles(data.pendingEnemyHalveBattles);
            // aeonKeys 从全局存档覆盖
        }
        else
        {
            Debug.LogWarning("[SaveManager] 未找到 PlayerData，玩家属性恢复跳过");
        }

        // 2. 从全局存档覆盖 aeonKeys
        ApplyGlobalAeonKeys();

        // 2.5. 恢复特殊祝福效果
        if (BlessingManager.Instance != null && data.specialBlessings != null)
        {
            BlessingManager.Instance.RestoreEffects(data.specialBlessings);
        }

        // 2.6. 恢复特殊敌人击败信号
        SpecialEnemyManager.Instance?.RestoreDefeated(data.defeatedSpecialEnemies);

        // 3. 恢复楼层记忆
        if (FloorMemoryManager.Instance != null && data.floorStates != null)
        {
            FloorMemoryManager.Instance.RestoreFromEntries(data.floorStates);
        }

        // 3.5. 恢复已访问楼层列表（快速跳层功能）
        if (FloorMemoryManager.Instance != null && data.visitedFloors != null)
        {
            FloorMemoryManager.Instance.SetVisitedFloors(data.visitedFloors);
        }

        // 4. 重新加载楼层
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen != null && data.currentFloor >= 0)
        {
            mapGen.LoadFloor(data.currentFloor);
        }

        // 5. 恢复玩家位置（在楼层加载后设置，因为加载楼层会重置位置）
        if (player != null)
        {
            player.transform.position = new Vector3(data.playerX, data.playerY, data.playerZ);
        }

        return true;
    }

    /// <summary>清除自动存档（auto_save.json），不影响主动存档。静态方法，无需实例。</summary>
    public static void ClearAutoSave()
    {
        string saveDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "save");
        string autoSavePath = Path.Combine(saveDir, "auto_save.json");

        if (File.Exists(autoSavePath))
        {
            File.Delete(autoSavePath);
            Debug.Log($"[SaveManager] 已清除自动存档 → {autoSavePath}");
        }
        else
        {
            Debug.Log("[SaveManager] 自动存档不存在，无需清除");
        }
    }

    // ============================================================
    //  战斗速度（全局存档，静态读写，供 Setting / Game 场景使用）
    // ============================================================

    private static string GetGlobalSavePath()
    {
        string saveDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "save");
        return Path.Combine(saveDir, "global.json");
    }

    /// <summary>读取全局存档中的战斗速度（无存档或读取失败时返回默认 1f）。</summary>
    public static float LoadBattleSpeed()
    {
        string path = GetGlobalSavePath();
        if (!File.Exists(path)) return 1f;

        try
        {
            string json = File.ReadAllText(path);
            GlobalSaveData data = JsonConvert.DeserializeObject<GlobalSaveData>(json);
            if (data == null) return 1f;
            return data.battleSpeed > 0f ? data.battleSpeed : 1f;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 读取战斗速度失败：{path}\n{e}");
            return 1f;
        }
    }

    /// <summary>将战斗速度写入全局存档（合并现有全局数据后写回，不覆盖其它字段）。</summary>
    public static void SaveBattleSpeed(float speed)
    {
        string path = GetGlobalSavePath();

        GlobalSaveData data = null;
        if (File.Exists(path))
        {
            try
            {
                data = JsonConvert.DeserializeObject<GlobalSaveData>(File.ReadAllText(path));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveManager] 读取全局存档失败，将新建：{e.Message}");
            }
        }
        if (data == null) data = new GlobalSaveData();

        data.battleSpeed = speed;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
            Debug.Log($"[SaveManager] 战斗速度已保存 → {speed}（{path}）");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 保存战斗速度失败：{path}\n{e}");
        }
    }

    // ============================================================
    //  文件读写（使用 Newtonsoft.Json）
    // ============================================================

    private void EnsureSaveDirectory()
    {
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
            Debug.Log($"[SaveManager] 创建存档目录：{saveDir}");
        }
    }

    private void WriteJson<T>(string path, T data)
    {
        try
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 写入存档失败：{path}\n{e}");
        }
    }

    private T ReadJson<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 读取存档失败：{path}\n{e}");
            return null;
        }
    }
}
