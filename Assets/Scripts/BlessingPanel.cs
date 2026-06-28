using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 祝福选择面板 — 挂载在 BlessingPanel 根对象上。
/// Show() 时显示 3 张卡片，每张卡片的背景由 BlessingData.backgroundSprite 决定。
/// </summary>
public class BlessingPanel : MonoBehaviour
{
    [Header("面板根对象")]
    [SerializeField] private GameObject panelRoot;

    [Header("卡片容器（3 张卡片的父节点）")]
    [SerializeField] private Transform cardContainer;

    // ============================================================
    //  卡片引用（运行时从 cardContainer 的子对象获取）
    // ============================================================
    private CardEntry[] cards;

    private System.Action<BlessingData> onChosenCallback;

    void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        // 收集 3 张卡片
        cards = new CardEntry[cardContainer.childCount];
        for (int i = 0; i < cardContainer.childCount && i < cards.Length; i++)
        {
            Transform child = cardContainer.GetChild(i);
            cards[i] = new CardEntry
            {
                button = child.GetComponent<Button>(),
                background = child.Find("RarityBackground")?.GetComponent<Image>(),
                nameText = child.Find("BlessingName")?.GetComponent<TextMeshProUGUI>(),
                descText = child.Find("BlessingDesc")?.GetComponent<TextMeshProUGUI>(),
            };
        }
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
                cards[i].button.gameObject.SetActive(true);
            }
            else
            {
                cards[i].button.gameObject.SetActive(false);
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

    private void FillCard(CardEntry card, BlessingData data)
    {
        // 背景：直接从数据中取
        if (card.background != null && data.backgroundSprite != null)
            card.background.sprite = data.backgroundSprite;

        // 文字
        if (card.nameText != null)
            card.nameText.text = data.blessingName;
        if (card.descText != null)
            card.descText.text = data.description;

        // 点击回调
        card.button.onClick.RemoveAllListeners();
        card.button.onClick.AddListener(() =>
        {
            Hide();
            onChosenCallback?.Invoke(data);
        });
    }

    [System.Serializable]
    private class CardEntry
    {
        public Button button;
        public Image background;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descText;
    }
}
