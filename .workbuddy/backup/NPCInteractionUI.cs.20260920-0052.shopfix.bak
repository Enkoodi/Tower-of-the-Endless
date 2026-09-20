using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NPC交互UI — 挂载在NPC交互面板上，显示商店界面。
/// </summary>
public class NPCInteractionUI : MonoBehaviour
{
    [Header("窗口根节点")]
    [SerializeField] private GameObject interactionWindow;

    [Header("商店顶部信息")]
    [SerializeField] private TextMeshProUGUI shopCostText;       // "本次购买消耗：XXX金币"
    [SerializeField] private TextMeshProUGUI playerGoldText;     // "拥有金币：XXX"

    [Header("商店 — 5个选项按钮")]
    [SerializeField] private Button option1Button;   // 生命值 +X%
    [SerializeField] private TextMeshProUGUI option1Label;
    [SerializeField] private Button option2Button;   // 攻击力 +X%
    [SerializeField] private TextMeshProUGUI option2Label;
    [SerializeField] private Button option3Button;   // 防御力 +X%
    [SerializeField] private TextMeshProUGUI option3Label;
    [SerializeField] private Button option4Button;   // 随机祝福
    [SerializeField] private TextMeshProUGUI option4Label;
    [SerializeField] private Button option5Button;   // 离开商店
    [SerializeField] private TextMeshProUGUI option5Label;

    // ============================================================
    //  运行时状态
    // ============================================================

    private NPCController currentNPC;
    private PlayerData currentPlayer;
    private bool waitingForBlessing = false;

    /// <summary>NPC交互界面打开/关闭事件（供PlayerMove订阅以锁定/解锁移动）</summary>
    public static event System.Action OnPanelOpen;
    public static event System.Action OnPanelClose;

    public bool IsOpen => interactionWindow != null && interactionWindow.activeInHierarchy;

    // ============================================================
    //  生命周期
    // ============================================================

    private void Awake()
    {
        if (interactionWindow != null)
            interactionWindow.SetActive(false);

        // 绑定按钮事件
        if (option1Button != null)
            option1Button.onClick.AddListener(() => OnBuyHP());
        if (option2Button != null)
            option2Button.onClick.AddListener(() => OnBuyAttack());
        if (option3Button != null)
            option3Button.onClick.AddListener(() => OnBuyDefense());
        if (option4Button != null)
            option4Button.onClick.AddListener(OnBuyBlessing);
        if (option5Button != null)
            option5Button.onClick.AddListener(CloseInteraction);

        // 祝福面板关闭时，刷新商店UI
        BlessingManager.OnPanelClose += OnBlessingPanelClosed;
    }

    // ============================================================
    //  公开接口
    // ============================================================

    /// <summary>
    /// 打开商店界面
    /// </summary>
    public void OpenInteraction(NPCController npc, PlayerData player)
    {
        if (npc == null || player == null || interactionWindow == null) return;

        currentNPC = npc;
        currentPlayer = player;

        interactionWindow.SetActive(true);
        OnPanelOpen?.Invoke();

        RefreshShopUI();
    }

    /// <summary>
    /// 关闭商店界面
    /// </summary>
    public void CloseInteraction()
    {
        if (interactionWindow != null)
            interactionWindow.SetActive(false);

        currentNPC = null;
        currentPlayer = null;
        OnPanelClose?.Invoke();
    }

    // ============================================================
    //  商店界面刷新
    // ============================================================

