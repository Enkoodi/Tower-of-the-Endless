using UnityEngine;

/// <summary>
/// 挑战模式的敌人倍率 —— 移涌之钥越多，敌人越强。
///
/// n = 开局的移涌之钥数量：
///   生命 / 攻击                     × min(100 + 20n, 260)%
///   防御 / 金币 / 速度               × min(100 + 10n, 180)%
///   吸血 / 反伤 / 魔力上限 / 充能    × min(100 + 5n,  140)%
///   段数 / 减伤                     不变
///
/// 倍率在开局（MapGenerator.Start）定格，整局不变 —— 钥匙在局内会被门与祝福改动，
/// 实时读会让同一局的敌人数值忽强忽弱，读档后也会与上一帧不一致。
///
/// 取整：一律向下截断（敌人变强，舍尾偏向玩家）；金币除外，向上（那是玩家收益）。
/// </summary>
public static class EnemyScaling
{
    private const int HpAttackStep = 20;   // 每把钥匙 +20%
    private const int HpAttackCap  = 260;
    private const int GuardStep    = 10;   // 防御 / 金币 / 速度：每把 +10%
    private const int GuardCap     = 180;
    private const int MiscStep     = 5;    // 吸血 / 反伤 / 魔力：每把 +5%
    private const int MiscCap      = 140;

    /// <summary>本局开局时的移涌之钥数量。</summary>
    public static int KeyCount { get; private set; }

    public static int HpAttackPercent { get; private set; } = 100;
    public static int GuardPercent    { get; private set; } = 100;
    public static int MiscPercent     { get; private set; } = 100;

    /// <summary>缩放后的完整数值 —— 实战与图鉴共用这一份。</summary>
    public struct Values
    {
        public int hp, attack, defense, attackCount, lifeSteal, reflectDamage,
                   damageReduction, manaCharge, manaMax, speed, goldReward;
    }

    /// <summary>
    /// 开局算一次倍率。必须在 <see cref="GameSession.EnsureMode"/> 之后调用。
    /// 读档路径随后会用存档里「本局开局时的钥匙数」覆盖（见 <see cref="RestoreRun"/>）。
    /// </summary>
    public static void BeginRun()
    {
        // 上一局可能刚写过一份新的 Zeno 镜像，缓存要作废
        ZenoMirror.Invalidate();

        int keys = 0;
        if (GameSession.IsChallenge)
        {
            GlobalSaveData global = SaveManager.LoadGlobalData();
            keys = Mathf.Max(0, global != null ? global.aeonKeys : 0);
        }
        ApplyKeyCount(keys);
        Log("开局");
    }

    /// <summary>
    /// 读档时用存档里记的钥匙数定格倍率 —— 必须和存档那一刻一致。
    /// 钥匙来自 boss 掉落物，捡起后会被写进全局存档；重进场景时全局值已经变了，
    /// 不按存档恢复的话，同一局读档会平白变难（或变简单）。
    /// </summary>
    /// <param name="savedKeyCount">存档里记的开局钥匙数；旧存档没有这个字段，传负数表示未知。</param>
    public static void RestoreRun(int savedKeyCount)
    {
        if (savedKeyCount < 0) return;

        int keys = GameSession.IsChallenge ? savedKeyCount : 0;
        if (keys == KeyCount) return;   // 与开局算出来的一致，不重复打日志

        ApplyKeyCount(keys);
        Log("读档恢复");
    }

    private static void ApplyKeyCount(int keys)
    {
        KeyCount = keys;
        HpAttackPercent = ScaledPercent(keys, HpAttackStep, HpAttackCap);
        GuardPercent    = ScaledPercent(keys, GuardStep, GuardCap);
        MiscPercent     = ScaledPercent(keys, MiscStep, MiscCap);
    }

    private static void Log(string when)
    {
        if (KeyCount <= 0) return;
        Debug.Log($"[EnemyScaling] 挑战模式（{when}）：移涌之钥 {KeyCount} 把 → " +
                  $"生命/攻击 {HpAttackPercent}%，防御/金币/速度 {GuardPercent}%，" +
                  $"吸血/反伤/魔力 {MiscPercent}%");
    }

    private static int ScaledPercent(int keys, int step, int cap)
        => keys > 0 ? Mathf.Min(100 + step * keys, cap) : 100;

    /// <summary>
    /// 把资产上的基础数值换算成本局实际数值。
    /// </summary>
    /// <param name="exempt">
    /// true = 不参与挑战倍率。目前用于**可战斗 NPC**（<c>EnemyController.isScriptedEnemy</c>）——
    /// 它们以后走单独的一套规则，现在先保持原始数值。
    /// </param>
    public static Values Apply(EnemyStats e, bool exempt = false)
    {
        Values v = default;
        if (e == null) return v;

        // 挑战模式的 Zeno：整套照抄「上一次战胜他时的玩家面板」（生命除外），
        // 优先于下面任何倍率 —— 他的数值本来就不该由钥匙数决定。
        if (ZenoMirror.TryBuild(e, out Values mirror)) return mirror;

        int hpAttackPct  = exempt ? 100 : HpAttackPercent;
        int guardPct     = exempt ? 100 : GuardPercent;
        int miscPct      = exempt ? 100 : MiscPercent;

        v.hp              = e.hp <= 0 ? e.hp : Mathf.Max(1, Scale(e.hp, hpAttackPct));
        v.attack          = Scale(e.attack, hpAttackPct);
        v.defense         = Scale(e.defense, guardPct);
        v.goldReward      = ScaleUp(e.goldReward, guardPct);
        v.speed           = Scale(e.speed, guardPct);
        v.lifeSteal       = Scale(e.lifeSteal, miscPct);
        v.reflectDamage   = Scale(e.reflectDamage, miscPct);
        v.manaCharge      = Scale(e.manaCharge, miscPct);
        v.manaMax         = Scale(e.manaMax, miscPct);
        v.attackCount     = e.attackCount;
        v.damageReduction = e.damageReduction;
        return v;
    }

    /// <summary>向下取整。0 与负数原样保留（没这项能力的敌人不该被乘出 1）。</summary>
    private static int Scale(int value, int percent)
        => value <= 0 ? value : value * percent / 100;

    /// <summary>向上取整 —— 只给金币用（玩家收益）。</summary>
    private static int ScaleUp(int value, int percent)
        => value <= 0 ? value : (value * percent + 99) / 100;
}
