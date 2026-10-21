using UnityEngine;

/// <summary>
/// 一次 Zeno 镜像的快照 —— 玩家战胜他那一刻的面板（不含生命值）。
/// 字段与 PlayerData 一一对应，读出来直接用，不再乘任何系数。
/// </summary>
[System.Serializable]
public class ZenoRecord
{
    public bool valid;

    public int attack;
    public int defense;
    public int attackCount;
    public int lifeSteal;
    public int reflectDamage;
    public int damageReduction;
    public int manaCharge;
    public int manaMax;
    public int speed;

    public bool IsValid() => valid;
}

/// <summary>
/// Zeno 镜像 —— 挑战模式 30F 的 Zeno 照抄「玩家上一次战胜他时的面板」。
/// 生命值不镜像（固定为资产初始值，圣水照常减半），金币奖励也保留资产值。
///
/// 两份记录进全局存档（跨局）：
///   · 故事模式：每通关一次覆盖一次，只给**第一次**挑战模式用（开局移涌之钥 ≤ 1）；
///   · 挑战模式：每战胜一次覆盖一次，之后一直用它 —— 故事模式再通关也不影响。
/// </summary>
public static class ZenoMirror
{
    /// <summary>Zeno 的特殊敌人 ID（唯一入口是 30F 的 NPC95）。</summary>
    public const string SpecialId = "Zeno";

    private static GlobalSaveData cached;

    public static bool IsZeno(EnemyStats stats)
        => stats != null && stats.specialEnemyId == SpecialId;

    /// <summary>丢掉缓存的全局存档（开局 / 写盘后调用）。</summary>
    public static void Invalidate() => cached = null;

    private static GlobalSaveData Global
    {
        get
        {
            if (cached == null) cached = SaveManager.LoadGlobalData();
            return cached;
        }
    }

    /// <summary>本局该用的那份记录；不该镜像（非挑战模式 / 两份都无效）时返回 null。</summary>
    public static ZenoRecord Select()
    {
        if (!GameSession.IsChallenge) return null;

        GlobalSaveData g = Global;

        // 第一次挑战模式（开局钥匙 ≤ 1）用最后一次通关故事模式的那份
        bool firstChallenge = EnemyScaling.KeyCount <= 1;
        ZenoRecord primary  = firstChallenge ? g.zenoStoryRecord     : g.zenoChallengeRecord;
        ZenoRecord fallback = firstChallenge ? g.zenoChallengeRecord : g.zenoStoryRecord;

        if (primary != null && primary.IsValid()) return primary;
        return fallback != null && fallback.IsValid() ? fallback : null;
    }

    /// <summary>拍下此刻的玩家面板（战斗前真实值）。</summary>
    public static ZenoRecord Capture(PlayerData player)
    {
        if (player == null) return null;

        return new ZenoRecord
        {
            valid           = true,
            attack          = player.BaseAttack,
            defense         = player.BaseDefense,
            attackCount     = player.BaseAttackCount,
            lifeSteal       = player.BaseLifeSteal,
            reflectDamage   = player.BaseReflectDamage,
            damageReduction = player.BaseDamageReduction,
            manaCharge      = player.ManaCharge,
            manaMax         = player.ManaMax,
            speed           = player.Speed,
        };
    }

    /// <summary>写进全局存档：挑战模式覆盖挑战那份，故事模式覆盖故事那份（无尽模式不记，别污染前两份）。</summary>
    public static void Save(ZenoRecord record)
    {
        if (record == null || !record.IsValid()) return;

        GlobalSaveData g = SaveManager.LoadGlobalData();
        switch (GameSession.Mode)
        {
            case GameMode.Challenge: g.zenoChallengeRecord = record; break;
            case GameMode.Story:     g.zenoStoryRecord     = record; break;
            default: return;
        }

        if (!SaveManager.SaveGlobalData(g)) return;

        Invalidate();
        Debug.Log($"[ZenoMirror] 已记录（{GameSession.Mode}）：攻 {record.attack} / " +
                  $"防 {record.defense} / 段 {record.attackCount} / 魔 {record.manaCharge}·{record.manaMax} / " +
                  $"速 {record.speed} / 吸血 {record.lifeSteal}% / 反伤 {record.reflectDamage}% / 减伤 {record.damageReduction}%");
    }

    /// <summary>按记录填一份敌人数值；不该镜像时返回 false（调用方走正常数值）。</summary>
    public static bool TryBuild(EnemyStats stats, out EnemyScaling.Values values)
    {
        values = default;
        if (!IsZeno(stats)) return false;

        ZenoRecord r = Select();
        if (r == null) return false;

        values.hp              = stats.hp;   // 生命固定为资产初始值
        values.attack          = r.attack;
        values.defense         = r.defense;
        values.attackCount     = r.attackCount;
        values.lifeSteal       = r.lifeSteal;
        values.reflectDamage   = r.reflectDamage;
        values.damageReduction = r.damageReduction;
        values.manaCharge      = r.manaCharge;
        values.manaMax         = r.manaMax;
        values.speed           = r.speed;
        values.goldReward      = stats.goldReward;

        return true;
    }
}
