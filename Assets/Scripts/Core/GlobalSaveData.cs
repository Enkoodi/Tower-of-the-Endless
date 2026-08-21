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

    // TODO: 是否通关故事模式
    // TODO: 游戏音量设置
    // TODO: 其他全局道具
}
