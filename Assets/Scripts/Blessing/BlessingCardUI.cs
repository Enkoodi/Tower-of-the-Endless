using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 祝福卡片 UI — 挂载在 BlessingPanel 下的每张 Card 上。
/// 直接拖拽引用，无需字符串查找。
/// </summary>
public class BlessingCardUI : MonoBehaviour
{
    public Button button;
    public Image background;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
}
