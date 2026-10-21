using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 上一次挑战模式阵亡的记录 —— 阵亡位置 + 阵亡时的部分属性。
/// 全局存档存一份（跨局继承），本局存档也存一份（本局读档不丢）。
/// </summary>
[System.Serializable]
public class AnthroposRecord
{
    /// <summary>阵亡楼层（GameSaveData.NoFloor = 无效记录）。</summary>
    public int floor = GameSaveData.NoFloor;
    public int gridX;
    public int gridY;

    // 只收这 8 项：攻击 / 防御 / 魔力输出最大值 / 魔力充能 / 速度 / 吸血 / 反伤 / 减伤
    public int attack;
    public int defense;
    public int manaMax;
    public int manaCharge;
    public int speed;
    public int lifeSteal;
    public int reflectDamage;
    public int damageReduction;

    /// <summary>楼层有值才算一条有效记录（用方法而不是属性，避免被 Json.NET 一起写进存档）。</summary>
    public bool IsValid() => floor != GameSaveData.NoFloor;
}

/// <summary>
/// 『安特罗波斯』的祝福一次性发放的数值 —— 全部是算好的最终值，
/// PlayerData 那边直接加，**不再乘任何系数**（记录里的数值已经是上一局结算后的结果）。
/// </summary>
public struct AnthroposGrant
{
    public int attack;
    public int defense;
    public int manaMax;
    public int manaCharge;
    public int speed;
    public int lifeSteal;
    public int reflectDamage;
    public int damageReduction;
    public int hp;
}

/// <summary>
/// 『安特罗波斯』的祝福 —— 挑战模式专用，不属于四级稀有度，不进抽取池。
///
/// 生成规则：上一次挑战模式阵亡的位置（楼层 + 格子）。
/// 效果：拿到那次阵亡时 8 项数值的 1/3，外加当前生命值的 25%、黄之钥 1、蓝之钥 1。
///
/// 数据分两份：
///   · 全局存档 = 「上一次阵亡」，开局继承，拾取后清掉（避免每局白拿一次）；
///   · 本局存档 = 本局继承到的那份，拾取后保留（楼层记忆会挡住重复刷出）。
/// </summary>
public static class AnthroposBlessing
{
    /// <summary>地图 itemPrefabs 里「祝福」的 id（复用同一套拾取物外观与楼层坐标写入）。</summary>
    private const int BlessingItemId = 20;

    /// <summary>额外发放的生命值比例（拾取时的当前生命值）。</summary>
    public const int HpPercent = 25;

    public static BlessingID Id => BlessingID.AnthroposBlessing;

    /// <summary>本局继承到的阵亡记录（null = 本局没有）。</summary>
    public static AnthroposRecord RunRecord { get; private set; }

    // ============================================================
    //  开局 / 读档
    // ============================================================

    /// <summary>
    /// 进入 Game 场景时调用：非挑战模式一律清空，挑战模式则继承全局那份「上一次阵亡」。
    /// 读档路径随后会用存档里的那份覆盖（见 RestoreRun）。
    /// </summary>
    public static void BeginRun()
    {
        RunRecord = GameSession.IsChallenge ? LoadGlobalRecord() : null;
    }

    /// <summary>读档恢复本局存档里的记录；存档里没有就回退到全局那份。</summary>
    public static void RestoreRun(AnthroposRecord record)
    {
        RunRecord = record ?? (GameSession.IsChallenge ? LoadGlobalRecord() : null);
    }

    private static AnthroposRecord LoadGlobalRecord()
        => SaveManager.LoadGlobalData()?.anthroposRecord;

    // ============================================================
    //  阵亡
    // ============================================================

    /// <summary>
    /// 挑战模式下阵亡时调用（阵亡画面打开 = 死亡已确认）：
    /// 拍下位置与 8 项数值，写进本局记录并落盘到全局存档。
    /// 非挑战模式什么都不做 —— 剧情/无尽模式不产生这条记录。
    /// </summary>
    public static void RecordDeath(PlayerData player)
    {
        if (!GameSession.IsChallenge || player == null) return;

        MapGenerator map = Object.FindFirstObjectByType<MapGenerator>();
        if (map == null || map.CurrentFloor == GameSaveData.NoFloor)
        {
            Debug.LogWarning("[AnthroposBlessing] 阵亡时拿不到当前楼层，本次阵亡不记录");
            return;
        }

        Vector2Int deathCell = map.WorldToGrid(player.transform.position);
        Vector2Int grid = ResolveSpawnCell(player, map, deathCell);
        if (grid != deathCell)
            Debug.Log($"[AnthroposBlessing] 阵亡格 ({deathCell.x},{deathCell.y}) 新一局里被占，按轨迹退到 ({grid.x},{grid.y})");

        // 读 Base*：战斗中会返回含临时加成的实时值，那不是玩家真正的面板数值
        AnthroposRecord record = new AnthroposRecord
        {
            floor = map.CurrentFloor,
            gridX = grid.x,
            gridY = grid.y,
            attack = player.BaseAttack,
            defense = player.BaseDefense,
            manaMax = player.ManaMax,
            manaCharge = player.ManaCharge,
            speed = player.Speed,
            lifeSteal = player.BaseLifeSteal,
            reflectDamage = player.BaseReflectDamage,
            damageReduction = player.BaseDamageReduction,
        };

        RunRecord = record;
        SaveManager.Instance?.SaveGlobalAnthroposRecord(record);

        Debug.Log($"[AnthroposBlessing] 记录阵亡：{record.floor}F ({record.gridX},{record.gridY}) " +
                  $"攻{record.attack} 防{record.defense} 魔力上限{record.manaMax} 充能{record.manaCharge} " +
                  $"速{record.speed} 吸血{record.lifeSteal} 反伤{record.reflectDamage} 减伤{record.damageReduction}%");
        PlayerLog.Note($"安特罗波斯：记录阵亡位置 {record.floor}F ({record.gridX},{record.gridY})");
    }

