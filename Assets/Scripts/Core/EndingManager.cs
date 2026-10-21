using UnityEngine;

/// <summary>
/// 片尾（ED）触发器 — 提供触发游戏真结局片尾的统一入口。
/// 片尾通过加载 "Ending" 场景播放，请将该场景加入 Build Settings。
/// </summary>
public static class EndingManager
{
    /// <summary>触发真结局片尾 ED。</summary>
    public static void Trigger()
    {
        Debug.Log("[EndingManager] 触发真结局片尾 ED");
        PlayerLog.Note("触发真结局");

        // 通关后不再有「继续游戏」：清掉自动存档，回到标题点任一模式都是开新局。
        // 不清的话标题页会读回通关前那份档，玩家能从 30F 接着走。
        SaveManager.ClearAutoSave();

        // 通过 ScreenFader 完成淡出 → 加载 → 淡入的过场
        ScreenFader.FadeToScene("Ending");
    }
}
