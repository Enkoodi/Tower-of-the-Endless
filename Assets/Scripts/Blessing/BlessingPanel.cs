using UnityEngine;
using System.Collections.Generic;

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
            card.descText.text = data.description;

        card.button.onClick.RemoveAllListeners();
        card.button.onClick.AddListener(() =>
        {
            Hide();
            onChosenCallback?.Invoke(data);
        });
    }
}
