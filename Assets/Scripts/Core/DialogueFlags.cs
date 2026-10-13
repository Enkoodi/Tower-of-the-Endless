using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话标记 —— 存档级的字符串标记集合，记录「某个 NPC 的对话收尾逻辑已经结算过」。
/// 静态类（不挂场景，Unity 运行中也能改）。存档/读档/新游戏由 SaveManager 与 SettingMenu 负责整份搬。
/// </summary>
public static class DialogueFlags
{
    private static readonly HashSet<string> flags = new HashSet<string>();

    /// <summary>某个标记第一次被设置时触发。</summary>
    public static event System.Action<string> OnFlagSet;

    /// <summary>设置标记。返回 true 表示这次是第一次设置。</summary>
    public static bool Set(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!flags.Add(id)) return false;

        Debug.Log($"[DialogueFlags] 已记录对话标记：{id}");
        OnFlagSet?.Invoke(id);
        return true;
    }

    public static bool IsSet(string id) => !string.IsNullOrEmpty(id) && flags.Contains(id);

    /// <summary>供存档读取。</summary>
    public static List<string> GetIds() => new List<string>(flags);

    /// <summary>读档恢复（覆盖现有数据；ids 允许为 null，旧存档没有该字段）。</summary>
    public static void Restore(List<string> ids)
    {
        flags.Clear();
        if (ids == null) return;

        foreach (string id in ids)
            if (!string.IsNullOrEmpty(id))
                flags.Add(id);
    }

    /// <summary>新游戏清空。</summary>
    public static void Clear() => flags.Clear();
}
