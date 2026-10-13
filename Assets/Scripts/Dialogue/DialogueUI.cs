using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对话UI — 挂载在对话面板Canvas上。
/// 整个面板作为一个按钮，点击面板范围内任意位置即可推进对话。
/// 选项点击后若配了"后续对话"索引，会继续播后面的句子；该选项若开了一场战斗，
/// 则等战斗胜利结束后再自动弹出来续接（见 DialogueTrigger 的选项后续对话字段）。
/// 对话结束时额外执行一次 DialogueTrigger 的 onDialogueEnd，随后落 endFlagId 标记并发出 OnDialogueEnded 信号。
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

    [Header("打字机（可见字符/秒）")]
    [SerializeField] private float charsPerSecond = 30f;

    [Header("文字飘散（台词里用 {S} 包住的字才会飘）")]
    [SerializeField] private float scatterJitter = 6f;
    [SerializeField] private float scatterWave = 2f;
    [SerializeField] private float scatterSpeed = 2.5f;
    [SerializeField] private int scatterSeed = 20261013;

    // ============================================================
    //  事件（供 PlayerMove 订阅以锁定/解锁移动）
    // ============================================================

    public static event System.Action OnPanelOpen;
    public static event System.Action OnPanelClose;

    /// <summary>对话收尾逻辑被执行时发出（参数=发起对话的 NPC，可能已被销毁，订阅方自行判空）</summary>
    public static event System.Action<DialogueTrigger> OnDialogueEnded;

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

    /// <summary>对话自然播完时执行一次（打开时复制，NPC 可能已被销毁）</summary>
    private UnityEvent endActionCache;
    private bool endActionFired;

    /// <summary>本段对话是否点过选项。点过就整段封死，结束事件不再执行（勾了 endActionAfterChoice 则除外）</summary>
    private bool choiceHandled;

    /// <summary>勾了「点过选项也算正常结束」（打开时复制）</summary>
    private bool endActionAfterChoice;

    /// <summary>结束标记 ID（打开时复制；非空且已标记过就不再执行收尾逻辑）</summary>
    private string endFlagIdCache = "";

    /// <summary>面板当前是否对玩家显示。OnPanelOpen/OnPanelClose 与它一一对应，不发重复的关闭事件</summary>
    private bool panelShown;

    private int currentLineIndex;
    private bool isTyping;
    private Coroutine typewriterCoroutine;

    /// <summary>本句里被 {S} 包住的区间（干净文本的下标）</summary>
    private readonly List<Vector2Int> scatterBounds = new List<Vector2Int>();

    /// <summary>偏移前的顶点原值，每份对应一个材质。每帧都写「原值 + 偏移」，绝不累加</summary>
    private Vector3[][] scatterBase;

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

        // 文字飘散要跟着 TMP 的重建走：重建会把顶点写回原值，必须紧接着补一次
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private void Update()
    {
        // 打字机停下来之后也要继续飘，所以每帧自己写一遍顶点
        if (scatterBounds.Count == 0 || !IsOpen) return;
        ApplyScatter(Time.unscaledTime);
    }

    private void OnDestroy()
    {
        BattleManager.OnBattleClose -= OnBattleClosed;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
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
        int startIndex = ResolveStartIndex(trigger, lines);

        // 玩家日志：同一个 NPC 只记一次（反复撞对话会刷屏）
        if (startIndex < lines.Length
            && !string.IsNullOrEmpty(lines[startIndex].speakerName)
            && PlayerLog.FirstTime($"dialog:{trigger.GetInstanceID()}"))
        {
            PlayerLog.Note($"对话 {lines[startIndex].speakerName}");
        }

        OpenLines(trigger, lines, startIndex);
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
        endActionCache = trigger != null ? trigger.OnDialogueEnd : null;
        endActionAfterChoice = trigger != null && trigger.EndActionAfterChoice;
        endFlagIdCache = trigger != null ? trigger.EndFlagId : "";
        endActionFired = false;
        choiceHandled = false;

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

    /// <summary>关闭对话界面（打断语义，不执行结束事件）</summary>
    public void CloseDialogue()
    {
        StopTypewriter();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        currentTrigger = null;
        currentLines = null;
        awaitingPostBattle = false;
        postBattleLineIndex = -1;
        scatterBounds.Clear();
        scatterBase = null;

        if (panelShown)
        {
            panelShown = false;
            OnPanelClose?.Invoke();
        }
    }

    /// <summary>
    /// 对话正常播完：先收起面板 → 执行一次收尾逻辑 → 落结束标记 → 发出信号。
    /// 判据：点过选项要 endActionAfterChoice 放行；一段对话只跑一次；已标记过就不再跑（只结算一次）。
    /// 顺序不能反 —— 收尾逻辑里常绑 NpcRemover.RemoveSelf，NPC 会被当场销毁。
    /// </summary>
    private void FinishDialogue()
    {
        DialogueTrigger finished = currentTrigger;          // CloseDialogue 会清掉它
        bool runEndAction = (!choiceHandled || endActionAfterChoice)
                         && !endActionFired
                         && !DialogueFlags.IsSet(endFlagIdCache);
        endActionFired = true;

        CloseDialogue();

        if (!runEndAction) return;

        endActionCache?.Invoke();
        DialogueFlags.Set(endFlagIdCache);
        OnDialogueEnded?.Invoke(finished);
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
    ///   3. 打字完成且无下一句（无选项） → 关闭对话，未点过选项则执行结束事件
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

        // 最后一句且无选项 → 关闭，并按规则执行结束事件
        FinishDialogue();
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

                // 结束标记条件：按本 NPC 的 endFlagId 判断（Ignore 才跳过）
                if (branch.endFlagCondition != DialogueFlagCondition.Ignore)
                {
                    if (string.IsNullOrEmpty(trigger.EndFlagId))
                    {
                        Debug.LogWarning($"[DialogueUI] {trigger.name} 配了结束标记分支但 endFlagId 为空，该分支永不命中");
                        continue;
                    }

                    bool flagSet = DialogueFlags.IsSet(trigger.EndFlagId);
                    if (flagSet != (branch.endFlagCondition == DialogueFlagCondition.Set)) continue;
                }

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

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        // 整段一次设好，打字机只递增 maxVisibleCharacters：
        // 标签不会露出半截，换行也在这一刻定死、不随字数变化跳动。
        // {S} 必须先剥掉 —— TMP 不认识它，留着会被当普通文字画出来。
        contentText.text = ParseScatterTags(line.content, scatterBounds);
        scatterBase = null;                 // 换了句子，等重建回调重存原值
        contentText.ForceMeshUpdate();      // textInfo 要强制更新一次才是这句的
        contentText.maxVisibleCharacters = 0;

        typewriterCoroutine = StartCoroutine(TypewriterEffect());
    }

    /// <summary>
    /// 文字已整段设好，这里只把 maxVisibleCharacters 一格格放开。
    /// 总字数取 textInfo.characterCount —— 它不含标签字符，否则标签长的台词会明显变慢。
    /// </summary>
    private IEnumerator TypewriterEffect()
    {
        isTyping = true;

        int total = contentText.textInfo.characterCount;
        float delay = charsPerSecond > 0 ? 1f / charsPerSecond : 0.02f;

        for (int i = 1; i <= total; i++)
        {
            contentText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(delay);
        }

        contentText.maxVisibleCharacters = int.MaxValue;
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

        contentText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;

        OnTypingComplete();
    }

    // ============================================================
    //  文字飘散 —— 只作用于台词里用 {S} 包住的字
    // ============================================================

    private const string ScatterTag = "{S}";

    /// <summary>TMP 每次重建完都会发这个事件，包括打字机推 maxVisibleCharacters 引发的那次</summary>
    private void OnTextChanged(UnityEngine.Object obj)
    {
        if (obj != contentText) return;

        SnapshotScatterBase();              // TMP 刚写完原值，趁机存一份
        ApplyScatter(Time.unscaledTime);    // 同帧补写，否则这一帧会闪回原型
    }

    /// <summary>剥掉 {S} 并记下被标记的区间（干净文本的下标）。成对使用；只写一个就到句尾。</summary>
    private string ParseScatterTags(string content, List<Vector2Int> bounds)
    {
        bounds.Clear();
        if (string.IsNullOrEmpty(content)) return "";

        System.Text.StringBuilder sb = new System.Text.StringBuilder(content.Length);
        int cursor = 0;
        int start = -1;

        while (true)
        {
            int at = content.IndexOf(ScatterTag, cursor, System.StringComparison.Ordinal);
            if (at < 0) break;

            sb.Append(content, cursor, at - cursor);
            if (start < 0)
            {
                start = sb.Length;
            }
            else
            {
                bounds.Add(new Vector2Int(start, sb.Length));
                start = -1;
            }

            cursor = at + ScatterTag.Length;
        }

        sb.Append(content, cursor, content.Length - cursor);
        if (start >= 0) bounds.Add(new Vector2Int(start, sb.Length));

        return sb.ToString();
    }

    /// <summary>characterInfo.index 就是干净文本里的下标</summary>
    private bool IsScatterMarked(int charIndex)
    {
        for (int i = 0; i < scatterBounds.Count; i++)
        {
            if (charIndex >= scatterBounds[i].x && charIndex < scatterBounds[i].y) return true;
        }
        return false;
    }

    /// <summary>存一份顶点原值（每个材质一份）。之后每帧都写「原值 + 偏移」，绝不累加</summary>
    private void SnapshotScatterBase()
    {
        TMP_TextInfo info = contentText != null ? contentText.textInfo : null;
        if (info == null) return;

        if (scatterBase == null || scatterBase.Length < info.materialCount)
            scatterBase = new Vector3[info.materialCount][];

        for (int m = 0; m < info.materialCount; m++)
        {
            Vector3[] src = info.meshInfo[m].vertices;
            if (scatterBase[m] == null || scatterBase[m].Length < src.Length)
                scatterBase[m] = new Vector3[src.Length];

            src.CopyTo(scatterBase[m], 0);
        }
    }

    /// <summary>只动 {S} 区间里、且已经被打字机放出来的字</summary>
    private void ApplyScatter(float time)
    {
        if (scatterBounds.Count == 0 || scatterBase == null || contentText == null) return;

        TMP_TextInfo info = contentText.textInfo;
        if (info == null) return;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible || !IsScatterMarked(ch.index)) continue;

            int m = ch.materialReferenceIndex;
            if (m >= scatterBase.Length || scatterBase[m] == null) continue;

            Vector3[] verts = info.meshInfo[m].vertices;
            int v = ch.vertexIndex;
            if (v < 0 || v + 3 >= verts.Length || v + 3 >= scatterBase[m].Length) continue;

            Vector3 off = ScatterOffset(ch.index, time);
            for (int k = 0; k < 4; k++)
                verts[v + k] = scatterBase[m][v + k] + off;
        }

        contentText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    /// <summary>固定方向（每个字一份，稳定不跳）叠一层随时间摆动</summary>
    private Vector3 ScatterOffset(int charIndex, float time)
    {
        float angle = Hash01(charIndex) * 6.2831853f;
        float radius = 0.55f + 0.45f * Hash01(charIndex + 977);
        float sway = Mathf.Sin(time * scatterSpeed + angle) * scatterWave;

        return new Vector3(
            Mathf.Cos(angle) * scatterJitter * radius + sway,
            Mathf.Sin(angle) * scatterJitter * radius + sway * 0.5f,
            0f);
    }

    private float Hash01(int value)
    {
        unchecked
        {
            uint h = (uint)(value * 73856093) ^ (uint)(scatterSeed * 19349663);
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777216f;
        }
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
    ///   点过选项后结束事件是否还执行，由 DialogueTrigger.endActionAfterChoice 决定。
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

        // 玩家日志：玩家选了哪个选项（选项文案更有用，取不到时退化为序号）
        string choiceText = choiceIndex == 1 ? choice1TextCache : choice2TextCache;
        PlayerLog.Note($"对话选项 {choiceIndex}：{(string.IsNullOrEmpty(choiceText) ? "（未命名）" : choiceText)}");

        // 点过选项就整段封死：结束事件不再执行（含战后续接的那一段）
        choiceHandled = true;

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

        // 选项即终点：该选项自己就是收尾动作，结束事件不参与
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
