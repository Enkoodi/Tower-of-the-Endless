using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 阵亡画面 — 挂在 Game 场景的 DeathCanvas 上（场景内实例，不跳场景）。
/// 触发两条路：
///   · 战斗内阵亡：BattleManager.OnBattleClose 之后判定（战斗窗口已真正消失、移动已解锁）；
///   · 战斗外阵亡：PlayerData 在战斗外把 HP 扣到 0 时发 OnPlayerDied（魔力光环 / 魂之门）。
/// 两个按钮：读档（读 auto_save.json 重进本层） / 新游戏（复用 SettingMenu.StartNewGame 的重置）。
/// </summary>
public class DeathPanel : MonoBehaviour
{
    public static DeathPanel Instance { get; private set; }

    /// <summary>面板开/关事件，供 PlayerMove 锁输入（与其它面板同一套）。</summary>
    public static event System.Action OnPanelOpen;
    public static event System.Action OnPanelClose;

    [Header("面板根节点（关闭时 SetActive(false)；留空则自动找子节点 Panel）")]
    [SerializeField] private GameObject panelRoot;

    [Header("按钮")]
    [SerializeField] private Button loadSaveButton;
    [SerializeField] private Button newGameButton;

    [Header("游戏场景名")]
    [SerializeField] private string gameSceneName = "Game";

    private bool isOpen;

    void Awake()
    {
        Instance = this;
        AutoBind();

        if (panelRoot != null) panelRoot.SetActive(false);
        if (loadSaveButton != null) loadSaveButton.onClick.AddListener(OnLoadSaveClicked);
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
    }

    void OnEnable()
    {
        BattleManager.OnBattleClose += HandleBattleClose;
        PlayerData.OnPlayerDied += HandlePlayerDied;
    }

    void OnDisable()
    {
        BattleManager.OnBattleClose -= HandleBattleClose;
        PlayerData.OnPlayerDied -= HandlePlayerDied;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>打开阵亡画面（重复调用无副作用）。</summary>
    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        // 挑战模式：把这次阵亡（位置 + 面板数值）记进存档，『安特罗波斯』的祝福按它生成。
        // 这里是两条死亡路径唯一的汇合点，放别处会漏掉一边。
        AnthroposBlessing.RecordDeath(FindFirstObjectByType<PlayerData>());

        // 没有自动存档时「读档」无意义 → 置灰（未解锁的灰字由 OpeningButton 负责）
        if (loadSaveButton != null)
            loadSaveButton.interactable = SaveManager.HasAutoSave();

        if (panelRoot != null) panelRoot.SetActive(true);
        OnPanelOpen?.Invoke();

        Debug.Log("[DeathPanel] 玩家阵亡，打开阵亡画面");
        PlayerLog.Note("玩家阵亡");
    }

    /// <summary>关闭阵亡画面（读档 / 新游戏跳转前调用）。</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);
        OnPanelClose?.Invoke();
    }

    // ============================================================
    //  触发
    // ============================================================

    /// <summary>
    /// 战斗收尾后判定：刚打完且失败 + 玩家已死 → 弹阵亡画面。
    /// 挂 OnBattleClose 而非 EndBattle：此时战斗窗口已关闭、移动已解锁，
    /// 面板再自行锁输入，避免「窗口还开着就叠两层 / 结算那一秒还能走」。
    /// </summary>
    private void HandleBattleClose()
    {
        if (isOpen) return;

        BattleManager bm = BattleManager.Instance;
        if (bm == null || bm.LastBattleWon) return;   // 胜利，或非阵亡的收场（如僵持）→ 不弹

        PlayerData player = FindFirstObjectByType<PlayerData>();
        if (player != null && player.IsDead)
            Open();
    }

    /// <summary>战斗外阵亡（魔力光环 / 魂之门扣血致死）。</summary>
    private void HandlePlayerDied()
    {
        Open();
    }

    // ============================================================
    //  按钮
    // ============================================================

    /// <summary>读档：读自动存档重进 Game（MapGenerator.Start 会消费 LoadAutoSaveOnStart → LoadAutoGame）。</summary>
    private void OnLoadSaveClicked()
    {
        if (!SaveManager.HasAutoSave())
        {
            Debug.LogWarning("[DeathPanel] 没有自动存档，读档无效");
            return;
        }

        Close();
        SaveManager.LoadAutoSaveOnStart = true;
        ScreenFader.FadeToScene(gameSceneName);
    }

    /// <summary>
    /// 新游戏：与 SettingMenu.StartNewGame 共用 NewGameFlow —— 清自动存档 + 重置本局静态状态 + 重进 Game。
    /// </summary>
    private void OnNewGameClicked()
    {
        Close();

        // 新游戏沿用这一局的模式：在挑战局里阵亡 → 重开的还是挑战局（不是剧情模式）
        GameSession.EnsureMode();

        NewGameFlow.Start(GameSession.Mode);

        ScreenFader.FadeToScene(gameSceneName);
    }

    // ============================================================
    //  绑定（拖引用优先，为空时按节点名兜底查找）
    // ============================================================

    private void AutoBind()
    {
        if (panelRoot == null)
        {
            Transform panel = FindDeep(transform, "Panel");
            if (panel != null) panelRoot = panel.gameObject;
        }

        if (loadSaveButton == null)
            loadSaveButton = FindDeep(transform, "LoadSave")?.GetComponent<Button>();
        if (newGameButton == null)
            newGameButton = FindDeep(transform, "NewGame")?.GetComponent<Button>();
    }

    /// <summary>在 DeathCanvas 子树里按节点名查找（层级很浅，找不到返回 null，不报错）。</summary>
    private static Transform FindDeep(Transform root, string nodeName)
    {
        if (root.name == nodeName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), nodeName);
            if (found != null) return found;
        }
        return null;
    }
}
