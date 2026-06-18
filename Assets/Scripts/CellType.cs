using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CellType
{
    Empty,      // 空地
    Wall,       // 墙（不可通行）
    Door,       // 门（需要钥匙打开）
    Enemy,      // 敌人（触发战斗）
    NPC,        // NPC（触发对话）
    Item,       // 道具（自动拾取）
    Stairs,     // 楼梯（切换楼层）
    Event,      // 事件（触发特殊效果）
}