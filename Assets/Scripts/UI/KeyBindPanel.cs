using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 设置页的按键列表：12 行（左列 6 行只读、右列 6 行含 5 个可改），
/// 点键位按钮 → 变「按任意键」→ 抓到键即改绑（与其它可改操作冲突时交换）。
/// 行内容全部取自 <see cref="KeyBindings.Rows"/>，场景里只摆位置、不写文案。
/// </summary>
public class KeyBindPanel : MonoBehaviour
{
    [System.Serializable]
    public class RowRef
    {
        public TextMeshProUGUI label;
        public TextMeshProUGUI key;
        public Button button;
    }

    [Header("12 行（顺序同 KeyBindings.Rows）")]
    [SerializeField] private RowRef[] rows = new RowRef[12];

    [Header("恢复默认")]
    [SerializeField] private Button resetButton;

    private static KeyCode[] allKeys;

    private int capturingIndex = -1;
    private int conflictIndex = -1;
    private float conflictUntil;

    private void Awake()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null || rows[i].button == null) continue;

            int index = i;
            rows[i].button.onClick.AddListener(() => BeginCapture(index));
        }

        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);

        RefreshAll();
    }

    private void Update()
    {
        if (capturingIndex < 0)
        {
            if (conflictIndex >= 0 && Time.unscaledTime >= conflictUntil) EndConflict();
            return;
        }

        if (allKeys == null) allKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));

        // 只在这一帧里找按下的那个键；鼠标移动之类不会被 GetKeyDown 命中
        for (int i = 0; i < allKeys.Length; i++)
        {
            KeyCode key = allKeys[i];
            if (key == KeyCode.None || !Input.GetKeyDown(key)) continue;

            OnKeyPressed(key);
            return;
        }
    }

    // ============================================================
    //  捕获
    // ============================================================

    private void BeginCapture(int index)
    {
        if (index < 0 || index >= KeyBindings.Rows.Length) return;
        if (index >= rows.Length || rows[index] == null || rows[index].key == null) return;
        if (KeyBindings.Rows[index].Action == null) return;   // 只读行

        CancelConflictIfAny();

        capturingIndex = index;
        rows[index].key.text = "按任意键";
        SetButtonsInteractable(false);

        // 捕获期间禁掉 UI 导航：否则方向键 / Enter 会顺带移动选中项、触发 Submit
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
    }

    private void OnKeyPressed(KeyCode key)
    {
        if (key == KeyCode.Escape)   // 取消
        {
            EndCapture();
            RefreshAll();
            return;
        }

        if (KeyBindings.IsReserved(key))
        {
            // 保留键（移动 / 跳层 / 手册 / ESC / 鼠标左右中键 / 修饰键）：提示后继续等下一个键
            conflictIndex = capturingIndex;
            conflictUntil = Time.unscaledTime + 0.8f;
            rows[capturingIndex].key.text = "不可用";
            return;
        }

        KeyAction action = KeyBindings.Rows[capturingIndex].Action.Value;
        KeyBindings.Rebind(action, key);

        EndCapture();
        RefreshAll();
    }

    private void EndCapture()
    {
        capturingIndex = -1;
        conflictIndex = -1;
        SetButtonsInteractable(true);

        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
    }

    private void EndConflict()
    {
        if (conflictIndex >= 0 && conflictIndex < rows.Length && rows[conflictIndex] != null)
            rows[conflictIndex].key.text = "按任意键";

        conflictIndex = -1;
    }

    private void CancelConflictIfAny()
    {
        if (conflictIndex >= 0) EndConflict();
    }

    private void SetButtonsInteractable(bool value)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null || rows[i].button == null) continue;
            if (KeyBindings.Rows[i].Action == null) continue;   // 只读行本来就不可点
            rows[i].button.interactable = value;
        }
    }

    // ============================================================
    //  刷新
    // ============================================================

    private void ResetAll()
    {
        if (capturingIndex >= 0) EndCapture();

        KeyBindings.ResetToDefaults();
        RefreshAll();
    }

    private void RefreshAll()
    {
        int count = Mathf.Min(rows.Length, KeyBindings.Rows.Length);

        for (int i = 0; i < count; i++)
        {
            if (rows[i] == null || rows[i].key == null) continue;

            KeyBindings.RowInfo info = KeyBindings.Rows[i];
            rows[i].key.text = info.Action.HasValue
                ? KeyBindings.DisplayName(info.Action.Value)
                : info.FixedKeys;

            if (rows[i].label != null && !string.IsNullOrEmpty(info.Label))
                rows[i].label.text = info.Label;
        }
    }
}
