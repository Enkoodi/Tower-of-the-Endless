/// <summary>
/// 「开一局全新的」统一入口：定模式 + 清自动存档 + 清所有「本局」静态状态。
///
/// 三处调用共用这一份清单，不要各写一份 —— 漏一项就是把上一局的东西带进新局：
///   · 设置页「新游戏」  · 阵亡界面「新游戏」  · 标题页点了与存档不同的模式。
///
/// 只做状态，不跳场景：跳场景由调用方决定（Setting 用 LoadSceneInstant，阵亡界面用 FadeToScene）。
/// </summary>
public static class NewGameFlow
{
    /// <summary>按 mode 重开一局。模式由调用方先决定好（见 GameSession.EnsureMode）。</summary>
    public static void Start(GameMode mode)
    {
        GameSession.Choose(mode);

        // 不读自动存档（刚被清掉，旗标也要按住），随后重进 Game 时 MapGenerator 会加载初始楼层
        SaveManager.LoadAutoSaveOnStart = false;
        SaveManager.ClearAutoSave();

        PlayerLog.Reset();
        FloorMemoryManager.Instance?.ResetAll();
        SpecialEnemyManager.Instance?.ResetAll();
        DialogueFlags.Clear();
        NPCController.ResetPurchaseCount();
        BlessingManager.Instance?.ResetAll();
        ChallengeBonus.Reset();

        UnityEngine.Debug.Log($"[NewGameFlow] 开新局：模式 {mode}，自动存档与所有本局静态状态已重置");
    }
}
