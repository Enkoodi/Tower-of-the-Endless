using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 对话触发器 — 挂载在对话NPC上。
/// 玩家移动碰撞检测到该组件时，不移动到目标格，而是触发对话脚本。
/// 检测优先级高于 NPCController，确保对话NPC不会被当作商店打开。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("对话数据")]
    [SerializeField] private DialogueLine[] dialogueLines;

    [Header("选项按钮文本")]
    [SerializeField] private string choice1Text = "选项1";
    [SerializeField] private string choice2Text = "选项2";

    [Header("选项1逻辑（选择<选项1>时触发，挂载目标脚本的方法）")]
    [SerializeField] private UnityEvent onChoice1;

    [Header("选项2逻辑（选择<选项2>时触发，挂载目标脚本的方法）")]
    [SerializeField] private UnityEvent onChoice2;

    /// <summary>在地图网格中的坐标（由MapGenerator在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由MapGenerator在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    public DialogueLine[] Lines => dialogueLines;
    public string Choice1Text => choice1Text;
    public string Choice2Text => choice2Text;
    public UnityEvent OnChoice1 => onChoice1;
    public UnityEvent OnChoice2 => onChoice2;
}
