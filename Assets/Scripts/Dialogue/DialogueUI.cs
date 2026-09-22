using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对话UI — 挂载在对话面板Canvas上。
/// 整个面板作为一个按钮，点击面板范围内任意位置即可推进对话。
/// 选项点击后若配了"后续对话"索引，会继续播后面的句子；该选项若开了一场战斗，
/// 则等战斗胜利结束后再自动弹出来续接（见 DialogueTrigger 的选项后续对话字段）。
/// UI结构：
///   - Panel（可点击区域）
///     - 名称文本（TextMeshProUGUI）
///     - 对话内容文本（TextMeshProUGUI，支持打字机逐字显示）
///     - 选项1按钮（默认隐藏，最后一句showChoices=true时显示）
///     - 选项2按钮（默认隐藏，最后一句showChoices=true时显示）
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("面板根节点")]
    [SerializeField] private GameObject panelRoot;

    [Header("点击区域（整个Panel作为按钮）")]
    [SerializeField] private Button clickArea;

    [Header("对话文本")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI contentText;

    [Header("选项按钮（默认隐藏，最后一句对话打字完成后出现）")]
    [SerializeField] private Button choice1Button;
    [SerializeField] private Button choice2Button;
    [SerializeField] private TextMeshProUGUI choice1Label;
    [SerializeField] private TextMeshProUGUI choice2Label;

    [Header("打字机效果")]
    [SerializeField] private float charsPerSecond = 30f;

    // ============================================================
    //  事件（供 PlayerMove 订阅以锁定/解锁移动）
    // ============================================================

    public static event System.Action OnPanelOpen;
    public static event System.Action OnPanelClose;

    public bool IsOpen => panelRoot != null && panelRoot.activeInHierarchy;

    // ============================================================
    //  运行时状态
    // ============================================================

    private DialogueTrigger currentTrigger;

    /// <summary>打开时复制一份对话数据 —— NPC 可能在选项里被移除/被击败销毁，面板之后不能依赖它</summary>
    private DialogueLine[] currentLines;
    private string choice1TextCache = "";
    private string choice2TextCache = "";
    private int choice1NextLineCache = -1;
    private int choice2NextLineCache = -1;

    /// <summary>选项开启了战斗：面板先收起，等战斗结束后从 postBattleLineIndex 续接（-1=战后不再说话）</summary>
    private bool awaitingPostBattle;
    private int postBattleLineIndex = -1;

    /// <summary>面板当前是否对玩家显示。OnPanelOpen/OnPanelClose 与它一一对应，不发重复的关闭事件</summary>
    private bool panelShown;

    private int currentLineIndex;
    private bool isTyping;
    private string fullText;
    private Coroutine typewriterCoroutine;

    // ============================================================
    //  生命周期
    // ============================================================

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (clickArea != null)
            clickArea.onClick.AddListener(OnPanelClicked);

        if (choice1Button != null)
        {
            choice1Button.onClick.AddListener(OnChoice1Clicked);
            choice1Button.gameObject.SetActive(false);
        }

        if (choice2Button != null)
        {
            choice2Button.onClick.AddListener(OnChoice2Clicked);
            choice2Button.gameObject.SetActive(false);
        }

        // 选项开启的战斗结束后，回来播战后续接的那几句
        BattleManager.OnBattleClose += OnBattleClosed;
    }

    private void OnDestroy()
    {
        BattleManager.OnBattleClose -= OnBattleClosed;
    }

    // ============================================================
    //  公开接口
    // ============================================================

    /// <summary>打开对话界面</summary>
    public void OpenDialogue(DialogueTrigger trigger)
    {
        if (trigger == null || trigger.Lines == null || trigger.Lines.Length == 0)
        {
            Debug.LogWarning("[DialogueUI] 对话数据为空，无法打开");
            return;
        }

        DialogueLine[] lines = (DialogueLine[])trigger.Lines.Clone();
        OpenLines(trigger, lines, ResolveStartIndex(trigger, lines));
    }

    /// <summary>载入一段对话并显示。trigger 允许为 null（战后续接时 NPC 可能已被销毁）</summary>
    private void OpenLines(DialogueTrigger trigger, DialogueLine[] lines, int startIndex)
    {
        currentTrigger = trigger;
        currentLines = lines;
        choice1TextCache = trigger != null ? trigger.Choice1Text : "";
        choice2TextCache = trigger != null ? trigger.Choice2Text : "";
        choice1NextLineCache = trigger != null ? trigger.Choice1NextLineIndex : -1;
        choice2NextLineCache = trigger != null ? trigger.Choice2NextLineIndex : -1;
        awaitingPostBattle = false;
        postBattleLineIndex = -1;

        OpenPanelAt(ClampLineIndex(startIndex, lines.Length));
    }

    /// <summary>显示面板并从指定句开始播</summary>
    private void OpenPanelAt(int index)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        panelShown = true;
        ShowLineFrom(index);
        OnPanelOpen?.Invoke();
    }

    /// <summary>隐藏选项按钮并播放指定句</summary>
    private void ShowLineFrom(int index)
    {
        if (choice1Button != null) choice1Button.gameObject.SetActive(false);
        if (choice2Button != null) choice2Button.gameObject.SetActive(false);

        currentLineIndex = index;
        ShowLine(index);
    }

    /// <summary>关闭对话界面</summary>
    public void CloseDialogue()
    {
        StopTypewriter();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        currentTrigger = null;
        currentLines = null;
        awaitingPostBattle = false;
        postBattleLineIndex = -1;

        if (panelShown)
        {
            panelShown = false;
            OnPanelClose?.Invoke();
        }
    }

    /// <summary>只收起面板、保留对话数据（战斗结束后还要用它续接）</summary>
    private void HidePanel()
    {
        StopTypewriter();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (panelShown)
        {
            panelShown = false;
            OnPanelClose?.Invoke();
        }
    }

    private void StopTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        isTyping = false;
    }

    // ============================================================
    //  点击交互
    // ============================================================

    /// <summary>
    /// 点击面板任意位置：
    ///   1. 正在打字 → 立即完成打字
    ///   2. 打字完成且有下一句 → 显示下一句
    ///   3. 打字完成且无下一句（无选项） → 关闭对话
    ///   4. 选项按钮已显示 → 不处理（由按钮自身处理点击）
    /// </summary>
    private void OnPanelClicked()
    {
        if (currentLines == null) return;
        DialogueLine[] lines = currentLines;

        // 正在打字 → 立即完成
        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        // 打字已完成
        DialogueLine currentLine = lines[currentLineIndex];
        int nextIndex = GetNextLineIndex(lines, currentLineIndex);
        bool isLast = nextIndex < 0;

        // 如果是最后一句且选项按钮已显示，不拦截点击（让按钮响应）
        if (isLast && currentLine.showChoices)
        {
            bool choicesVisible = (choice1Button != null && choice1Button.gameObject.activeSelf)
                               || (choice2Button != null && choice2Button.gameObject.activeSelf);
            if (choicesVisible) return;
        }

        // 还有下一句 → 推进
        if (!isLast)
        {
            currentLineIndex = nextIndex;
            ShowLine(currentLineIndex);
            return;
        }

        // 最后一句且无选项 → 关闭
        CloseDialogue();
    }

    // ============================================================
    //  分支解析
    // ============================================================

    /// <summary>根据开场分支决定对话从哪一句开始。</summary>
    private int ResolveStartIndex(DialogueTrigger trigger, DialogueLine[] lines)
    {
        if (lines == null || lines.Length == 0) return 0;

        if (trigger.EntryBranches != null)
        {
            foreach (DialogueEntryBranch branch in trigger.EntryBranches)
            {
                if (branch == null) continue;

                bool matched;
                if (string.IsNullOrEmpty(branch.specialEnemyId))
                {
                    matched = true; // 无条件兜底分支
                }
                else
                {
                    bool defeated = SpecialEnemyManager.Instance != null
                                 && SpecialEnemyManager.Instance.IsDefeated(branch.specialEnemyId);
                    matched = defeated == branch.requireDefeated;
                }

                if (matched)
                {
                    return ClampLineIndex(branch.startLineIndex, lines.Length);
                }
            }
        }

        return 0;
    }

    /// <summary>根据当前句的 nextLineIndex 计算下一句索引；返回 -1 表示没有下一句。</summary>
    private int GetNextLineIndex(DialogueLine[] lines, int currentIndex)
    {
        if (lines == null || currentIndex < 0 || currentIndex >= lines.Length)
            return -1;

        int next = lines[currentIndex].nextLineIndex;

        if (next == -1)
        {
            int linear = currentIndex + 1;
            return linear < lines.Length ? linear : -1;
        }

        if (next == -2)
            return -1; // 显式结束

        if (next >= 0 && next < lines.Length)
            return next;

        Debug.LogWarning($"[DialogueUI] 第 {currentIndex} 句的 nextLineIndex({next}) 越界，按顺序处理");
        int fallback = currentIndex + 1;
        return fallback < lines.Length ? fallback : -1;
    }

    private int ClampLineIndex(int index, int length)
    {
        if (index < 0) return 0;
        if (index >= length) return length - 1;
        return index;
    }

    // ============================================================
    //  对话显示
    // ============================================================

    private void ShowLine(int index)
    {
        if (currentLines == null) return;

        DialogueLine[] lines = currentLines;
        if (index < 0 || index >= lines.Length) return;

        DialogueLine line = lines[index];

        if (nameText != null)
            nameText.text = line.speakerName;

        fullText = line.content;

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypewriterEffect());
    }

    private IEnumerator TypewriterEffect()
    {
        isTyping = true;
        contentText.text = "";

        float delay = charsPerSecond > 0 ? 1f / charsPerSecond : 0.02f;

        for (int i = 0; i < fullText.Length; i++)
        {
            contentText.text += fullText[i];
            yield return new WaitForSeconds(delay);
        }

        contentText.text = fullText;
        isTyping = false;
        typewriterCoroutine = null;

        // 打字完成后，检查是否需要显示选项按钮
        OnTypingComplete();
    }

    /// <summary>跳过打字动画，立即显示全部文字</summary>
    private void CompleteTyping()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        contentText.text = fullText;
        isTyping = false;

        OnTypingComplete();
    }

    /// <summary>打字完成后的处理：如果无下一句且配置了showChoices，显示选项按钮</summary>
    private void OnTypingComplete()
    {
        if (currentLines == null) return;

        DialogueLine currentLine = currentLines[currentLineIndex];

        if (GetNextLineIndex(currentLines, currentLineIndex) < 0 && currentLine.showChoices)
        {
            ShowChoiceButtons();
        }
    }

    // ============================================================
    //  选项按钮
    // ============================================================

    private void ShowChoiceButtons()
    {
        // 原对话对象已销毁（NPC 在选项里被移除、或被战斗击败）时选项已无处可挂 → 只留文本
        if (currentTrigger == null) return;

        if (choice1Button != null)
        {
            choice1Button.gameObject.SetActive(true);
            if (choice1Label != null)
                choice1Label.text = choice1TextCache;
        }

        if (choice2Button != null)
        {
            choice2Button.gameObject.SetActive(true);
            if (choice2Label != null)
                choice2Label.text = choice2TextCache;
        }
    }

    /// <summary>"选项1"按钮点击</summary>
    private void OnChoice1Clicked() => ExecuteChoice(1);

    /// <summary>"选项2"按钮点击</summary>
    private void OnChoice2Clicked() => ExecuteChoice(2);

    /// <summary>
    /// 选项点击：先收起面板，再执行选项绑定的逻辑（购买 / 开战 / 移除NPC …），最后决定对话怎么走。
    ///   配了续接句 → 从该句接着播；
    ///   该选项开了一场战斗 → 等战斗胜利结束后自动弹回来续接。
    /// </summary>
    private void ExecuteChoice(int choiceIndex)
    {
        if (currentLines == null)
        {
            CloseDialogue();
            return;
        }

        int followUp = choiceIndex == 1 ? choice1NextLineCache : choice2NextLineCache;
        bool hasFollowUp = followUp >= 0 && followUp < currentLines.Length;

        // 待续接状态必须赶在执行选项之前摆好：跳过档下整场战斗会在下面这一行同步打完，
        // 战斗结束回调当场就会读它。面板也先收起，免得和战斗界面叠在一起。
        awaitingPostBattle = hasFollowUp;
        postBattleLineIndex = hasFollowUp ? followUp : -1;
        HidePanel();

        // 执行选项逻辑。之后 NPC 可能已经不存在，所以下面只读打开时复制下来的数据
        if (currentTrigger != null)
        {
            UnityEvent choiceEvent = choiceIndex == 1 ? currentTrigger.OnChoice1 : currentTrigger.OnChoice2;
            choiceEvent?.Invoke();
        }

        // 战斗还在进行 → 停手，等 OnBattleClosed 把续接句弹回来
        if (IsBattleRunning) return;

        awaitingPostBattle = false;
        postBattleLineIndex = -1;

        if (hasFollowUp && currentLines != null)
        {
            OpenPanelAt(followUp);
            return;
        }

        CloseDialogue();
    }

    /// <summary>战斗结束回调：打赢了才弹回战后续接的那几句</summary>
    private void OnBattleClosed()
    {
        if (!awaitingPostBattle) return;

        PostBattleDialogue(BattleManager.Instance != null && BattleManager.Instance.LastBattleWon);
    }

    /// <summary>战斗已结束：赢了弹出战后续接的句子，输了/没配续接句就收拾干净</summary>
    private void PostBattleDialogue(bool won)
    {
        int index = postBattleLineIndex;
        awaitingPostBattle = false;
        postBattleLineIndex = -1;

        if (!won || index < 0 || currentLines == null || index >= currentLines.Length)
        {
            currentTrigger = null;
            currentLines = null;
            return;
        }

        OpenPanelAt(index);
    }

    /// <summary>选项是否刚开了一场战斗</summary>
    private static bool IsBattleRunning => BattleManager.Instance != null && BattleManager.Instance.IsFighting;
}
