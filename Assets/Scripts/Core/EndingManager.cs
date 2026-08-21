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

        // 通过 ScreenFader 完成淡出 → 加载 → 淡入的过场
        ScreenFader.FadeToScene("Ending");
    }
}