    // ============================================================
    //  生成
    // ============================================================

    /// <summary>
    /// 阵亡格若在新一局里被别的东西占着（门 / 敌人 / 道具…），沿玩家走过的路往回退到空地。
    /// 三级退让：完全空 → 只避门/楼梯/敌人/NPC → 楼梯四邻 → 都不行就留在原地。
    ///
    /// 为什么在阵亡时算：新一局的世界 ≈ 静态地图（新局 FloorMemory 清空，门全关、怪与道具全在），
    /// 所以「这一格新一局有没有东西」直接查地图数据就够，不用等生成时再看运行态；
    /// 也不用把轨迹存进记录（旧档没有轨迹就走楼梯兜底）。
    /// </summary>
    private static Vector2Int ResolveSpawnCell(PlayerData player, MapGenerator map, Vector2Int deathCell)
    {
        // 1) 沿轨迹从末尾往前：完全空的格子
        Vector2Int? cell = ScanTrail(player, deathCell, map.IsCellEmptyForSpawn);
        if (cell.HasValue) return cell.Value;

        // 2) 放宽：道具与触发器可以同格，只避门 / 楼梯 / 敌人 / NPC
        cell = ScanTrail(player, deathCell, map.IsCellFreeForSpawn);
        if (cell.HasValue) return cell.Value;

        // 3) 楼梯四邻（正常地图上楼梯必有一侧能站人）
        cell = ScanStairNeighbours(map, deathCell);
        if (cell.HasValue) return cell.Value;

        // 4) 兜底：留在阵亡格
        Debug.LogWarning($"[AnthroposBlessing] {deathCell.x},{deathCell.y} 一带找不到空位，阵亡格照用");
        return deathCell;
    }

    private static Vector2Int? ScanTrail(PlayerData player, Vector2Int deathCell,
                                         System.Func<Vector2Int, bool> isFree)
    {
        // 死点自己先看一次：本来就空就不用退
        if (isFree(deathCell)) return deathCell;

        IReadOnlyList<Vector2Int> trail = player.GetComponent<PlayerMove>()?.Trail;
        if (trail == null) return null;

        for (int i = trail.Count - 1; i >= 0; i--)
        {
            if (trail[i] == deathCell) continue;
            if (isFree(trail[i])) return trail[i];
        }
        return null;
    }

