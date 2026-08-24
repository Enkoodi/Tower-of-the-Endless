using UnityEngine;

/// <summary>
/// 玩家 HUD — 绑定 DataCanvas 中 LeftPanel / RightPanel 的文本到玩家数据。
/// 挂在 DataCanvas 上，运行时按子物体顺序自动绑定，无需手动拖引用。
/// LeftPanel（从上到下）：生命值、攻击力、防御力、攻击段数、吸血、反伤、减伤、魔力充能、魔力输出、速度、金币。
/// RightPanel（从上到下）：黄钥匙、蓝钥匙、红钥匙、移涌之钥、上传送器、下传送器、圣水。
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    private TMPro.TextMeshProUGUI[] leftTexts;   // 11 项：生命值 → 金币
    private TMPro.TextMeshProUGUI[] rightTexts;  // 7 项：黄钥匙 → 圣水
    private PlayerData playerData;

    void Start()
    {
        Transform leftPanel = transform.Find("LeftPanel");
        Transform rightPanel = transform.Find("RightPanel");

        if (leftPanel != null)
            leftTexts = leftPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>();

        if (rightPanel != null)
            rightTexts = rightPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>();

        playerData = FindFirstObjectByType<PlayerData>();
    }

    void Update()
    {
        if (playerData == null)
        {
            playerData = FindFirstObjectByType<PlayerData>();
            return;
        }

        Refresh();
    }

    void Refresh()
    {
        // 左侧：显示格式为「名称：数值」
        if (leftTexts != null && leftTexts.Length >= 11)
        {
            SetLabel(leftTexts[0], "生命值：", playerData.HP);
            SetLabel(leftTexts[1], "攻击力：", playerData.Attack);
            SetLabel(leftTexts[2], "防御力：", playerData.Defense);
            SetLabel(leftTexts[3], "攻击段数：", playerData.AttackCount);
            SetLabel(leftTexts[4], "吸血：", playerData.LifeSteal);
            SetLabel(leftTexts[5], "反伤：", playerData.ReflectDamage);
            SetLabel(leftTexts[6], "减伤：", playerData.DamageReduction);
            SetLabel(leftTexts[7], "魔力充能：", playerData.ManaCharge);
            SetLabel(leftTexts[8], "魔力输出：", playerData.ManaMax);
            SetLabel(leftTexts[9], "速度：", playerData.Speed);
            SetLabel(leftTexts[10], "金币：", playerData.Gold);
        }

        // 右侧：显示格式为纯数字
        if (rightTexts != null && rightTexts.Length >= 7)
        {
            SetNumber(rightTexts[0], playerData.GetKeyCount(KeyType.Yellow));
            SetNumber(rightTexts[1], playerData.GetKeyCount(KeyType.Blue));
            SetNumber(rightTexts[2], playerData.GetKeyCount(KeyType.Red));
            SetNumber(rightTexts[3], playerData.GetKeyCount(KeyType.Aeon));
            SetNumber(rightTexts[4], playerData.UpTeleporterCount);
            SetNumber(rightTexts[5], playerData.DownTeleporterCount);
            SetNumber(rightTexts[6], playerData.EnemyHalveItemCount);
        }
    }

    void SetLabel(TMPro.TextMeshProUGUI text, string label, int value)
    {
        text.text = label + value;
    }

    void SetNumber(TMPro.TextMeshProUGUI text, int value)
    {
        text.text = value.ToString();
    }
}
