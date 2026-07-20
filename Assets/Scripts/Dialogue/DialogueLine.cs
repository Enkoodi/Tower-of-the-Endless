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

    [Header("结束后显示选项按钮（仅最后一句生效）")]
    public bool showChoices;
}
