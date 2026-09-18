using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Setting（设置）场景控制器：
/// - NewGame（新游戏）→ 清存档并进入 Game；
/// - Return（返回游戏）→ 回到进入设置前的场景（等同按 ESC）；
/// - Title（回到标题）→ 返回 Opening 场景；
/// - Exit（退出游戏）→ 退出游戏；
/// - 战斗速度按钮：互斥选择，点击后通过 OpeningButton.SetLocked 保持选中外框。
///
/// 绑定方式：Inspector 拖引用（[SerializeField]）+ Awake 里代码 AddListener。
/// 引用为空时按物体名兜底查找一次（见 AutoBind），兜底只影响运行时，不会写回 Inspector。
/// 速度档位读数来自 OpeningButton.speedDelay，不再依赖按钮名字。
/// 挂在 **Panel** 上（最小能包含全部按钮的节点），不要挂 Canvas。
/// </summary>
public class SettingMenu : MonoBehaviour
{
    [Header("目标场景")]
    [Tooltip("返回标题的目标场景名（需加入 Build Settings）")]
    [SerializeField] private string titleSceneName = "Opening";

    [Tooltip("新游戏跳转的游戏场景名（需加入 Build Settings）")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("导航按钮")]
    [Tooltip("新游戏")]
    [SerializeField] private Button newGameButton;

    [Tooltip("返回游戏（回到进入设置前的场景，等同按 ESC）")]
    [SerializeField] private Button returnButton;

    [Tooltip("回到标题")]
    [SerializeField] private Button titleButton;

    [Tooltip("退出游戏")]
    [SerializeField] private Button exitButton;

    [Header("战斗速度")]
    [Tooltip("速度档按钮容器（其下带 OpeningButton 且填了 speedDelay 的按钮即为速度档）")]
    [SerializeField] private Transform battleSpeedRoot;

    [Tooltip("速度档按钮。留空则运行时从 battleSpeedRoot 下自动收集。")]
    [SerializeField] private OpeningButton[] speedButtons;

    private void Awake()
    {
        AutoBind();

        BindNavigationButtons();
        CollectSpeedButtons();
        SelectSavedSpeed();
    }

    // ============================================================
    //  绑定：拖引用优先，为空时按名字兜底查找
    // ============================================================

    /// <summary>
    /// 补齐 Inspector 中未拖的引用（按物体名查找）。
    /// 只在引用为空时生效，属于兜底；正式引用请在 Inspector 里拖。
    /// </summary>
    private void AutoBind()
    {
        if (battleSpeedRoot == null)
        {
            // 本组件挂在 Panel 上，BattleSpeed 是它的直接子物体
            Transform found = transform.Find("BattleSpeed");
            if (found != null) battleSpeedRoot = found;
        }

        if (newGameButton == null) newGameButton = FindButton("NewGame");
        if (returnButton == null) returnButton = FindButton("Return");
        if (titleButton == null) titleButton = FindButton("Title");
        if (exitButton == null) exitButton = FindButton("Exit");
    }

    private Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            if (b.name == objectName) return b;
        }

        Debug.LogWarning($"[SettingMenu] 未找到名为「{objectName}」的按钮，" +
                         "请在 Inspector 中手动拖引用。");
        return null;
    }

    private void BindNavigationButtons()
    {
        if (newGameButton != null) newGameButton.onClick.AddListener(StartNewGame);
        if (returnButton != null) returnButton.onClick.AddListener(ReturnToGame);
        if (titleButton != null) titleButton.onClick.AddListener(BackToTitle);
        if (exitButton != null) exitButton.onClick.AddListener(QuitGame);
    }

    // ============================================================
    //  战斗速度
    // ============================================================

    /// <summary>收集速度档按钮：优先用 Inspector 拖好的数组，为空则从容器下自动收集。</summary>
    private void CollectSpeedButtons()
    {
        if (speedButtons == null || speedButtons.Length == 0)
        {
            CollectSpeedButtonsFromRoot();
        }
        else
        {
            // Inspector 里手拖的数组也按 speedDelay 降序排一次，
            // 避免顺序与档位不一致导致选中框跳到错误的按钮上。
            SortByDelayDescending();
        }

        BindSpeedButtons();
    }

    private void SortByDelayDescending()
    {
        System.Array.Sort(speedButtons, (a, b) =>
        {
            float da = a != null ? a.SpeedDelay : float.MinValue;
            float db = b != null ? b.SpeedDelay : float.MinValue;
            return db.CompareTo(da);
        });
    }

    private void CollectSpeedButtonsFromRoot()
    {
        if (battleSpeedRoot == null)
        {
            Debug.LogWarning("[SettingMenu] 未指定战斗速度容器（battleSpeedRoot），速度档不可用。");
            return;
        }

        OpeningButton[] all = battleSpeedRoot.GetComponentsInChildren<OpeningButton>(true);
        System.Collections.Generic.List<OpeningButton> options =
            new System.Collections.Generic.List<OpeningButton>();

        foreach (OpeningButton ob in all)
        {
            if (ob != null && ob.IsSpeedOption) options.Add(ob);
        }

        // 按 speedDelay 从大到小排序：正常(1) → 两倍(0.5) → 四倍(0.25) → 跳过(0.01)
        options.Sort((a, b) => b.SpeedDelay.CompareTo(a.SpeedDelay));
        speedButtons = options.ToArray();

        if (speedButtons.Length == 0)
        {
            Debug.LogWarning("[SettingMenu] BattleSpeed 下没有配置 speedDelay 的速度按钮，" +
                             "请在各按钮的 OpeningButton 组件里填写 speedDelay。");
        }
    }

    private void BindSpeedButtons()
    {
        if (speedButtons == null) return;

        for (int i = 0; i < speedButtons.Length; i++)
        {
            if (speedButtons[i] == null) continue;

            int index = i;   // 必须先拷贝：for 循环变量是被所有闭包共享的
            Button b = speedButtons[i].GetComponent<Button>();
            if (b != null) b.onClick.AddListener(() => SelectSpeed(index));
        }
    }

    /// <summary>读取存档中的战斗速度，选中对应档位。</summary>
    private void SelectSavedSpeed()
    {
        if (speedButtons == null || speedButtons.Length == 0) return;

        float saved = SaveManager.LoadBattleSpeed();
        ApplySelection(FindClosestSpeedIndex(saved));
    }

    /// <summary>找出与存档值最接近的档位索引。</summary>
    private int FindClosestSpeedIndex(float saved)
    {
        int best = 0;
        float bestDelta = float.MaxValue;

        for (int i = 0; i < speedButtons.Length; i++)
        {
            if (speedButtons[i] == null) continue;

            float delta = Mathf.Abs(speedButtons[i].SpeedDelay - saved);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }

        return best;
    }

    private void SelectSpeed(int index)
    {
        ApplySelection(index);
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
    /// 注意：0.01 会被 BattleManager.IsSkipMode 判定为「跳过档」——战斗只结算，不打开战斗界面。
    /// </summary>
    private void ApplyBattleSpeed(int selectedIndex)
    {
        if (speedButtons == null || selectedIndex < 0 || selectedIndex >= speedButtons.Length) return;

        OpeningButton selected = speedButtons[selectedIndex];
        if (selected == null) return;

        float delay = selected.SpeedDelay;

        if (BattleManager.Instance != null)
            BattleManager.Instance.LogDelay = delay;

        SaveManager.SaveBattleSpeed(delay);
    }

    // ============================================================
    //  按钮行为
    // ============================================================

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
