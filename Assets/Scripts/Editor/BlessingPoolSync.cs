using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 祝福池自动同步 — 池子内容由全工程的 BlessingData 决定，不需要手动增删。
/// 新增/删除/改名祝福资源后自动重扫；菜单：MagicTower → 重新扫描祝福池。
/// </summary>
public static class BlessingPoolSync
{
    /// <summary>扫描全工程的 BlessingData，按 id 升序（= BlessingID 枚举顺序）。</summary>
    public static List<BlessingData> Collect()
    {
        List<BlessingData> list = new List<BlessingData>();

        foreach (string guid in AssetDatabase.FindAssets("t:BlessingData"))
        {
            BlessingData data = AssetDatabase.LoadAssetAtPath<BlessingData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null) list.Add(data);
        }

        // id 相同时按资源名排，保证结果稳定
        list.Sort((a, b) => a.id != b.id ? a.id.CompareTo(b.id) : string.CompareOrdinal(a.name, b.name));
        return list;
    }

    /// <summary>把所有 BlessingPool 的列表刷成当前工程里的祝福；内容没变化就不写盘。</summary>
    public static void RefreshAll(bool verbose = false)
    {
        List<BlessingData> latest = Collect();
        int changed = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:BlessingPool"))
        {
            BlessingPool pool = AssetDatabase.LoadAssetAtPath<BlessingPool>(AssetDatabase.GUIDToAssetPath(guid));
            if (pool == null || SameList(pool.blessings, latest)) continue;

            pool.blessings = new List<BlessingData>(latest);
            EditorUtility.SetDirty(pool);
            changed++;
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[BlessingPoolSync] 已刷新 {changed} 个祝福池（共 {latest.Count} 个祝福）");
        }
        else if (verbose)
        {
            Debug.Log($"[BlessingPoolSync] 祝福池已是最新（共 {latest.Count} 个祝福）");
        }
    }

    private static bool SameList(List<BlessingData> a, List<BlessingData> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    [MenuItem("MagicTower/重新扫描祝福池")]
    private static void MenuRefresh()
    {
        RefreshAll(true);
    }

    /// <summary>脚本重编译后兜底跑一次（内容一致时是空操作）。</summary>
    [InitializeOnLoadMethod]
    private static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += () => RefreshAll();
    }
}

/// <summary>资源变动后自动同步。只看 .asset，避免每张图导入都扫一遍。</summary>
public class BlessingPoolAutoSync : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!HasAssetChange(imported) && !HasAssetChange(deleted) && !HasAssetChange(moved)) return;
        EditorApplication.delayCall += () => BlessingPoolSync.RefreshAll();
    }

    private static bool HasAssetChange(string[] paths)
    {
        foreach (string path in paths)
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".asset")) return true;
        return false;
    }
}
