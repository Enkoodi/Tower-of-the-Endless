using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按本局模式给 DataCanvas 换底色：故事模式 #1E2832，挑战模式 #641E1E。
/// 无尽模式暂用故事模式配色（等它有专属配色再加）。
///
/// 颜色必须在**模式确定之后**再刷一次：读档时模式是 MapGenerator.Start 里才定下来的，
/// 比这里的 Awake 晚，所以除了 Awake 现刷一次，还要订阅 GameSession.ModeChanged。
/// </summary>
public class GameModeTheme : MonoBehaviour
{
    [Header("目标面板")]
    [SerializeField] private Image leftPanel;
    [SerializeField] private Image rightPanel;

    [Header("配色")]
    [SerializeField] private Color storyColor = new Color32(0x1E, 0x28, 0x32, 0xFF);
    [SerializeField] private Color challengeColor = new Color32(0x64, 0x1E, 0x1E, 0xFF);

    void Awake()
    {
        // 引用没拖时按节点名兜底（与 PlayerHUD 同一套命名）
        if (leftPanel == null) leftPanel = FindPanel("LeftPanel");
        if (rightPanel == null) rightPanel = FindPanel("RightPanel");

        Apply();
        GameSession.ModeChanged += Apply;
    }

    void OnDestroy()
    {
        GameSession.ModeChanged -= Apply;
    }

    /// <summary>按当前模式重刷两个面板的颜色。</summary>
    public void Apply()
    {
        Color color = GameSession.IsChallenge ? challengeColor : storyColor;

        if (leftPanel != null) leftPanel.color = color;
        if (rightPanel != null) rightPanel.color = color;
    }

    private Image FindPanel(string nodeName)
    {
        Transform target = transform.Find(nodeName);
        return target != null ? target.GetComponent<Image>() : null;
    }
}
