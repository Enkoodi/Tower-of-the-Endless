using UnityEngine;
using UnityEditor;

/// <summary>
/// 快捷菜单：MagicTower → Create All Key Pickup Data → 生成黄/蓝/红钥匙数据资产。
/// </summary>
public static class KeyPickupDataCreator
{
    private const string Path = "Assets/Data/Keys/";

    [MenuItem("MagicTower/Create All Key Pickup Data")]
    public static void CreateAll()
    {
        EnsureFolder();

        CreateKeyData("YellowKey", KeyType.Yellow);
        CreateKeyData("BlueKey",   KeyType.Blue);
        CreateKeyData("RedKey",    KeyType.Red);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[KeyPickupDataCreator] 三把钥匙数据资产已创建，路径：Data/Keys/");
    }

    private static void CreateKeyData(string name, KeyType keyType)
    {
        string fullPath = Path + name + ".asset";

        if (AssetDatabase.LoadAssetAtPath<KeyPickupData>(fullPath) != null)
        {
            Debug.Log($"[KeyPickupDataCreator] {name}.asset 已存在，跳过");
            return;
        }

        KeyPickupData data = ScriptableObject.CreateInstance<KeyPickupData>();
        data.keyType = keyType;

        AssetDatabase.CreateAsset(data, fullPath);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Path.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Data", "Keys");
    }
}
