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

        // 无玩家时两个全局道具都保留文件旧值，不清零 ——
        // 在 Opening / Credits / Ending 这类没有玩家的场景里误调一次就是玩家资产清零。
        if (player != null)
        {
            globalData.aeonKeys = player.GetKeyCount(KeyType.Aeon);
            globalData.divineSpark = player.DivineSpark;
        }

        // 最近一局的模式：给「新游戏」推断用（自动/手动存档被清掉后还能知道该重开哪种模式）
        globalData.gameMode = (int)GameSession.Mode;

        WriteJson(globalSavePath, globalData);
        Debug.Log($"[SaveManager] 全局存档已保存 → {globalSavePath}" +
                  $"（aeonKeys = {globalData.aeonKeys}，divineSpark = {globalData.divineSpark}，" +
                  $"mode = {GameSession.Mode}）");
    }

    /// <summary>保存主动存档（P 键 / 未来 UI 按钮），写入 game_save.json。</summary>
    public void SaveGame()
    {
        SaveGameTo(gameSavePath, PlayerLogSlot.Manual);
    }

    /// <summary>保存自动存档，写入 auto_save.json（不覆盖主动存档）。</summary>
    public void SaveAutoGame()
    {
        SaveGameTo(autoSavePath, PlayerLogSlot.Auto);
    }

    /// <summary>将当前游戏状态写入指定存档文件。写盘成功后，同步把玩家日志落盘到同槽位的文件。</summary>
    private void SaveGameTo(string path, PlayerLogSlot slot)
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null)
        {
            Debug.LogWarning("[SaveManager] 未找到 PlayerData，无法存档");
            return;
        }

        // 构建游戏存档
        // 属性一律读 Base*（战斗前真实值）：Attack / Defense / AttackCount / DamageReduction
        // 在战斗中会返回含战斗内加成的实时值，存进去会把临时 buff 永久烙进存档。
        GameSaveData data = new GameSaveData
        {
            hp = player.HP,
            attack = player.BaseAttack,
            defense = player.BaseDefense,
            attackCount = player.BaseAttackCount,
            lifeSteal = player.BaseLifeSteal,
            reflectDamage = player.BaseReflectDamage,
            damageReduction = player.BaseDamageReduction,
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
            pendingEnemyHalve = player.PendingEnemyHalve,
            aleCount = player.AleCount,
            manaEssenceCount = player.ManaEssenceCount,
            pendingAleStacks = player.PendingAleStacks,
            pendingManaEssence = player.PendingManaEssence,
            playerX = player.transform.position.x,
            playerY = player.transform.position.y,
            playerZ = player.transform.position.z
        };

        // 本局模式 + 『安特罗波斯』的阵亡记录。
        // 记录进存档是硬要求：本局读档后要能继续按同一条记录生成祝福。
        data.gameMode = (int)GameSession.Mode;
        data.anthroposRecord = AnthroposBlessing.RunRecord;

        // 挑战模式的敌人数值倍率按「开局时的钥匙数」定格 —— 存下来，读档才能复现同一个倍率
        data.startAeonKeys = EnemyScaling.KeyCount;

        // 挑战模式额外祝福的发放进度（发到哪一层、每个节点还剩几次）
        data.challengeBonus = ChallengeBonus.Snapshot();

        // 装备效果（运行时 AddComponent 的组件，不在 PlayerData 字段里，不单独存就会在读档后消失）
        HolySwordEffect holySword = player.GetComponent<HolySwordEffect>();
        data.hasHolySword = holySword != null;
        if (holySword != null) data.holySwordMultiplierPercent = holySword.MultiplierPercent;

        data.hasHolyShield = player.GetComponent<HolyShieldEffect>() != null;

        LeviathansMarrowEffect marrow = player.GetComponent<LeviathansMarrowEffect>();
        data.hasLeviathansMarrow = marrow != null;
        if (marrow != null) data.leviathansMarrowInterval = marrow.FreezeInterval;

        BehemothsFangEffect fang = player.GetComponent<BehemothsFangEffect>();
        data.hasBehemothsFang = fang != null;
        if (fang != null)
        {
            data.fangStacksPerAttack = fang.StacksPerAttack;
            data.fangMaxStacks = fang.MaxStacks;
            data.fangPercentPerStack = fang.PercentPerStack;
        }

        OuroborosCrucifixEffect ouroboros = player.GetComponent<OuroborosCrucifixEffect>();
        data.hasOuroborosCrucifix = ouroboros != null;
        if (ouroboros != null) data.ouroborosPercentOfHigher = ouroboros.PercentOfHigher;

        // 特殊祝福效果
        data.specialBlessings = BlessingManager.Instance != null
            ? BlessingManager.Instance.GetActiveEffectLevels()
            : new Dictionary<string, int>();

        // 本局已获得的祝福记录（含 DirectBonus 型，specialBlessings 覆盖不到）
        data.obtainedBlessings = BlessingManager.Instance != null
            ? BlessingManager.Instance.GetObtainedBlessings()
            : new Dictionary<string, int>();

        // 祝福的持久运行时状态（跨战斗累计的计数，如卡巴拉生命之树的剩余触发次数）
        data.blessingStates = BlessingManager.Instance != null
            ? BlessingManager.Instance.GetActiveEffectStates()
            : new Dictionary<string, int>();

        // 商店购买次数（影响下一次购买价格）
        data.shopPurchaseCount = NPCController.GetGlobalPurchaseCount();

        // 特殊敌人击败信号
        data.defeatedSpecialEnemies = SpecialEnemyManager.Instance != null
            ? SpecialEnemyManager.Instance.GetDefeatedIds()
            : new List<string>();

        // 对话标记（NPC 收尾逻辑已结算过）
        data.dialogueFlags = DialogueFlags.GetIds();

        // 楼层状态
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        data.currentFloor = mapGen != null ? mapGen.CurrentFloor : GameSaveData.NoFloor;
        data.floorStates = FloorMemoryManager.Instance != null
            ? FloorMemoryManager.Instance.GetAllFloorEntries()
            : null;
        data.visitedFloors = FloorMemoryManager.Instance != null
            ? FloorMemoryManager.Instance.GetVisitedFloors()
            : null;

        // 只有真的写盘成功才落盘玩家日志 —— 写失败时日志必须保持原样，
        // 否则「没存档的操作不进日志」这条规则会被破掉。
        bool written = WriteJson(path, data);

        // 同时保存全局存档
        SaveGlobal();

        if (written) PlayerLog.Commit(slot);

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

    /// <summary>将全局存档中的 divineSpark 应用到玩家</summary>
    public void ApplyGlobalDivineSpark()
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null) return;

        GlobalSaveData globalData = LoadGlobal();
        player.SetDivineSpark(globalData.divineSpark);
    }

    /// <summary>神圣火花 +amount：先改玩家再落盘，否则 SaveGlobal 会用旧值覆盖</summary>
    public void AddDivineSpark(int amount = 1)
    {
        GlobalSaveData globalData = LoadGlobal();
        PlayerData player = FindAnyObjectByType<PlayerData>();

        if (player != null)
        {
            player.AddDivineSpark(amount);
            globalData.divineSpark = player.DivineSpark;
        }
        else
        {
            // 无玩家时退化为纯全局累加
            globalData.divineSpark += amount;
        }

        WriteJson(globalSavePath, globalData);
        Debug.Log($"[SaveManager] 神圣火花 +{amount}（总计 {globalData.divineSpark}），已写入全局存档");
    }

    /// <summary>是否已拥有神圣火花（数量大于 0）。</summary>
    public bool HasDivineSpark() => GetDivineSparkCount() > 0;

    /// <summary>获取神圣火花数量。</summary>
    public int GetDivineSparkCount() => LoadGlobal().divineSpark;

    // ============================================================
    //  『安特罗波斯』的阵亡记录（全局存档 = 上一次阵亡，跨局继承）
    // ============================================================

    /// <summary>
    /// 把这次阵亡的位置与数值写进全局存档（覆盖上一次）。
    /// 只写这一个字段，其余全局数据（Aeon 钥匙、神圣火花、战斗速度）原样保留。
    /// </summary>
    public void SaveGlobalAnthroposRecord(AnthroposRecord record)
    {
        if (record == null) return;

        GlobalSaveData globalData = LoadGlobal();
        globalData.anthroposRecord = record;
        // 阵亡本身就说明这一局是什么模式：一起记进全局，供「新游戏」沿用
        globalData.gameMode = (int)GameSession.Mode;
        WriteJson(globalSavePath, globalData);
        Debug.Log($"[SaveManager] 阵亡记录已写入全局存档（{record.floor}F）");
    }

    /// <summary>清掉全局存档里的阵亡记录（祝福被拾取后调用；本局存档里那份保留）。</summary>
    public void ClearGlobalAnthroposRecord()
    {
        GlobalSaveData globalData = LoadGlobal();
        if (globalData.anthroposRecord == null) return;

        globalData.anthroposRecord = null;
        WriteJson(globalSavePath, globalData);
        Debug.Log("[SaveManager] 全局存档的阵亡记录已清除");
    }

    /// <summary>读取主动存档（O 键），从 game_save.json。</summary>
    public void LoadGame()
    {
        LoadGameFrom(gameSavePath, PlayerLogSlot.Manual);
    }

    /// <summary>读取自动存档，从 auto_save.json。返回是否成功读取。</summary>
    public bool LoadAutoGame()
    {
        return LoadGameFrom(autoSavePath, PlayerLogSlot.Auto);
    }

    /// <summary>
    /// 只读地看一眼「本局模式」记在哪：自动存档 → 主动存档 → 全局存档，都没有返回 null。
    /// 供「新游戏」沿用上一局的模式用 —— 不改任何文件。
    /// 全局那份是兜底：新游戏会清掉自动存档，重启后也能记住上一次玩的是哪种模式。
    /// </summary>
    public static GameMode? PeekSavedMode()
    {
        return PeekSavedMode("auto_save.json")
            ?? PeekSavedMode("game_save.json")
            ?? ParseMode(LoadGlobalData().gameMode);
    }

    private static GameMode? PeekSavedMode(string fileName)
    {
        string saveDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "save");
        string path = Path.Combine(saveDir, fileName);
        if (!File.Exists(path)) return null;

        try
        {
            GameSaveData data = JsonConvert.DeserializeObject<GameSaveData>(File.ReadAllText(path));
            return data != null ? ParseMode(data.gameMode) : null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] 读取存档模式失败：{path}\n{e.Message}");
            return null;
        }
    }

    /// <summary>存档里的 int → 模式；越界/老档缺失返回 null。</summary>
    private static GameMode? ParseMode(int raw)
    {
        return raw >= 0 && raw <= (int)GameMode.Endless ? (GameMode)raw : (GameMode?)null;
    }

    /// <summary>从指定存档文件读取游戏状态。返回是否成功读取。</summary>
    private bool LoadGameFrom(string path, PlayerLogSlot slot)
    {
        GameSaveData data = ReadJson<GameSaveData>(path);
        if (data == null)
        {
            Debug.LogWarning($"[SaveManager] 存档不存在，无法读档：{path}");
            return false;
        }

        Debug.Log($"[SaveManager] 存档已读取 ← {path}");

        // 世界即将退回这份存档的状态 → 玩家日志也整份退回（用该槽位的日志文件回填缓冲区），
        // 之后新产生的操作才接在后面。必须放在下面 LoadFloor 之前 —— 它要写「进入 N 层」段标题。
        PlayerLog.Adopt(slot);

        // 0. 恢复本局模式与『安特罗波斯』的阵亡记录。
        //    必须在下面加载楼层之前 —— 生成祝福时要按模式判断。
        GameSession.Choose(ParseMode(data.gameMode) ?? GameMode.Story);
        AnthroposBlessing.RestoreRun(data.anthroposRecord);

        // 0.5. 挑战模式的敌人倍率按存档里记的开局钥匙数复现（必须在加载楼层之前）。
        //      旧存档没有这个字段（-1）→ 保持 BeginRun 按全局钥匙算出来的结果。
        EnemyScaling.RestoreRun(data.startAeonKeys);

        // 0.6. 挑战模式额外祝福的进度 —— 必须在加载楼层之前，楼层加载时会按它决定还要不要弹。
        ChallengeBonus.RestoreRun(data.challengeBonus);

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
            player.SetPendingEnemyHalve(data.pendingEnemyHalve);
            player.SetAleCount(data.aleCount);
            player.SetManaEssenceCount(data.manaEssenceCount);
            player.SetPendingAleStacks(data.pendingAleStacks);
            player.SetPendingManaEssence(data.pendingManaEssence);

            // 装备效果（运行时组件，按存档重新挂/摘 —— 不恢复就会「捡过的东西读档后消失」）
            RestoreEquipment(player, data);
            // aeonKeys 从全局存档覆盖
        }
        else
        {
            Debug.LogWarning("[SaveManager] 未找到 PlayerData，玩家属性恢复跳过");
        }

        // 2. 从全局存档覆盖全局道具
        ApplyGlobalAeonKeys();
        ApplyGlobalDivineSpark();

        // 2.5. 恢复特殊祝福效果
        //      ⚠️ 必须连 blessingStates 一起恢复：卡巴拉生命之树的「剩余触发次数」
        //      就存在这里，不恢复会导致读档后次数回满 → 反复触发 30 层（2026-10-05 修复）。
        if (BlessingManager.Instance != null && data.specialBlessings != null)
        {
            BlessingManager.Instance.RestoreEffects(data.specialBlessings, data.blessingStates);
        }

        // 2.55. 恢复已获得祝福记录（必须在 RestoreEffects 之外单独恢复：
        //       它记录的是「获得清单」，与当前生效的特殊祝福是两份数据）
        //       旧存档没有这个字段 → 用特殊祝福层数兜底做种子，避免读档后记录看着是空的。
        Dictionary<string, int> obtained = data.obtainedBlessings;
        if ((obtained == null || obtained.Count == 0)
            && data.specialBlessings != null && data.specialBlessings.Count > 0)
        {
            obtained = new Dictionary<string, int>(data.specialBlessings);
            Debug.Log("[SaveManager] 旧存档兼容：以特殊祝福层数作为「已获得祝福」记录的初始值");
        }
        BlessingManager.Instance?.RestoreObtainedBlessings(obtained);

        // 2.56. 恢复商店购买次数（影响购买价格，须在玩家进入商店前恢复）
        NPCController.SetPurchaseCount(data.shopPurchaseCount);

        // 2.6. 恢复特殊敌人击败信号
        SpecialEnemyManager.Instance?.RestoreDefeated(data.defeatedSpecialEnemies);

        // 2.65. 恢复对话标记（旧存档没有该字段 → null，Restore 内部当作空集合）
        DialogueFlags.Restore(data.dialogueFlags);

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
        //    ⚠️ 必须用 NoFloor 哨兵判断，不能写 >= 0：
        //    地下负层（floor_-01 ~ floor_-07）是合法楼层，>= 0 会把负层当成「无楼层」跳过加载，
        //    地图整张不生成 → 只剩 Unity 默认蓝底（2026-10-05 修复）。
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen != null && data.currentFloor != GameSaveData.NoFloor)
        {
            mapGen.LoadFloor(data.currentFloor);
        }

        // 5. 恢复玩家位置（在楼层加载后设置，因为加载楼层会重置位置）
        if (player != null)
        {
            player.transform.position = new Vector3(data.playerX, data.playerY, data.playerZ);

            // 传送后轨迹要跟着重置：读档没有「走过来的路」，从当前位置重新记
            player.GetComponent<PlayerMove>()?.ResetTrail(player.transform.position);
        }

        return true;
    }

    /// <summary>
    /// 读档恢复装备效果（神圣剑 / 神圣盾 / 利维坦之髓 / 贝希摩斯之牙 / 衔尾之十字）。
    ///
    /// 这些都是运行时 `AddComponent` 挂到玩家身上的组件，**不在 PlayerData 字段里**，
    /// 所以必须在这里按存档重新挂 / 摘：
    ///   · 不恢复 → 读档后装备静默消失。而楼层记忆已经把道具标记成「已拾取」，
    ///     地图上不再刷出，等于白捡（2026-10-04 实测踩到：利维坦之髓捡过、读档后就没了）。
    ///   · 不摘掉存档里没有的 → 读旧档会留下本不该有的装备。
    /// </summary>
    private static void RestoreEquipment(PlayerData player, GameSaveData data)
    {
        // 神圣剑（魔力伤害倍率）
        HolySwordEffect sword = player.GetComponent<HolySwordEffect>();
        if (data.hasHolySword)
        {
            if (sword == null) sword = player.gameObject.AddComponent<HolySwordEffect>();
            sword.MultiplierPercent = data.holySwordMultiplierPercent > 0 ? data.holySwordMultiplierPercent : 200;
        }
        else if (sword != null)
        {
            UnityEngine.Object.Destroy(sword);
        }

        // 神圣盾（免疫魔力光环 / 夹击）
        HolyShieldEffect shield = player.GetComponent<HolyShieldEffect>();
        if (data.hasHolyShield)
        {
            if (shield == null) player.gameObject.AddComponent<HolyShieldEffect>();
        }
        else if (shield != null)
        {
            UnityEngine.Object.Destroy(shield);
        }

        // 利维坦之髓（每隔 N 回合冻结敌人）
        LeviathansMarrowEffect marrow = player.GetComponent<LeviathansMarrowEffect>();
        if (data.hasLeviathansMarrow)
        {
            if (marrow == null) marrow = player.gameObject.AddComponent<LeviathansMarrowEffect>();
            marrow.FreezeInterval = data.leviathansMarrowInterval > 0 ? data.leviathansMarrowInterval : 5;
        }
        else if (marrow != null)
        {
            UnityEngine.Object.Destroy(marrow);
        }

        // 贝希摩斯之牙（每次攻击叠撕裂）
        BehemothsFangEffect fang = player.GetComponent<BehemothsFangEffect>();
        if (data.hasBehemothsFang)
        {
            if (fang == null) fang = player.gameObject.AddComponent<BehemothsFangEffect>();
            fang.StacksPerAttack = data.fangStacksPerAttack > 0 ? data.fangStacksPerAttack : 1;
            fang.MaxStacks = data.fangMaxStacks > 0 ? data.fangMaxStacks : 10;
            fang.PercentPerStack = data.fangPercentPerStack > 0 ? data.fangPercentPerStack : 1;
        }
        else if (fang != null)
        {
            UnityEngine.Object.Destroy(fang);
        }

        // 衔尾之十字（战斗开始时补齐吸血/反伤）
        OuroborosCrucifixEffect ouroboros = player.GetComponent<OuroborosCrucifixEffect>();
        if (data.hasOuroborosCrucifix)
        {
            if (ouroboros == null) ouroboros = player.gameObject.AddComponent<OuroborosCrucifixEffect>();
            ouroboros.PercentOfHigher = data.ouroborosPercentOfHigher > 0 ? data.ouroborosPercentOfHigher : 50;
        }
        else if (ouroboros != null)
        {
            UnityEngine.Object.Destroy(ouroboros);
        }

        Debug.Log($"[SaveManager] 装备恢复：神圣剑={data.hasHolySword}(×{data.holySwordMultiplierPercent}%) " +
                  $"神圣盾={data.hasHolyShield} 利维坦之髓={data.hasLeviathansMarrow}(每{data.leviathansMarrowInterval}回合) " +
                  $"贝希摩斯之牙={data.hasBehemothsFang}({data.fangStacksPerAttack}层/攻,上限{data.fangMaxStacks}) " +
                  $"衔尾之十字={data.hasOuroborosCrucifix}({data.ouroborosPercentOfHigher}%)");
    }

    /// <summary>自动存档（auto_save.json）是否存在。静态方法，无需实例。</summary>
    public static bool HasAutoSave()
    {
        string saveDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "save");
        return File.Exists(Path.Combine(saveDir, "auto_save.json"));
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

        // 日志与存档同生共死
        PlayerLog.Delete(PlayerLogSlot.Auto);
    }

    // ============================================================
    //  战斗速度（全局存档，静态读写，供 Setting / Game 场景使用）
    // ============================================================

    private static string GetGlobalSavePath()
    {
        string saveDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "save");
        return Path.Combine(saveDir, "global.json");
    }

    /// <summary>从磁盘读取全局存档（无需 SaveManager 实例，供 Opening 等场景使用）。失败时返回默认数据。</summary>
    public static GlobalSaveData LoadGlobalData()
    {
        string path = GetGlobalSavePath();
        if (!File.Exists(path)) return new GlobalSaveData();

        try
        {
            GlobalSaveData data = JsonConvert.DeserializeObject<GlobalSaveData>(File.ReadAllText(path));
            return data ?? new GlobalSaveData();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 读取全局存档失败：{path}\n{e}");
            return new GlobalSaveData();
        }
    }

    /// <summary>把一份全局存档整体写回（调用方自行读-改-写，只改自己要改的字段）。</summary>
    public static bool SaveGlobalData(GlobalSaveData data)
    {
        if (data == null) return false;

        string path = GetGlobalSavePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 写入全局存档失败：{path}\n{e}");
            return false;
        }
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

    /// <summary>写 JSON。返回是否写盘成功 —— SaveGameTo 据此决定要不要落盘玩家日志。</summary>
    private bool WriteJson<T>(string path, T data)
    {
        try
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 写入存档失败：{path}\n{e}");
            return false;
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
