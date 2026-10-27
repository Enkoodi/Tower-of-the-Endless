/// <summary>
/// 全局存档数据 — 跨存档的持久化数据。
/// 包括 Aeon 钥匙、通关状态、音量设置等全局信息。
/// </summary>
[System.Serializable]
public class GlobalSaveData
{
    /// <summary>Aeon 钥匙数量（全局道具，跨存档保留）</summary>
    public int aeonKeys = 0;

    /// <summary>神圣火花数量（全局道具，跨存档保留，初始为 0）</summary>
    public int divineSpark = 0;

    /// <summary>战斗速度（全局设置，跨存档保留）。对应 BattleManager.logDelay：1=正常，0.5=两倍，0.25=四倍，0.01=跳过。</summary>
    public float battleSpeed = 1f;

    /// <summary>
    /// 上一次挑战模式阵亡的位置与数值（『安特罗波斯』的祝福据此生成）。
    /// 开局继承到本局存档，祝福被拾取后清除 —— 同一次阵亡不会每局白给一次。
    /// </summary>
    public AnthroposRecord anthroposRecord;

    /// <summary>
    /// 最近一局的模式（GameMode 的整数值）。只作为「新游戏」推断模式的兜底 ——
    /// 自动/手动存档被清掉（新游戏）、或从标题进设置重开时，靠它记住上一次玩的是哪种模式。
    /// </summary>
    public int gameMode;

    /// <summary>最近一次通关故事模式（战胜 Zeno 前）的玩家面板，每通关一次覆盖。</summary>
    public ZenoRecord zenoStoryRecord;

    /// <summary>最近一次在挑战模式战胜 Zeno 时的玩家面板，每胜一次覆盖。</summary>
    public ZenoRecord zenoChallengeRecord;

    /// <summary>
    /// 开场过场已经播过的阶段：0 = 一次没播，1 = 故事首次通关那次播过了，2 = 两次都播完了。
    /// 一辈子只播两次（见 TransitionController），钥匙/火花都是数量，光靠它们判不出"第几次"。
    /// </summary>
    public int openingIntroStage = 0;

    // TODO: 是否通关故事模式
    // TODO: 游戏音量设置
    // TODO: 其他全局道具
}
