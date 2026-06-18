using System.Collections.Generic;
using UnityEngine;

// ========================================================================
//  地图数据模型（纯数据，与 JSON 字段一一对应）
// ========================================================================

/// <summary>
/// JSON 地图数据 — 与 floor_XX.json 结构一致。
/// 注意：Newtonsoft.Json 默认只序列化属性（{ get; set; }），因此不能使用 public field。
/// </summary>
[System.Serializable]
public class MapData
{
    public int floor { get; set; }
    public string name { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public PlayerSpawnPos player_spawn { get; set; }
    public List<List<int>> terrain { get; set; }
    public List<List<int>> objects { get; set; }
    public List<List<int>> enemies { get; set; }
    public List<List<int>> items { get; set; }
}

/// <summary>
/// 玩家出生点坐标
/// </summary>
[System.Serializable]
public class PlayerSpawnPos
{
    public int x { get; set; }
    public int y { get; set; }
}

// ========================================================================
//  Inspector 配表条目（供 MapGenerator 序列化使用）
// ========================================================================

/// <summary>
/// ID → Prefab 映射条目，在 Inspector 中配置
/// </summary>
[System.Serializable]
public class PrefabEntry
{
    [Tooltip("与 JSON 数据层中使用的数字 ID 对应")]
    public int id;

    [Tooltip("要生成的 Prefab")]
    public GameObject prefab;

    [Tooltip("生成后的物体名字（Inspector 中显示用）")]
    public string displayName;
}
