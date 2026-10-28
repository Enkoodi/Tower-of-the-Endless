using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opening 场景右上角的设置图标（纯图形，无文字）。
/// 点击等同按 ESC：把上一场景记成 Opening，然后切到 Setting。
/// </summary>
[RequireComponent(typeof(Button))]
public class OpeningSettingsButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Open);
    }

    private void Open()
    {
        if (SettingsToggle.Instance != null)
        {
            SettingsToggle.Instance.OpenSettingsFromButton();
        }
    }
}
