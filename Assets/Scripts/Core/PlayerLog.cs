using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>玩家日志的槽位 —— 与两份存档一一对应。</summary>
public enum PlayerLogSlot
{
    /// <summary>主动存档：game_save.json ↔ game_save_log.txt</summary>
    Manual,

    /// <summary>自动存档：auto_save.json ↔ auto_save_log.txt</summary>
    Auto,
}

/// <summary>
/// 玩家日志 —— 记录关键操作，用于复现一局的完整过程。
///
/// 三条规则（互相自洽，不要单独改其中一条）：
///   1. 操作只写内存缓冲区 lines，不碰磁盘；
///   2. 只有存档真的写盘成功，才把 lines **整份覆盖**到对应槽位的日志文件（不是追加）；
///   3. 读档时反向操作 —— 用该槽位日志文件的内容**整份回填** lines。
///
/// 于是「没存档就退出 → 缓冲区随进程消失 → 文件保持原样」自动成立，不需要任何索引或游标。
/// 也正因为规则 2 是整份覆盖，读档回退后那些"没存档的操作"不会被留在文件里。
///
/// 文件位置：{项目根}/save/game_save_log.txt、save/auto_save_log.txt
/// 编码：UTF-8 带 BOM 明文，全文无转义，记事本双击直读。
/// </summary>
public static class PlayerLog
{
    /// <summary>缓冲区硬上限，防极端情况下内存无限增长。</summary>
    private const int MaxLines = 20000;

    private static readonly List<string> lines = new List<string>();
    private static readonly HashSet<string> seenOnce = new HashSet<string>();
    private static string saveDir;

    private static string Now => DateTime.Now.ToString("HH:mm:ss");

    // ============================================================
    //  写入缓冲区
    // ============================================================

    /// <summary>记一条正文。</summary>
    public static void Note(string text)
    {
        if (!string.IsNullOrEmpty(text)) Append(text);
    }

    /// <summary>记一个段标题（进入楼层等）：前面空一行，行尾带时间戳。</summary>
    public static void Section(string title)
    {
        if (string.IsNullOrEmpty(title)) return;
        if (lines.Count > 0) Append("");
        Append($"—— {title} ——  {Now}");
    }

    /// <summary>同一个 key 只记一次（用于反复对话等防刷屏）。Reset 时清空。</summary>
    public static bool FirstTime(string key)
    {
        if (string.IsNullOrEmpty(key)) return true;
        return seenOnce.Add(key);
    }

    private static void Append(string line)
    {
        if (lines.Count >= MaxLines) return;
        lines.Add(line);
    }

    // ============================================================
    //  存档 / 读档 / 新游戏
    // ============================================================

    /// <summary>存档写盘成功后调用：追加存档点标记，再把整个缓冲区覆盖到该槽位的日志文件。</summary>
    public static void Commit(PlayerLogSlot slot)
    {
        Append($"── {SlotName(slot)} ──  {Now}");
        Write(slot);
    }

    /// <summary>读档成功后调用：用该槽位日志文件的内容整份回填缓冲区（世界退回，日志跟着退回）。</summary>
    public static void Adopt(PlayerLogSlot slot)
    {
        lines.Clear();
        seenOnce.Clear();

        string path = PathOf(slot);
        if (File.Exists(path))
        {
            try
            {
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.TrimEnd();
                    if (line.StartsWith("#")) continue;   // 文件头
                    lines.Add(line);
                }

                while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                    lines.RemoveAt(lines.Count - 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerLog] 读取日志失败：{path}\n{e}");
            }
        }

