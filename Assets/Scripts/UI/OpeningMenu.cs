using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开场（Opening）菜单控制器：
/// - 剧情模式 / 挑战模式 / 无尽模式 → 跳转 "Game" 场景，并记下本局模式（GameSession）；
/// - 退出 → 退出游戏。
/// 按钮通过场景内的 Button 组件按名称自动匹配并绑定点击事件，
/// 无需在 Inspector 中手动拖拽引用。请挂载到 Canvas 上。
/// </summary>
public class OpeningMenu : MonoBehaviour
{
    [Tooltip("模式按钮统一跳转的游戏场景名（需加入 Build Settings）")]
    [SerializeField] private string gameSceneName = "Game";

    private void Awake()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0)
        {
            Debug.LogWarning("[OpeningMenu] 未找到任何 Button，请确认脚本挂在 Canvas 下");
            return;
        }

        // 读取全局道具数量，用于决定挑战/无尽模式是否可选
        GlobalSaveData globalData = SaveManager.LoadGlobalData();
        int aeonKeys = globalData != null ? globalData.aeonKeys : 0;
        int divineSpark = globalData != null ? globalData.divineSpark : 0;

        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Story":
                    button.onClick.AddListener(() => StartGame(GameMode.Story));
                    break;

                case "Challenge":
                    // 挑战模式：拥有 Aeon 钥匙（aeonKeys > 0）时才可选
                    button.interactable = aeonKeys > 0;
                    button.onClick.AddListener(() => StartGame(GameMode.Challenge));
                    break;

                case "Endless":
                    // 无尽模式：拥有神圣火花（divineSpark > 0）时才可选
                    button.interactable = divineSpark > 0;
                    button.onClick.AddListener(() => StartGame(GameMode.Endless));
                    break;

                case "Exit":
                    button.onClick.AddListener(QuitGame);
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// 进游戏。模式必须在这里设 —— 不能放在 Awake 的 switch 里，那样最后遍历到的按钮会覆盖全部。
    /// </summary>
    private void StartGame(GameMode mode)
    {
        // 有存档且模式不同 → 点这个按钮就是「我要玩这个模式」，按开新局处理
        //（清自动存档 + 重置本局静态状态，见 NewGameFlow）；
        // 有存档且模式一致 → 继续那一局。
        // 没有存档 → 没有可继续的东西（正常启动 / 通关后回到标题）→ 同样是开新局：
        //   不重置的话，上一局残留的对话标记、商店购买次数、玩家日志缓冲区会带进新局。
        if (!SaveManager.HasAutoSave() || SaveManager.PeekSavedMode() != mode)
            NewGameFlow.Start(mode);
        else
            GameSession.Choose(mode);

        // 通过 ScreenFader 完成淡出 → 加载 → 淡入的过场
        ScreenFader.FadeToScene(gameSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
