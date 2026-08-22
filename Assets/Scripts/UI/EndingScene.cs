using UnityEngine;

/// <summary>
/// Ending（片尾）场景控制器：
/// 玩家点击屏幕任意处（鼠标左键 / 触摸）后跳转到 "Opening" 场景。
/// </summary>
public class EndingScene : MonoBehaviour
{
    [Tooltip("要跳转的目标场景名（需加入 Build Settings）")]
    [SerializeField] private string nextSceneName = "Opening";

    [Tooltip("点击后延迟跳转的秒数")]
    [SerializeField] private float delaySeconds = 0f;

    private bool isLoading;

    private void Update()
    {
        if (isLoading) return;

        // 点击任意处（含触摸）即触发跳转
        if (Input.GetMouseButtonDown(0))
        {
            isLoading = true;

            if (delaySeconds > 0f)
                Invoke(nameof(LoadNextScene), delaySeconds);
            else
                LoadNextScene();
        }
    }

    private void LoadNextScene()
    {
        // 从其他场景返回 Opening 时，标记播放开场过场动画
        TransitionController.ShouldPlayOnLoad = true;

        // 通过 ScreenFader 完成淡出 → 加载 → 淡入的过场
        ScreenFader.FadeToScene(nextSceneName);
    }
}