        Append($"── 读档·{SlotName(slot)} ──  {Now}");
    }

    /// <summary>新游戏：清空缓冲区与去重表。对应槽位的日志文件由 Delete 单独处理。</summary>
    public static void Reset()
    {
        lines.Clear();
        seenOnce.Clear();
    }

    /// <summary>删除某个槽位的日志文件（清除自动存档时同步调用）。</summary>
    public static void Delete(PlayerLogSlot slot)
    {
        try
        {
            string path = PathOf(slot);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerLog] 删除日志失败：{PathOf(slot)}\n{e}");
        }
    }

    // ============================================================
    //  文案工具 —— 同一件东西在日志里必须叫同一个名字
    // ============================================================

    public static string KeyName(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow: return "黄钥匙";
            case KeyType.Blue:   return "蓝钥匙";
            case KeyType.Red:    return "红钥匙";
            case KeyType.Psyche: return "魂钥匙";
            case KeyType.Aeon:   return "移涌之钥";
            default:             return keyType.ToString();
        }
    }

    // ============================================================
    //  落盘
    // ============================================================

    private static void Write(PlayerLogSlot slot)
    {
        try
        {
            string dir = Dir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(PathOf(slot), BuildHeader(slot) + RenderBody(), new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerLog] 写入日志失败：{PathOf(slot)}\n{e}");
        }
    }

    // ============================================================
    //  落盘前的裁剪
    // ============================================================

    private static bool IsSectionLine(string s) => s.StartsWith("—— ");

    private static bool IsMarkLine(string s) => !IsSectionLine(s) && s.StartsWith("── ");

    private static bool IsContentLine(string s) => !IsSectionLine(s) && !IsMarkLine(s);

    /// <summary>把缓冲区渲染成正文：先裁掉空段，再按「段标题前空一行」排版。</summary>
    private static string RenderBody()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string line in Prune())
        {
            if (IsSectionLine(line) && sb.Length > 0) sb.Append(Environment.NewLine);
            sb.Append(line).Append(Environment.NewLine);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 落盘前裁剪：把「一次实际操作都没有的换层」整段去掉，顺带去掉因此变孤立的存档点标记。
    ///
    /// 规则（顺序不能换）：
    ///   1. 连着的多个标记只留最后一个 —— 连按两次存档时前一次没有信息量；
    ///   2. 标记前面（同一段里）没有内容行 → 它是孤立的，删；
    ///   3. 段标题到下一个段标题之间一条内容行都没有 → 整段删。
    ///
    /// 只作用于写出去的文件，缓冲区保持全量；裁剪是幂等的，所以反复落盘结果一致。
    /// </summary>
    private static List<string> Prune()
    {
        List<string> records = new List<string>();
        foreach (string s in lines)
            if (s.Length > 0) records.Add(s);

        // 1) 连着的多个标记只留最后一个
        List<string> step1 = new List<string>();
        for (int i = 0; i < records.Count; i++)
        {
            if (IsMarkLine(records[i]) && i + 1 < records.Count && IsMarkLine(records[i + 1]))
                continue;
            step1.Add(records[i]);
        }

        // 2) 标记前面没有内容行 → 孤立 → 删
        List<string> step2 = new List<string>();
        foreach (string s in step1)
        {
            if (IsMarkLine(s) && !(step2.Count > 0 && IsContentLine(step2[step2.Count - 1])))
                continue;
            step2.Add(s);
        }

        // 3) 空段标题整段删
        List<string> result = new List<string>();
        for (int i = 0; i < step2.Count; i++)
        {
            if (!IsSectionLine(step2[i]))
            {
                result.Add(step2[i]);
                continue;
            }

            bool hasContent = false;
            for (int j = i + 1; j < step2.Count; j++)
            {
                if (IsSectionLine(step2[j])) break;
                if (IsContentLine(step2[j])) { hasContent = true; break; }
            }

            if (hasContent) result.Add(step2[i]);
        }

        return result;
    }

    /// <summary>文件头：每次落盘重建，记录「这份存档是什么状态」。</summary>
    private static string BuildHeader(PlayerLogSlot slot)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"# 玩家日志 · {SlotName(slot)}");
        sb.AppendLine($"# 写入 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        PlayerData player = UnityEngine.Object.FindAnyObjectByType<PlayerData>();
        if (player != null)
        {
            sb.AppendLine($"# 位置：{FloorText()} · HP {player.HP} · 金币 {player.Gold}");
            sb.AppendLine($"# 属性：攻 {player.BaseAttack}(×{player.AttackMultiplier}%) 防 {player.BaseDefense}(×{player.DefenseMultiplier}%)"
                        + $" 段 {player.BaseAttackCount} 减伤 {player.BaseDamageReduction}%"
                        + $" 吸血 {player.BaseLifeSteal}% 反伤 {player.BaseReflectDamage}");
            sb.AppendLine($"# 数值：魔力 {player.ManaCharge}/{player.ManaMax} 移速 {player.Speed}"
                        + $" 金币倍率 {player.GoldMultiplier}% HP倍率 {player.HPMultiplier}%");
            sb.AppendLine($"# 钥匙：黄{player.GetKeyCount(KeyType.Yellow)} 蓝{player.GetKeyCount(KeyType.Blue)}"
                        + $" 红{player.GetKeyCount(KeyType.Red)} 魂{player.GetKeyCount(KeyType.Psyche)}"
                        + $" 移涌{player.GetKeyCount(KeyType.Aeon)}");
            sb.AppendLine($"# 道具：传送↑{player.UpTeleporterCount} ↓{player.DownTeleporterCount}"
                        + $" 圣水{player.EnemyHalveItemCount} 麦酒{player.AleCount}"
                        + $" 魔力精华{player.ManaEssenceCount} 神圣火花{player.DivineSpark}");
            sb.AppendLine($"# 装备：{EquipText(player)}");
        }

        sb.AppendLine("# ---------- 以下为记录 ----------");
        return sb.ToString();
    }

    private static string EquipText(PlayerData player)
    {
        List<string> parts = new List<string>();

        HolySwordEffect sword = player.GetComponent<HolySwordEffect>();
        if (sword != null) parts.Add($"神圣剑(魔力×{sword.MultiplierPercent}%)");

        if (player.GetComponent<HolyShieldEffect>() != null) parts.Add("神圣盾");

        LeviathansMarrowEffect marrow = player.GetComponent<LeviathansMarrowEffect>();
        if (marrow != null) parts.Add($"利维坦之髓(每{marrow.FreezeInterval}回合冻结)");

        BehemothsFangEffect fang = player.GetComponent<BehemothsFangEffect>();
        if (fang != null) parts.Add($"贝希摩斯之牙({fang.StacksPerAttack}层/攻 上限{fang.MaxStacks})");

        OuroborosCrucifixEffect ouroboros = player.GetComponent<OuroborosCrucifixEffect>();
        if (ouroboros != null) parts.Add($"衔尾之十字(补{ouroboros.PercentOfHigher}%)");

        return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : "无";
    }

    private static string FloorText()
    {
        MapGenerator mapGen = UnityEngine.Object.FindAnyObjectByType<MapGenerator>();
        int floor = mapGen != null ? mapGen.CurrentFloor : GameSaveData.NoFloor;
        return floor == GameSaveData.NoFloor ? "未加载楼层" : $"{floor}F";
    }

    // ============================================================
    //  路径
    // ============================================================

    private static string Dir
    {
        get
        {
            if (saveDir == null)
                saveDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "save");
            return saveDir;
        }
    }

    private static string PathOf(PlayerLogSlot slot)
    {
        return Path.Combine(Dir, slot == PlayerLogSlot.Manual ? "game_save_log.txt" : "auto_save_log.txt");
    }

    private static string SlotName(PlayerLogSlot slot)
    {
        return slot == PlayerLogSlot.Manual ? "主动存档" : "自动存档";
    }
}