    private static Vector2Int? ScanStairNeighbours(MapGenerator map, Vector2Int deathCell)
    {
        List<Vector2Int> stairs = map.GetStairCells();
        if (stairs.Count == 0) return null;

        // 离阵亡点近的楼梯优先
        stairs.Sort((a, b) => Manhattan(a, deathCell).CompareTo(Manhattan(b, deathCell)));

        // 所有楼梯先找「完全空」，一轮都不行再放宽到「可站」
        System.Func<Vector2Int, bool>[] rules = { map.IsCellEmptyForSpawn, map.IsCellFreeForSpawn };
        Vector2Int[] directions =
        {
            new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
        };

        foreach (System.Func<Vector2Int, bool> isFree in rules)
        {
            foreach (Vector2Int stair in stairs)
            {
                foreach (Vector2Int dir in directions)
                {
                    Vector2Int neighbour = stair + dir;
                    if (isFree(neighbour)) return neighbour;
                }
            }
        }
        return null;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    /// <summary>
    /// 楼层生成完后调用：挑战模式下，若本局记录就在这一层，按记录格生成这个祝福。
    /// 该格在楼层记忆里已被拿过则跳过（本局读档回同一个存档不会重复刷）。
    /// </summary>
    public static bool TrySpawnOnFloor(MapGenerator map, MapData mapData)
    {
        if (!GameSession.IsChallenge || map == null || mapData == null) return false;

        AnthroposRecord record = RunRecord;
        if (record == null || !record.IsValid() || record.floor != mapData.floor) return false;

        Vector2Int gridPos = new Vector2Int(record.gridX, record.gridY);

        FloorState state = FloorMemoryManager.Instance?.GetState(mapData.floor);
        if (state != null && state.IsItemPickedUp(gridPos)) return false;

        BlessingData data = BlessingManager.Instance?.FindBlessing(Id);
        if (data == null)
        {
            Debug.LogWarning("[AnthroposBlessing] 默认祝福池里找不到『安特罗波斯』的祝福数据，" +
                             "本次不生成（该资产必须留在 BlessingPool 列表里，只是不参与抽取）");
            return false;
        }

        GameObject prefab = map.GetItemPrefab(BlessingItemId);
        if (prefab == null)
        {
            Debug.LogWarning($"[AnthroposBlessing] 未注册的道具 ID={BlessingItemId}（祝福），本次不生成");
            return false;
        }

        GameObject obj = map.SpawnPickupAt(prefab, gridPos, mapData.floor);
        if (obj == null) return false;

        obj.name = data.blessingName;

        BlessingPickup pickup = obj.GetComponent<BlessingPickup>();
        if (pickup == null)
        {
            Debug.LogWarning("[AnthroposBlessing] 祝福 Prefab 上没有 BlessingPickup，本次不生成");
            Object.Destroy(obj);
            return false;
        }
        pickup.SetSpecificBlessing(data);

        Debug.Log($"[AnthroposBlessing] 在 {mapData.floor}F ({gridPos.x},{gridPos.y}) 生成『{data.blessingName}』");
        PlayerLog.Note($"安特罗波斯：{mapData.floor}F ({gridPos.x},{gridPos.y}) 出现");
        return true;
    }

    // ============================================================
    //  结算
    // ============================================================

    /// <summary>
    /// 玩家拿起这个祝福时调用（经 PlayerData.ApplyBlessing）。
    /// 发放 1/3 的 8 项数值 + 当前生命值 25% + 黄之钥 1 / 蓝之钥 1。
    /// 拾取后清掉全局那份记录 —— 同一次阵亡不会在后续每一局里反复给。
    /// </summary>
    public static void Apply(PlayerData player)
    {
        if (player == null) return;

        AnthroposRecord record = RunRecord;
        if (record == null || !record.IsValid())
        {
            Debug.LogWarning("[AnthroposBlessing] 本局没有阵亡记录，祝福不发放任何数值");
            return;
        }

        AnthroposGrant grant = BuildGrant(record, player.HP);
        player.ApplyAnthroposGrant(grant);
        player.AddKey(KeyType.Yellow, 1);
        player.AddKey(KeyType.Blue, 1);

        SaveManager.Instance?.ClearGlobalAnthroposRecord();

        Debug.Log($"[AnthroposBlessing] 获得『安特罗波斯』的祝福：攻+{grant.attack} 防+{grant.defense} " +
                  $"魔力上限+{grant.manaMax} 充能+{grant.manaCharge} 速+{grant.speed} " +
                  $"吸血+{grant.lifeSteal} 反伤+{grant.reflectDamage} 减伤+{grant.damageReduction}% " +
                  $"HP+{grant.hp} 黄钥+1 蓝钥+1");
        PlayerLog.Note($"获得『安特罗波斯』的祝福（攻+{grant.attack} 防+{grant.defense} 速+{grant.speed} HP+{grant.hp}）");
    }

    /// <summary>把记录换算成实际发放量（1/3 一律向上取整，偏向玩家）。</summary>
    private static AnthroposGrant BuildGrant(AnthroposRecord record, int currentHp)
    {
        return new AnthroposGrant
        {
            attack = Third(record.attack),
            defense = Third(record.defense),
            manaMax = Third(record.manaMax),
            manaCharge = Third(record.manaCharge),
            speed = Third(record.speed),
            lifeSteal = Third(record.lifeSteal),
            reflectDamage = Third(record.reflectDamage),
            damageReduction = Third(record.damageReduction),
            hp = Mathf.CeilToInt(currentHp * HpPercent / 100f),
        };
    }

    private static int Third(int value) => Mathf.CeilToInt(value / 3f);

    // ============================================================
    //  文案
    // ============================================================

    /// <summary>
    /// 卡面描述 —— 具体数字按本局记录现算，和 Apply 用的是同一份公式。
    /// 祝福资产里的 description 只是兜底文案。
    /// </summary>
    public static string BuildDescription()
    {
        AnthroposRecord record = RunRecord;
        if (record == null || !record.IsValid())
            return "本局没有可继承的阵亡记录";

        PlayerData player = Object.FindFirstObjectByType<PlayerData>();
        AnthroposGrant grant = BuildGrant(record, player != null ? player.HP : 0);

        return $"攻击 +{grant.attack}　防御 +{grant.defense}\n" +
               $"魔力输出 +{grant.manaMax}　魔力充能 +{grant.manaCharge}\n" +
               $"速度 +{grant.speed}　吸血 +{grant.lifeSteal}\n" +
               $"反伤 +{grant.reflectDamage}　减伤 +{grant.damageReduction}%\n" +
               $"生命值 +{HpPercent}%（+{grant.hp}）　黄之钥 ×1　蓝之钥 ×1";
    }
}
