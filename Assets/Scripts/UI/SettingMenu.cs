using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Setting（设置）场景控制器：
/// - Title（回到标题）→ 返回 Opening 场景；
/// - Exit（退出游戏）→ 退出游戏；
/// - 战斗速度按钮：互斥选择，点击后通过 OpeningButton.SetLocked 保持选中外框。
/// 按钮通过场景内的 Button 组件按名称自动匹配并绑定，无需手动拖拽引用。
/// 挂载到 Canvas 上。
/// </summary>
public class SettingMenu : MonoBehaviour
{
    [Tooltip("返回标题的目标场景名（需加入 Build Settings）")]
    [SerializeField] private string titleSceneName = "Opening";

    [Tooltip("新游戏跳转的游戏场景名（需加入 Build Settings）")]
    [SerializeField] private string gameSceneName = "Game";

    private OpeningButton[] speedButtons;

    private void Awake()
    {
        BindMenuButtons();

        Transform battleSpeedRoot = transform.Find("Panel/BattleSpeed");
        if (battleSpeedRoot != null)
        {
            BindSpeedButtons(battleSpeedRoot);
        }
        else
        {
            Debug.LogWarning("[SettingMenu] 未找到 Panel/BattleSpeed 容器");
        }

        SelectDefaultSpeed();
    }

    private void BindMenuButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            switch (button.name)
            {
                case "Title":
                    button.onClick.AddListener(BackToTitle);
                    break;

                case "NewGame":
                    button.onClick.AddListener(StartNewGame);
                    break;

                case "Exit":
                    button.onClick.AddListener(QuitGame);
                    break;

                case "Return":
                    button.onClick.AddListener(ReturnToGame);
                    break;
            }
        }
    }

    private void BindSpeedButtons(Transform battleSpeedRoot)
    {
        Button[] buttons = battleSpeedRoot.GetComponentsInChildren<Button>(true);
        speedButtons = new OpeningButton[buttons.Length];

        for (int i = 0; i < buttons.Length; i++)
        {
            Button speedButton = buttons[i];
            speedButtons[i] = speedButton.GetComponent<OpeningButton>();

            Button captured = speedButton;
            speedButton.onClick.AddListener(() => SelectSpeed(captured));
        }
    }

    private void SelectDefaultSpeed()
    {
        if (speedButtons == null || speedButtons.Length == 0) return;

        // 进入设置时，根据全局存档中的战斗速度选中对应按钮
        float saved = SaveManager.LoadBattleSpeed();

        int index = 0;
        for (int i = 0; i < speedButtons.Length; i++)
        {
            if (speedButtons[i] != null && Mathf.Approximately(GetDelayForButton(speedButtons[i]), saved))
            {
                index = i;
                break;
            }
        }

        ApplySelection(index);
    }

    private void SelectSpeed(Button clicked)
    {
        for (int i = 0; i < speedButtons.Length; i++)
        {
            if (speedButtons[i] != null && speedButtons[i].gameObject == clicked.gameObject)
            {
                ApplySelection(i);
                return;
            }
        }
    }

    private void ApplySelection(int selectedIndex)
    {
        for (int i = 0; i < speedButtons.Length; i++)
        {
            if (speedButtons[i] != null)
                speedButtons[i].SetLocked(i == selectedIndex);
        }

        ApplyBattleSpeed(selectedIndex);
    }

    /// <summary>
    /// 将选中的战斗速度同步到 BattleManager.logDelay 并写入全局存档：
    /// 正常=1、两倍=0.5、四倍=0.25、跳过=0.01。
    /// </summary>
    private void ApplyBattleSpeed(int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= speedButtons.Length) return;

        OpeningButton selected = speedButtons[selectedIndex];
        if (selected == null) return;

        float delay = GetDelayForButton(selected);

        if (BattleManager.Instance != null)
            BattleManager.Instance.LogDelay = delay;

        SaveManager.SaveBattleSpeed(delay);
    }

    /// <summary>根据按钮名称返回对应的战斗日志间隔。</summary>
    private float GetDelayForButton(OpeningButton button)
    {
        if (button == null) return 1f;

        switch (button.gameObject.name)
        {
            case "Button (1)": return 0.5f;  // 两倍
            case "Button (2)": return 0.25f; // 四倍
            case "Button (3)": return 0.01f; // 跳过
            default: return 1f;              // 正常（Button）
        }
    }

    private void BackToTitle()
    {
        // 直接返回标题，不触发开场过场动画
        ScreenFader.FadeToScene(titleSceneName);
    }

    private void ReturnToGame()
    {
        // 与按 ESC 返回相同：回到进入设置前的场景（Game 或 Opening）
        if (SettingsToggle.Instance != null)
            SettingsToggle.Instance.ExitSettingsFromButton();
    }

    private void StartNewGame()
    {
        // 清除自动存档，但不影响主动存档；随后标记本次进入为“新游戏”，跳过自动读档
        SaveManager.ClearAutoSave();
        SaveManager.LoadAutoSaveOnStart = false;

        // 新游戏：重置楼层记忆与特殊敌人击败信号（字典记忆）
        FloorMemoryManager.Instance?.ResetAll();
        SpecialEnemyManager.Instance?.ResetAll();

        // 退出设置界面时淡入淡出
        ScreenFader.FadeToScene(gameSceneName);
    }

    private void QuitGame()
    {
        // 退出游戏前自动存档
        SaveManager.Instance?.SaveAutoGame();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
