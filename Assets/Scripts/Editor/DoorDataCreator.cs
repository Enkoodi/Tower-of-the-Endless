using UnityEngine;
using UnityEditor;

/// <summary>
/// 快捷菜单：Magictower → Create All Door Data → 在 Data/Doors/ 下生成五扇门的数据资产。
/// </summary>
public static class DoorDataCreator
{
    private const string Path = "Assets/Data/Doors/";

    /// <summary>移涌之门所需的 Aeon 钥匙数量（修改此处后重新生成即可）</summary>
    private const int AeonDoorRequiredKeys = 3;

    [MenuItem("MagicTower/Create All Door Data")]
    public static void CreateAll()
    {
        EnsureFolder();

        CreateDoor("YellowDoor",  "黄之门", KeyType.Yellow, true);
        CreateDoor("BlueDoor",    "蓝之门", KeyType.Blue,   true);
        CreateDoor("RedDoor",     "红之门", KeyType.Red,    true);
        CreateDoor("PsycheDoor",  "魂之门", KeyType.Psyche, false, 100);
        CreateDoor("AeonDoor",    "移涌之门", KeyType.Aeon,   false, requiredKeyCount: AeonDoorRequiredKeys);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DoorDataCreator] 五扇门数据资产已创建，路径：Data/Doors/");
    }

    private static void CreateDoor(string name, string doorName, KeyType keyType, bool consume, int healthCost = 0, int requiredKeyCount = 0)
    {
        string fullPath = Path + name + ".asset";

        // 已存在则跳过
        if (AssetDatabase.LoadAssetAtPath<DoorData>(fullPath) != null)
        {
            Debug.Log($"[DoorDataCreator] {name}.asset 已存在，跳过");
            return;
        }

        DoorData data = ScriptableObject.CreateInstance<DoorData>();
        data.doorName = doorName;
        data.requiredKeyType = keyType;
        data.consumeKey = consume;
        data.healthCost = healthCost;
        data.requiredKeyCount = requiredKeyCount;

        AssetDatabase.CreateAsset(data, fullPath);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Path.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Data", "Doors");
    }
}
