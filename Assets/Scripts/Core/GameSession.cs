/// <summary>
/// 本局游戏模式 — 由开场菜单选择，随存档持久化。
///
/// 目前只用来决定『安特罗波斯』的祝福要不要生成；
/// 挑战模式的敌人数值差异以后再做（TODO：EnemyStats 按模式取倍率）。
/// 地图与剧情模式完全一样，不要在这里分叉地图数据。
///
/// 为什么要单独存进存档：从标题进「剧情」再读一个挑战模式的档，
/// 如果用按钮上的选择就会把挑战局当成剧情局（祝福不刷）。
/// </summary>
public enum GameMode
{
    Story,
    Challenge,
    Endless,
}

/// <summary>
/// 本局模式的运行时持有者（跨场景静态）。
///
/// 三个来源：开场菜单点按钮 / 读档 / 「新游戏」时从存档推断。
/// 「新游戏」必须沿用**这一局**的模式 —— 挑战局阵亡后重开还是挑战局，
/// 不能因为运行时静态被重置（编辑器里直接在 Game 场景 Play、或从标题进设置点新游戏）
/// 就退回剧情模式。
/// </summary>
public static class GameSession
{
    /// <summary>模式变化通知（供按模式换色的 UI 订阅）。</summary>
    public static event System.Action ModeChanged;

    public static GameMode Mode { get; private set; } = GameMode.Story;

    /// <summary>本局模式是否已确定（点过按钮 / 读过档 / 已从存档推断过）。</summary>
    public static bool ModeChosen { get; private set; }

    public static bool IsChallenge => Mode == GameMode.Challenge;

    /// <summary>选定本局模式：开场菜单点按钮、读档时用。</summary>
    public static void Choose(GameMode mode)
    {
        Mode = mode;
        ModeChosen = true;
        ModeChanged?.Invoke();
    }

    /// <summary>
    /// 开局前确认模式：还没确定就从存档里记的那一局推断（都没有才回落到剧情模式）。
    /// 覆盖两条「没点过开场菜单」的入口：
    ///   · 从标题进设置点「新游戏」；
    ///   · 编辑器里直接在 Game / Setting 场景 Play。
    /// ⚠️ 必须在清自动存档**之前**调用（推断要看那份存档）。
    /// </summary>
    public static void EnsureMode()
    {
        if (ModeChosen) return;

        Mode = SaveManager.PeekSavedMode() ?? GameMode.Story;
        ModeChosen = true;
        ModeChanged?.Invoke();
    }
}
