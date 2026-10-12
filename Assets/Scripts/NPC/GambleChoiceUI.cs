using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>赌场二选一：展示赔率、掷骰结算，结果显示后点另一按钮关闭。</summary>
public class GambleChoiceUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button firstButton;
    [SerializeField] private Button secondButton;
    [SerializeField] private TextMeshProUGUI firstLabel;
    [SerializeField] private TextMeshProUGUI secondLabel;

    public static GambleChoiceUI Instance { get; private set; }
    public static event System.Action OnPanelOpen;
    public static event System.Action OnPanelClose;

    private PlayerData player;
    private Offer firstOffer;
    private Offer secondOffer;
    private bool resultShown;

    private struct Offer
    {
        public int rarePercent;
        public int rareAmount;    // 正数=获得，负数=失去
        public int commonAmount;

        public int ExpectedHundred => rarePercent * rareAmount + (100 - rarePercent) * commonAmount;
    }

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (firstButton != null) firstButton.onClick.AddListener(() => Choose(true));
        if (secondButton != null) secondButton.onClick.AddListener(() => Choose(false));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open(PlayerData currentPlayer)
    {
        if (currentPlayer == null || panel == null || firstButton == null || secondButton == null
            || firstLabel == null || secondLabel == null)
        {
            Debug.LogError("[GambleChoiceUI] 玩家或 ChoiceCanvas 引用未配置");
            return;
        }
        if (panel.activeSelf) return;

        player = currentPlayer;
        resultShown = false;
        GenerateOffers();
        firstLabel.text = Describe(firstOffer);
        secondLabel.text = Describe(secondOffer);
        firstButton.interactable = true;
        secondButton.interactable = true;
        panel.SetActive(true);
        OnPanelOpen?.Invoke();
    }

    private void Choose(bool first)
    {
        if (panel == null || !panel.activeSelf || player == null) return;
        if (resultShown)
        {
            Close();
            return;
        }

        Offer offer = first ? firstOffer : secondOffer;
        int amount = Random.Range(0, 100) < offer.rarePercent ? offer.rareAmount : offer.commonAmount;
        int actual;
        if (amount >= 0)
        {
            // 赌局按牌面金额结算，不受金币倍率影响。
            actual = Mathf.Min(amount, int.MaxValue - player.Gold);
            player.SetGold(player.Gold + actual);
        }
        else
        {
            actual = -Mathf.Min(-amount, player.Gold);
            player.SpendGold(-actual);
        }

        resultShown = true;
        if (first)
        {
            firstLabel.text = $"结果：{DescribeAmount(actual)}\n当前金币：{player.Gold}";
            firstButton.interactable = false;
            secondLabel.text = "关闭";
        }
        else
        {
            secondLabel.text = $"结果：{DescribeAmount(actual)}\n当前金币：{player.Gold}";
            secondButton.interactable = false;
            firstLabel.text = "关闭";
        }
        Debug.Log($"[Shop3Gamble] {DescribeAmount(actual)}（当前金币 {player.Gold}）");
        PlayerLog.Note($"赌博 {DescribeAmount(actual)}（现 {player.Gold} 金币）");
    }

    private void Close()
    {
        if (panel == null || !panel.activeSelf) return;
        panel.SetActive(false);
        player = null;
        OnPanelClose?.Invoke();
    }

    private static string Describe(Offer offer)
    {
        return $"{offer.rarePercent}%{DescribeAmount(offer.rareAmount)}\n" +
               $"{100 - offer.rarePercent}%{DescribeAmount(offer.commonAmount)}";
    }

    private static string DescribeAmount(int amount)
    {
        return $"{(amount >= 0 ? "获得" : "失去")}{Mathf.Abs(amount)}金币";
    }

    // 概率和金额分别由函数生成；逆幂曲线让大金额集中在小概率一侧。
    private static int SampleRarePercent()
    {
        return 1 + Mathf.FloorToInt(Mathf.Pow(Random.value, 1.4f) * 40f);
    }

    private static int AmountCap(int probability)
    {
        int cap = Mathf.RoundToInt(10f + 1990f * Mathf.Pow((100f - probability) / 99f, 1.65f));
        return probability > 20 ? Mathf.Min(cap, 1000) : cap;
    }

    private static int SampleAmount(int probability)
    {
        int cap = AmountCap(probability) / 10 * 10;
        return Mathf.Clamp(Mathf.RoundToInt((0.25f + 0.75f * Random.value) * cap / 10f) * 10, 10, cap);
    }

    private static Offer GenerateOffer()
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            int rare = SampleRarePercent();
            int common = 100 - rare;
            int kind = Random.Range(0, 100);
            int commonAmount;
            int rareAmount;

            if (kind < 8) // 两个都是失去：额度小，避免拉低整轮期望过多。
            {
                commonAmount = -Mathf.Min(30, SampleAmount(common));
                rareAmount = -Mathf.Max(-commonAmount + 10, Mathf.Min(60, SampleAmount(rare)));
            }
            else if (kind < 11) // 偶尔两个都是获得；另一张牌须补足整体负期望。
            {
                commonAmount = Mathf.Min(20, SampleAmount(common));
                rareAmount = Mathf.Max(commonAmount + 10, Mathf.Min(40, SampleAmount(rare)));
            }
            else if (kind < 36 && rare <= 30) // 小概率赢，常见结果亏。
            {
                commonAmount = -SampleAmount(common);
                int edge = Random.Range(1, 19);
                int maxWin = Mathf.Min(AmountCap(rare) / 10 * 10,
                    ((common * -commonAmount - edge * 100) / (rare * 10)) * 10);
                if (maxWin <= -commonAmount) continue;
                rareAmount = Mathf.Max(-commonAmount + 10, maxWin - Random.Range(0, 4) * 10);
            }
            else // 常见结果小赚，小概率大亏。
            {
                commonAmount = SampleAmount(common);
                int edge = Random.Range(1, 23);
                int minLoss = (common * commonAmount + edge * 100 + rare * 10 - 1) / (rare * 10) * 10;
                if (minLoss <= commonAmount || minLoss > AmountCap(rare) / 10 * 10) continue;
                rareAmount = -minLoss;
            }

            return new Offer { rarePercent = rare, rareAmount = rareAmount, commonAmount = commonAmount };
        }

        return new Offer { rarePercent = 10, rareAmount = -20, commonAmount = -10 };
    }

    private void GenerateOffers()
    {
        // 每轮两张牌的等权平均期望为 -2.5 ~ -30 金币。
        for (int attempt = 0; attempt < 500; attempt++)
        {
            Offer a = GenerateOffer();
            Offer b = GenerateOffer();
            int combined = a.ExpectedHundred + b.ExpectedHundred;
            if (combined < -6000 || combined > -500) continue;
            firstOffer = a;
            secondOffer = b;
            return;
        }

        firstOffer = new Offer { rarePercent = 10, rareAmount = -20, commonAmount = -10 };
        secondOffer = new Offer { rarePercent = 10, rareAmount = -20, commonAmount = -10 };
    }
}
