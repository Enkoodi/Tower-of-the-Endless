using UnityEngine;

/// <summary>
/// 单句对话数据
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Header("对话内容")]
    public string speakerName = "";

    [TextArea(3, 10)]
    public string content = "";

    [Header("结束后显示选项按钮（仅无下一句时生效）")]
    public bool showChoices;

    [Header("下一句（分支跳转，可选）")]
    [Tooltip("-1=按数组顺序显示下一句；>=0=跳转到指定索引；-2=本句结束后直接结束对话")]
    public int nextLineIndex = -1;
}
