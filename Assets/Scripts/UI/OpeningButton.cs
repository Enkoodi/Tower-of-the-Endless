using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 菜单按钮的交互效果：
/// - 默认状态：无任何外框/底色，直接透出背景；
/// - 悬停或选中（键盘/手柄导航）时：显示外框；
/// - 按下时：外框变色并轻微缩放，作为点击反馈；
/// - 通过 SetLocked(true) 可锁定为常驻选中（外框一直显示），用于设置页等互斥选择按钮。
/// 外框由 4 条纯色细条（上/下/左/右）拼成，不依赖任何图片资源和九宫格切片，渲染稳定。
/// </summary>
[RequireComponent(typeof(Button))]
public class OpeningButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [Header("外框颜色")]
    [Tooltip("悬停/选中时外框的颜色")]
    [SerializeField] private Color frameColor = new Color(0.92f, 0.55f, 0.2f, 1f);

    [Tooltip("按下时外框的颜色")]
    [SerializeField] private Color pressedFrameColor = new Color(1f, 0.85f, 0.6f, 1f);

    [Header("布局")]
    [Tooltip("外框线条的粗细")]
    [SerializeField] private float frameThickness = 6f;

    [Tooltip("按下时按钮整体缩小的比例")]
    [SerializeField] private float pressedScale = 0.96f;

    [Tooltip("状态切换的插值速度")]
    [SerializeField] private float transitionSpeed = 14f;

    private Button button;
    private Image[] frameBars;
    private Vector3 originalScale;
    private bool isHovered;
    private bool isSelected;
    private bool isPressed;
    private bool isLocked;
    private Color targetColor = Color.clear;
    private Vector3 targetScale;

    private void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
        targetScale = originalScale;

        frameBars = CreateFrame();
        SetFrameColor(Color.clear);
    }

    private void Update()
    {
        if (frameBars == null || frameBars.Length == 0) return;

        Color color = Color.Lerp(frameBars[0].color, targetColor, Time.unscaledDeltaTime * transitionSpeed);
        for (int i = 0; i < frameBars.Length; i++)
            frameBars[i].color = color;

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
    }

    private void OnDisable()
    {
        isHovered = false;
        isSelected = false;
        isPressed = false;
        RefreshState();
        SetFrameColor(Color.clear);
    }

    // ---------------- 状态计算 ----------------
    private void RefreshState()
    {
        bool showFrame = isHovered || isSelected || isLocked;
        targetColor = showFrame ? (isPressed ? pressedFrameColor : frameColor) : Color.clear;
        targetScale = isPressed ? originalScale * pressedScale : originalScale;
    }

    /// <summary>
    /// 锁定/解锁选中状态：锁定时外框常驻显示（即使未悬停/未选中）。
    /// 用于战斗速度等互斥选择按钮，由外部控制器在点击时切换。
    /// </summary>
    public void SetLocked(bool locked)
    {
        isLocked = locked;
        RefreshState();
    }

    // ---------------- 指针 / 选择事件 ----------------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable) isHovered = true;
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        RefreshState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable || eventData.button != PointerEventData.InputButton.Left) return;
        isPressed = true;
        RefreshState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        RefreshState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (button.interactable) isSelected = true;
        RefreshState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshState();
    }

    // ---------------- 外框创建 ----------------
    /// <summary>用 4 条纯色细条拼出按钮外框（无 Sprite 的 Image 渲染为纯白矩形，可任意着色）。</summary>
    private Image[] CreateFrame()
    {
        float half = frameThickness * 0.5f;
        Image[] bars = new Image[4];
        bars[0] = CreateBar("FrameTop",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -half), new Vector2(0f, half));
        bars[1] = CreateBar("FrameBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -half), new Vector2(0f, half));
        bars[2] = CreateBar("FrameLeft",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-half, 0f), new Vector2(half, 0f));
        bars[3] = CreateBar("FrameRight",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-half, 0f), new Vector2(half, 0f));
        return bars;
    }

    private Image CreateBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private void SetFrameColor(Color color)
    {
        if (frameBars == null) return;
        for (int i = 0; i < frameBars.Length; i++)
            frameBars[i].color = color;
    }
}
