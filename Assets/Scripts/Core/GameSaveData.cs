using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 当前游戏存档数据 — 包含玩家状态、楼层记忆等。
/// </summary>
[System.Serializable]
public class GameSaveData
{
    // ============================================================
    //  玩家战斗属性
    // ============================================================
    public int hp;
    public int attack;
    public int defense;
    public int attackCount;
    public int lifeSteal;
    public int reflectDamage;
    public int damageReduction;
    public int manaCharge;
    public int manaMax;
    public int speed;

    // ============================================================
    //  属性系数
    // ============================================================
    public int goldMultiplier;
    public int hpMultiplier;
    public int attackMultiplier;
    public int defenseMultiplier;

    // ============================================================
    //  金币与钥匙
    // ============================================================
    public int gold;
    public int yellowKeys;
    public int blueKeys;
    public int redKeys;
    public int psycheKeys;
    public int aeonKeys;

    // ============================================================
    //  传送器数量
    // ============================================================
    public int upTeleporterCount;
    public int downTeleporterCount;

    // ============================================================
    //  敌人减半道具
    // ============================================================
    public int enemyHalveItemCount;

    // 圣水是否已待生效（只作用于下一场，不累计场次）
    public bool pendingEnemyHalve;

    // ============================================================
    //  麦酒 / 魔力精华
    // ============================================================
    public int aleCount;
    public int manaEssenceCount;

    // 已喝下、但还没兑现到战斗里的部分（下一场战斗生效）
    public int pendingAleStacks;
    public bool pendingManaEssence;

    // ============================================================
    //  装备效果（神圣剑 / 神圣盾 / 利维坦之髓）
    //
    //  这三样是运行时 AddComponent 挂到玩家身上的组件，**不属于 PlayerData 字段**，
    //  所以必须单独存档 —— 否则读档后装备会静默消失：
    //  楼层记忆里道具已标记「已拾取」，地图上不再刷出，效果也没了，等于白捡。
    // ============================================================
    public bool hasHolySword;
    public int holySwordMultiplierPercent = 200;
    public bool hasHolyShield;
    public bool hasLeviathansMarrow;
    public int leviathansMarrowInterval = 5;
    public bool hasBehemothsFang;
    public int fangStacksPerAttack = 1;
    public int fangMaxStacks = 10;
    public int fangPercentPerStack = 1;
    public bool hasOuroborosCrucifix;
    public int ouroborosPercentOfHigher = 50;

    // ============================================================
    //  特殊祝福效果（BlessingID → 层数）
    // ============================================================
    public Dictionary<string, int> specialBlessings = new Dictionary<string, int>();

    // ============================================================
    //  本局已获得的祝福记录（BlessingID → 获得次数）
    //  覆盖 DirectBonus 与 Conditional 两类；含商店购买的重复获得。
    //  与 specialBlessings 的区别：那份是「当前生效的特殊祝福状态」，
    //  这份是「获得清单」，读档后不会因为祝福叠加而丢失历史。
    // ============================================================
    public Dictionary<string, int> obtainedBlessings = new Dictionary<string, int>();

    // ============================================================
    //  特殊祝福的持久运行时状态（BlessingID → 状态值）
    //
    //  存「跨战斗累计」的计数，目前只有卡巴拉生命之树（value = 剩余触发次数）。
    //  这类状态不属于 Level，也不能每场战斗重置 —— 不单独存就会「读档/打完仗后
    //  次数回满 → 反复触发」。纯战斗内状态（回合计数等）不写这里。
    //  旧存档没有这个键 → 效果保持「未初始化」语义（回退到 Level）。
    // ============================================================
    public Dictionary<string, int> blessingStates = new Dictionary<string, int>();

    // ============================================================
    //  商店购买次数（所有商店共通的全局计数，决定下一次购买价格）
    // ============================================================
    public int shopPurchaseCount;

    // ============================================================
    //  特殊敌人击败信号（specialEnemyId 列表）
    // ============================================================
    public List<string> defeatedSpecialEnemies = new List<string>();

    // ============================================================
    //  对话标记（DialogueTrigger.endFlagId，记录收尾逻辑已结算过）
    // ============================================================
    public List<string> dialogueFlags = new List<string>();

    // ============================================================
    //  楼层状态（序列化格式）
    // ============================================================
    public List<FloorStateEntry> floorStates = new List<FloorStateEntry>();

    // ============================================================
    //  已访问楼层（用于快速跳层功能）
    // ============================================================
    public List<int> visitedFloors = new List<int>();

    // ============================================================
    //  玩家位置
    // ============================================================
    public float playerX;
    public float playerY;
    public float playerZ;

    // ============================================================
    //  当前楼层
    //
    //  ⚠️ 哨兵值不能用 -1：地下负层（floor_-01 ~ floor_-07）是**合法的真实楼层编号**，
    //  与 -1 撞车会导致「在负层读档时被判为无楼层 → 整张地图不生成、只剩 Unity 默认蓝底」。
    //  全工程统一用 NoFloor 表示「当前没有加载任何楼层」。
    // ============================================================
    public int currentFloor = NoFloor;

    /// <summary>「当前未加载任何楼层」的哨兵值。不能用 -1（-1 是真实的第 -1 层）。</summary>
    public const int NoFloor = int.MinValue;
}

/// <summary>
/// 单个楼层的存档条目，用于 JSON 序列化。
/// </summary>
[System.Serializable]
public class FloorStateEntry
{
    public int floorNumber;
    public List<string> defeatedEnemies = new List<string>();
    public List<string> pickedUpItems = new List<string>();
    public List<string> openedDoors = new List<string>();
    public List<string> openedBattleDoors = new List<string>();
    public List<string> activeDropItems = new List<string>();
    public List<string> pinceredEnemies = new List<string>(); // 格式: "x,y,剩余HP"
    public List<string> removedWalls = new List<string>(); // 格式: "x,y"
    public List<string> removedNpcs = new List<string>(); // 格式: "x,y"

    /// <summary>将 Vector2Int 序列化为 "x,y" 字符串</summary>
    public static string PosToString(Vector2Int pos) => $"{pos.x},{pos.y}";

    /// <summary>从 "x,y" 字符串反序列化为 Vector2Int</summary>
    public static Vector2Int StringToPos(string s)
    {
        string[] parts = s.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
            return new Vector2Int(x, y);
        return Vector2Int.zero;
    }
}
