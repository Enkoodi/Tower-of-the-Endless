using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>开场分支对「对话结束标记」的判断方式。</summary>
public enum DialogueFlagCondition
{
    /// <summary>不判断</summary>
    Ignore,

    /// <summary>本 NPC 的 endFlagId 已标记时进入</summary>
    Set,

    /// <summary>本 NPC 的 endFlagId 未标记时进入</summary>
    NotSet,
}

/// <summary>
/// 对话开场分支 — 根据特殊敌人击败信号 / 对话结束标记决定对话从哪一句开始。
/// 按顺序匹配，第一个命中的分支生效；全部未命中则从第 0 句开始。
/// </summary>
[System.Serializable]
public class DialogueEntryBranch
{
    [Tooltip("匹配的特殊敌人 ID（来自 EnemyStats.specialEnemyId）。留空表示无条件命中（可作为兜底分支）。")]
    public string specialEnemyId = "";

    [Tooltip("true=该特殊敌人已被击败时进入；false=该特殊敌人未被击败时进入")]
    public bool requireDefeated = true;

    [Tooltip("按本 NPC 的 endFlagId 判断：Ignore=不判断；Set=已标记时进入；NotSet=未标记时进入")]
    public DialogueFlagCondition endFlagCondition = DialogueFlagCondition.Ignore;

    [Tooltip("命中此条件时，对话从该句索引开始播放")]
    public int startLineIndex = 0;
}

/// <summary>
/// 对话触发器 — 挂载在对话NPC上。
/// 玩家移动碰撞检测到该组件时，不移动到目标格，而是触发对话脚本。
/// 检测优先级高于 NPCController，确保对话NPC不会被当作商店打开。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class DialogueTrigger : MonoBehaviour, IFloorEntity
{
    [Header("对话数据")]
    [SerializeField] private DialogueLine[] dialogueLines;

    [Header("开场分支（按顺序匹配，命中则从对应句开始；留空则从第 0 句开始）")]
    [SerializeField] private List<DialogueEntryBranch> entryBranches = new List<DialogueEntryBranch>();

    [Header("选项按钮文本")]
    [SerializeField] private string choice1Text = "选项1";
    [SerializeField] private string choice2Text = "选项2";

    [Header("选项1逻辑（选择<选项1>时触发，挂载目标脚本的方法）")]
    [SerializeField] private UnityEvent onChoice1;

    [Header("选项2逻辑（选择<选项2>时触发，挂载目标脚本的方法）")]
    [SerializeField] private UnityEvent onChoice2;

    [Header("选项后续对话（-1=结束对话；>=0=点击后从该句继续。该选项若开战，则改到战斗胜利后再继续）")]
    [SerializeField] private int choice1NextLineIndex = -1;
    [SerializeField] private int choice2NextLineIndex = -1;

    [Header("对话结束时执行（默认只在没点过选项时触发）")]
    [SerializeField] private UnityEvent onDialogueEnd;

    [Header("勾选后：点过选项也算正常结束")]
    [SerializeField] private bool endActionAfterChoice;

    [Header("结束标记（非空=只结算一次，并可作开场分支条件）")]
    [SerializeField] private string endFlagId = "";

    /// <summary>在地图网格中的坐标（由MapGenerator在生成时设置）</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号（由MapGenerator在生成时设置）</summary>
    [HideInInspector] public int floorNumber;

    /// <summary>IFloorEntity：写入所属楼层与网格坐标（由 MapGenerator 调用）。</summary>
    public void SetFloorInfo(Vector2Int gridPos, int floor)
    {
        gridPosition = gridPos;
        floorNumber = floor;
    }

    public DialogueLine[] Lines => dialogueLines;
    public List<DialogueEntryBranch> EntryBranches => entryBranches;
    public string Choice1Text => choice1Text;
    public string Choice2Text => choice2Text;
    public UnityEvent OnChoice1 => onChoice1;
    public UnityEvent OnChoice2 => onChoice2;
    public int Choice1NextLineIndex => choice1NextLineIndex;
    public int Choice2NextLineIndex => choice2NextLineIndex;
    public UnityEvent OnDialogueEnd => onDialogueEnd;
    public bool EndActionAfterChoice => endActionAfterChoice;
    public string EndFlagId => endFlagId;
}
