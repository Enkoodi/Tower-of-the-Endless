using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 祝福选择面板 — 挂载在 BlessingPanel 根对象上。
/// Show() 时显示 N 张卡片，从 cardContainer 的子对象上获取 BlessingCardUI 组件。
/// </summary>
public class BlessingPanel : MonoBehaviour
{
    [Header("面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Header("卡片父节点（子对象挂有 BlessingCardUI）")]
    [SerializeField] private Transform cardContainer;

    private BlessingCardUI[] cards;
    private System.Action<BlessingData> onChosenCallback;

    void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        cards = cardContainer.GetComponentsInChildren<BlessingCardUI>();
    }

    public void Show(List<BlessingData> drawn, System.Action<BlessingData> onChosen)
    {
        if (drawn == null || drawn.Count == 0)
        {
            Debug.LogError("[BlessingPanel] 传入的祝福列表为空！");
            return;
        }

        onChosenCallback = onChosen;

        for (int i = 0; i < cards.Length; i++)
        {
            if (i < drawn.Count)
            {
                FillCard(cards[i], drawn[i]);
                cards[i].gameObject.SetActive(true);
            }
            else
            {
                cards[i].gameObject.SetActive(false);
            }
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void FillCard(BlessingCardUI card, BlessingData data)
    {
        if (card.background != null && data.backgroundSprite != null)
            card.background.sprite = data.backgroundSprite;

        if (card.nameText != null)
            card.nameText.text = data.blessingName;
        if (card.descText != null)
            card.descText.text = BuildDescription(data);
        if (card.statusText != null)
            card.statusText.text = BuildStatusLine(data);

        card.button.onClick.RemoveAllListeners();
        card.button.onClick.AddListener(() =>
        {
            Hide();
            onChosenCallback?.Invoke(data);
        });
    }

    // ============================================================
    //  卡片文案
    //  描述在上方的 BlessingDesc，状态行在下方的 BlessingStatus（两个独立组件）
    // ============================================================

    /// <summary>
    /// 描述：已拥有时把会随等级变化的数字标成「旧（旧→新）」，例如「额外获得10（10→20）金币」。
    /// </summary>
    private static string BuildDescription(BlessingData data)
    {
        if (data == null) return string.Empty;

        BlessingManager manager = BlessingManager.Instance;
        if (manager == null || data.type != BlessingType.Conditional) return data.description;

        BlessingEffect effect = manager.GetEffect<BlessingEffect>(data.id.ToString());
        return effect == null ? data.description : ApplyLevelUpPreview(data.description, effect);
    }

    /// <summary>
    /// 卡片底部状态行：特殊祝福显示 `Lv.N → Lv.N+1`（重复选择 = 升级），
    /// 普通祝福显示 `已获得 N 次`，两者都没有时留空。
    /// </summary>
    private static string BuildStatusLine(BlessingData data)
    {
        if (data == null) return string.Empty;

        BlessingManager manager = BlessingManager.Instance;
        if (manager == null) return string.Empty;

        if (data.type == BlessingType.Conditional)
        {
            BlessingEffect effect = manager.GetEffect<BlessingEffect>(data.id.ToString());
            return effect == null ? string.Empty : $"Lv.{effect.Level} → Lv.{effect.Level + 1}";
        }

        int count = manager.GetObtainedCount(data.id);
        return count > 0 ? $"已获得 {count} 次" : string.Empty;
    }

    private static readonly Regex NumberRegex = new Regex(@"\d+(?:\.\d+)?", RegexOptions.Compiled);

    /// <summary>按数字切成「文本, 数字, 文本, 数字, ...」——下标为奇数的元素一定是数字。</summary>
    private static List<string> SplitNumbers(string s)
    {
        List<string> parts = new List<string>();
        int last = 0;
        foreach (Match m in NumberRegex.Matches(s))
        {
            parts.Add(s.Substring(last, m.Index - last));
            parts.Add(m.Value);
            last = m.Index + m.Length;
        }
        parts.Add(s.Substring(last));
        return parts;
    }

    /// <summary>
    /// 两份同源描述（同一公式的相邻等级）里数值发生变化的项。
    /// 非数字部分必须逐段一致，否则视为结构不同、放弃预览。
    /// </summary>
    private static List<string[]> ExtractNumberChanges(string cur, string next)
    {
        List<string[]> changes = new List<string[]>();
        List<string> a = SplitNumbers(cur);
        List<string> b = SplitNumbers(next);
        if (a.Count != b.Count) return changes;

        for (int i = 0; i < a.Count; i++)
        {
            if (i % 2 == 0)
            {
                if (a[i] != b[i]) return new List<string[]>();   // 结构不同 → 放弃
            }
            else if (a[i] != b[i])
            {
                changes.Add(new[] { a[i], b[i] });
            }
        }
        return changes;
    }

    /// <summary>
    /// 把文案里数值等于 from 的数字改写成「from（from→to）」，
    /// 例如「额外获得10金币」→「额外获得10（10→20）金币」。
    /// annotate=false 时只做静默换算（用于把 Lv.1 文案抬到当前层）。
    /// </summary>
    private static string ReplaceNumbers(string text, List<string[]> changes, bool annotate)
    {
        List<string> parts = SplitNumbers(text);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i % 2 == 0)
            {
                sb.Append(parts[i]);
                continue;
            }

            string label = null;
            foreach (string[] c in changes)
            {
                if (c[0] == parts[i])
                {
                    label = annotate ? $"{c[0]}（{c[0]}→{c[1]}）" : c[1];
                    break;
                }
            }
            sb.Append(label ?? parts[i]);
        }
        return sb.ToString();
    }

    private static int CountNumber(List<string> parts, string value)
    {
        int n = 0;
        for (int i = 1; i < parts.Count; i += 2)
            if (parts[i] == value) n++;
        return n;
    }

    /// <summary>
    /// 只保留「源描述与目标文案里都恰好出现 1 次」的数字改动。
    /// 动作数是为了避免歧义替换：『工匠』升级改的是触发次数，但文案里的 1 指的是 HP 阈值，
    /// 若放行就会显示成「生命值小于1（1→2）」这种错话 —— 宁可整条不预览。
    /// </summary>
    private static List<string[]> FilterUnambiguous(string source, string target, List<string[]> changes)
    {
        List<string> sourceNums = SplitNumbers(source);
        List<string> targetNums = SplitNumbers(target);
        List<string[]> kept = new List<string[]>();

        foreach (string[] c in changes)
        {
            if (CountNumber(sourceNums, c[0]) == 1 && CountNumber(targetNums, c[0]) == 1)
                kept.Add(c);
        }
        return kept;
    }

    /// <summary>
    /// 把资产文案里的数值换算到当前层并标出「当前层 → 下一层」的变化。
    /// 资产文案里的数字是 Lv.1 快照，所以 Lv.2 时必须先静默抬到当前值，
    /// 否则会拿「20→30」去替换文案里写死的「10」。
    /// </summary>
    private static string ApplyLevelUpPreview(string text, BlessingEffect effect)
    {
        if (string.IsNullOrEmpty(text)) return text;

        int level = effect.Level;
        string desc1 = effect.GetEffectDescriptionAtLevel(1);
        string now   = effect.GetEffectDescriptionAtLevel(level);
        string next  = effect.GetEffectDescriptionAtLevel(level + 1);

        // 1) 静默换算到当前层
        if (level > 1 && desc1 != now)
        {
            List<string[]> toCurrent = FilterUnambiguous(desc1, text, ExtractNumberChanges(desc1, now));
            if (toCurrent.Count > 0)
                text = ReplaceNumbers(text, toCurrent, annotate: false);
        }

        // 2) 标注当前层 → 下一层
        if (now == next) return text;

        List<string[]> changes = FilterUnambiguous(now, text, ExtractNumberChanges(now, next));
        return changes.Count == 0 ? text : ReplaceNumbers(text, changes, annotate: true);
    }
}
