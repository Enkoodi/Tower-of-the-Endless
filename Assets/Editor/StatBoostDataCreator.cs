using UnityEngine;
using UnityEditor;

/// <summary>
/// 快捷菜单：MagicTower → Create All Stat Boost Data → 生成 5 种属性增益数据资产。
/// </summary>
public static class StatBoostDataCreator
{
    private const string Path = "Assets/Data/StatBoosts/";

    [MenuItem("MagicTower/Create All Stat Boost Data")]
    public static void CreateAll()
    {
        EnsureFolder();

        Create("AttackBoost",     StatBoostType.Attack,     5, "攻击力 +5");
        Create("DefenseBoost",    StatBoostType.Defense,    3, "防御力 +3");
        Create("ManaMaxBoost",    StatBoostType.ManaMax,   10, "魔力上限 +10");
        Create("ManaChargeBoost", StatBoostType.ManaCharge, 5, "魔力充能 +5");
        Create("SpeedBoost",      StatBoostType.Speed,      5, "速度 +5");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[StatBoostDataCreator] 5 种属性增益资产已创建，路径：Data/StatBoosts/");
    }

    private static void Create(string name, StatBoostType type, int value, string displayName)
    {
        string fullPath = Path + name + ".asset";

        if (AssetDatabase.LoadAssetAtPath<StatBoostData>(fullPath) != null)
        {
            Debug.Log($"[StatBoostDataCreator] {name}.asset 已存在，跳过");
            return;
        }

        StatBoostData data = ScriptableObject.CreateInstance<StatBoostData>();
        data.boostType = type;
        data.value = value;
        data.displayName = displayName;

        AssetDatabase.CreateAsset(data, fullPath);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Path.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Data", "StatBoosts");
    }
}