    /// <summary>
    /// 刷新商店界面 — 更新顶部价格、金币、各选项按钮状态
    /// </summary>
    private void RefreshShopUI()
    {
        if (currentNPC == null || currentPlayer == null) return;

        int cost = NPCController.GetCurrentCost();
        int gold = currentPlayer.Gold;
        bool canAfford = gold >= cost;

        // 顶部信息
        if (shopCostText != null)
            shopCostText.text = $"本次购买消耗：{cost} 金币";
        if (playerGoldText != null)
            playerGoldText.text = $"拥有金币：{gold}";

        // 选项1：生命值 +X%
        SetOption(option1Label, option1Button, $"生命值 +{currentNPC.HpPercent}%  ({cost}金币)", canAfford);

        // 选项2：攻击力 +X%
        SetOption(option2Label, option2Button, $"攻击力 +{currentNPC.AtkPercent}%  ({cost}金币)", canAfford);

        // 选项3：防御力 +X%
        SetOption(option3Label, option3Button, $"防御力 +{currentNPC.DefPercent}%  ({cost}金币)", canAfford);

        // 选项4：随机祝福（价格与其他选项一致）
        SetOption(option4Label, option4Button, $"随机祝福  ({cost}金币)", canAfford);

        // 选项5：离开商店（始终可用）
        if (option5Label != null)
            option5Label.text = "离开商店";
    }

    private static void SetOption(TextMeshProUGUI label, Button button, string text, bool interactable)
    {
        if (label != null)
            label.text = text;
        if (button != null)
            button.interactable = interactable;
    }

    // ============================================================
    //  购买回调
    // ============================================================

    private void OnBuyHP()
    {
        if (currentNPC == null || currentPlayer == null) return;

        int cost = NPCController.GetCurrentCost();
        if (!currentPlayer.SpendGold(cost)) return;

        int rawGain = currentPlayer.HP * currentNPC.HpPercent / 100;
        // 逆推换算后通过 AddHP（内部会乘以 hpMultiplier / 100，正好抵消）
        int inversed = rawGain * 100 / Mathf.Max(1, currentPlayer.HPMultiplier);
        currentPlayer.AddHP(inversed);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 生命值 +{currentNPC.HpPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");
        RefreshShopUI();
    }

    private void OnBuyAttack()
    {
        if (currentNPC == null || currentPlayer == null) return;

        int cost = NPCController.GetCurrentCost();
        if (!currentPlayer.SpendGold(cost)) return;

        int rawGain = currentPlayer.Attack * currentNPC.AtkPercent / 100;
        int inversed = rawGain * 100 / Mathf.Max(1, currentPlayer.AttackMultiplier);
        currentPlayer.AddAttack(inversed);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 攻击力 +{currentNPC.AtkPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");
        RefreshShopUI();
    }

    private void OnBuyDefense()
    {
        if (currentNPC == null || currentPlayer == null) return;

        int cost = NPCController.GetCurrentCost();
        if (!currentPlayer.SpendGold(cost)) return;

        int rawGain = currentPlayer.Defense * currentNPC.DefPercent / 100;
        int inversed = rawGain * 100 / Mathf.Max(1, currentPlayer.DefenseMultiplier);
        currentPlayer.AddDefense(inversed);
        NPCController.IncrementPurchaseCount();

        Debug.Log($"[商店] 防御力 +{currentNPC.DefPercent}%（=+{rawGain}），消耗 {cost} 金币（总购买次数 {NPCController.GetGlobalPurchaseCount()}）");
        RefreshShopUI();
    }

    /// <summary>
    /// 购买随机祝福
    /// </summary>
    private void OnBuyBlessing()
    {
        if (currentNPC == null || currentPlayer == null) return;

        int cost = NPCController.GetCurrentCost();
        if (!currentPlayer.SpendGold(cost)) return;

        Debug.Log($"[商店] 购买了随机祝福，消耗 {cost} 金币");
        NPCController.IncrementPurchaseCount();

        waitingForBlessing = true;

        BlessingManager manager = BlessingManager.Instance;
        if (manager != null)
        {
            manager.ShowWithPool(currentPlayer, currentNPC.GetBlessingPool());
        }
        else
        {
            Debug.LogError("[商店] BlessingManager.Instance 为 null，无法抽取祝福");
            waitingForBlessing = false;
        }

        RefreshShopUI();
    }

    /// <summary>
    /// 祝福面板关闭后，刷新商店界面
    /// </summary>
    private void OnBlessingPanelClosed()
    {
        if (!waitingForBlessing) return;
        waitingForBlessing = false;

        if (currentNPC != null && currentPlayer != null && interactionWindow != null && interactionWindow.activeInHierarchy)
        {
            RefreshShopUI();
        }
    }
}
