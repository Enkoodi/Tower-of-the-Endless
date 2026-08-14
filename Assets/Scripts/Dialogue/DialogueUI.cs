using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对话UI — 挂载在对话面板Canvas上。
/// 整个面板作为一个按钮，点击面板范围内任意位置即可推进对话。
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

        currentTrigger = trigger;
        currentLineIndex = 0;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        // 隐藏选项按钮
        if (choice1Button != null) choice1Button.gameObject.SetActive(false);
        if (choice2Button != null) choice2Button.gameObject.SetActive(false);

        ShowLine(currentLineIndex);
        OnPanelOpen?.Invoke();
    }

    /// <summary>关闭对话界面</summary>
    public void CloseDialogue()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);

        currentTrigger = null;
        OnPanelClose?.Invoke();
    }

    // ============================================================
    //  点击交互
    // ============================================================

    /// <summary>
    /// 点击面板任意位置：
    ///   1. 正在打字 → 立即完成打字
    ///   2. 打字完成且有下一句 → 显示下一句
    ///   3. 打字完成且是最后一句（无选项） → 关闭对话
    ///   4. 选项按钮已显示 → 不处理（由按钮自身处理点击）
    /// </summary>
    private void OnPanelClicked()
    {
        if (currentTrigger == null) return;
        DialogueLine[] lines = currentTrigger.Lines;

        // 正在打字 → 立即完成
        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        // 打字已完成
        DialogueLine currentLine = lines[currentLineIndex];

        // 如果是最后一句且选项按钮已显示，不拦截点击（让按钮响应）
        if (currentLineIndex >= lines.Length - 1 && currentLine.showChoices)
        {
            bool choicesVisible = (choice1Button != null && choice1Button.gameObject.activeSelf)
                               || (choice2Button != null && choice2Button.gameObject.activeSelf);
            if (choicesVisible) return;
        }

        // 还有下一句 → 推进
        if (currentLineIndex < lines.Length - 1)
        {
            currentLineIndex++;
            ShowLine(currentLineIndex);
            return;
        }

        // 最后一句且无选项 → 关闭
        CloseDialogue();
    }

    // ============================================================
    //  对话显示
    // ============================================================

    private void ShowLine(int index)
    {
        DialogueLine[] lines = currentTrigger.Lines;
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

    /// <summary>打字完成后的处理：如果是最后一句且配置了showChoices，显示选项按钮</summary>
    private void OnTypingComplete()
    {
        DialogueLine[] lines = currentTrigger.Lines;
        DialogueLine currentLine = lines[currentLineIndex];

        if (currentLineIndex >= lines.Length - 1 && currentLine.showChoices)
        {
            ShowChoiceButtons();
        }
    }

    // ============================================================
    //  选项按钮
    // ============================================================

    private void ShowChoiceButtons()
    {
        if (choice1Button != null)
        {
            choice1Button.gameObject.SetActive(true);
            if (choice1Label != null)
                choice1Label.text = currentTrigger.Choice1Text;
        }

        if (choice2Button != null)
        {
            choice2Button.gameObject.SetActive(true);
            if (choice2Label != null)
                choice2Label.text = currentTrigger.Choice2Text;
        }
    }

    /// <summary>"选项1"按钮点击 → 执行选项1逻辑，然后关闭对话</summary>
    private void OnChoice1Clicked()
    {
        if (currentTrigger == null) return;

        currentTrigger.OnChoice1?.Invoke();
        CloseDialogue();
    }

    /// <summary>"选项2"按钮点击 → 执行选项2逻辑，然后关闭对话</summary>
    private void OnChoice2Clicked()
    {
        if (currentTrigger == null) return;

        currentTrigger.OnChoice2?.Invoke();
        CloseDialogue();
    }
}
