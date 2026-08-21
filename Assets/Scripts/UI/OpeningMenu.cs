using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开场（Opening）菜单控制器：
/// - 剧情模式 / 挑战模式 / 无尽模式 → 跳转 "Game" 场景（模式差异后续再实现）；
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

        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Story":
                case "Challenge":
                case "Endless":
                    button.onClick.AddListener(StartGame);
                    break;

                case "Exit":
                    button.onClick.AddListener(QuitGame);
                    break;

                default:
                    break;
            }
        }
    }

    private void StartGame()
    {
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
