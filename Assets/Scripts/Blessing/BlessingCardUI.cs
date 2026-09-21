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

    [Tooltip("卡片底部的状态行：特殊祝福显示 Lv.N → Lv.N+1，普通祝福显示已获得 N 次")]
    public TextMeshProUGUI statusText;
}
