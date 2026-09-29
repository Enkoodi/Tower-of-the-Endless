using UnityEngine;

/// <summary>
/// 玩家 HUD — 绑定 DataCanvas 中 LeftPanel / RightPanel 的文本到玩家数据。
/// 挂在 DataCanvas 上，Awake/Start 里按「节点名」查找，无需手动拖引用；
/// 节点名与 PlayerData 的字段一一对应（HPText ↔ playerData.HP …），
/// 因此在面板里增删行不会再让其它行的绑定整体错位。
///
/// LeftPanel ：PlayerImage 头像
///             HPText 生命值 / AttackText 攻击力 / DefenseText 防御力 / AttackCountText 攻击段数 /
///             LifeStealText 吸血 / ReflectDamageText 反伤 / DamageReductionText 减伤 /
///             ManaChargeText 魔力充能 / ManaMaxText 魔力输出 / SpeedText 速度 / GoldText 金币
/// RightPanel：FloorText 当前楼层 / YellowKeyText / BlueKeyText / RedKeyText / AeonKeyText /
///             AleText（麦酒）/ ManaEssenceText（魔力精华）/
///             UpTeleporterText / DownTeleporterText / EnemyHalveItemText（圣水）
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    // 左面板
    private TMPro.TextMeshProUGUI hpText;
    private TMPro.TextMeshProUGUI attackText;
    private TMPro.TextMeshProUGUI defenseText;
    private TMPro.TextMeshProUGUI attackCountText;
    private TMPro.TextMeshProUGUI lifeStealText;
    private TMPro.TextMeshProUGUI reflectDamageText;
    private TMPro.TextMeshProUGUI damageReductionText;
    private TMPro.TextMeshProUGUI manaChargeText;
    private TMPro.TextMeshProUGUI manaMaxText;
    private TMPro.TextMeshProUGUI speedText;
    private TMPro.TextMeshProUGUI goldText;

    // 右面板（FloorText 是标题，其余 9 项是道具数量）
    private TMPro.TextMeshProUGUI floorText;
    private TMPro.TextMeshProUGUI yellowKeyText;
    private TMPro.TextMeshProUGUI blueKeyText;
    private TMPro.TextMeshProUGUI redKeyText;
    private TMPro.TextMeshProUGUI aeonKeyText;
    private TMPro.TextMeshProUGUI aleText;
    private TMPro.TextMeshProUGUI manaEssenceText;
    private TMPro.TextMeshProUGUI upTeleporterText;
    private TMPro.TextMeshProUGUI downTeleporterText;
    private TMPro.TextMeshProUGUI enemyHalveItemText;

    private PlayerData playerData;
    private MapGenerator mapGenerator;

    void Start()
    {
        Transform leftPanel = transform.Find("LeftPanel");
        Transform rightPanel = transform.Find("RightPanel");

        hpText              = FindText(leftPanel, "HPText");
        attackText          = FindText(leftPanel, "AttackText");
        defenseText         = FindText(leftPanel, "DefenseText");
        attackCountText     = FindText(leftPanel, "AttackCountText");
        lifeStealText       = FindText(leftPanel, "LifeStealText");
        reflectDamageText   = FindText(leftPanel, "ReflectDamageText");
        damageReductionText = FindText(leftPanel, "DamageReductionText");
        manaChargeText      = FindText(leftPanel, "ManaChargeText");
        manaMaxText         = FindText(leftPanel, "ManaMaxText");
        speedText           = FindText(leftPanel, "SpeedText");
        goldText            = FindText(leftPanel, "GoldText");

        floorText           = FindText(rightPanel, "FloorText");
        yellowKeyText       = FindText(rightPanel, "YellowKeyText");
        blueKeyText         = FindText(rightPanel, "BlueKeyText");
        redKeyText          = FindText(rightPanel, "RedKeyText");
        aeonKeyText         = FindText(rightPanel, "AeonKeyText");
        aleText             = FindText(rightPanel, "AleText");
        manaEssenceText     = FindText(rightPanel, "ManaEssenceText");
        upTeleporterText    = FindText(rightPanel, "UpTeleporterText");
        downTeleporterText  = FindText(rightPanel, "DownTeleporterText");
        enemyHalveItemText  = FindText(rightPanel, "EnemyHalveItemText");

        playerData = FindFirstObjectByType<PlayerData>();
        mapGenerator = FindFirstObjectByType<MapGenerator>();
    }

    void Update()
    {
        if (playerData == null)
        {
            playerData = FindFirstObjectByType<PlayerData>();
            return;
        }

        if (mapGenerator == null)
            mapGenerator = FindFirstObjectByType<MapGenerator>();

        Refresh();
    }

    void Refresh()
    {
        // 左侧：显示格式为「名称：数值」
        SetLabel(hpText,              "生命值：",  playerData.HP);
        SetLabel(attackText,          "攻击力：",  playerData.Attack);
        SetLabel(defenseText,         "防御力：",  playerData.Defense);
        SetLabel(attackCountText,     "攻击段数：", playerData.AttackCount);
        SetLabel(lifeStealText,       "吸血：",   playerData.LifeSteal);
        SetLabel(reflectDamageText,   "反伤：",   playerData.ReflectDamage);
        SetLabel(damageReductionText, "减伤：",   playerData.DamageReduction);
        SetLabel(manaChargeText,      "魔力充能：", playerData.ManaCharge);
        SetLabel(manaMaxText,         "魔力输出：", playerData.ManaMax);
        SetLabel(speedText,           "速度：",   playerData.Speed);
        SetLabel(goldText,            "金币：",   playerData.Gold);

        // 右侧：显示格式为纯数字
        SetNumber(yellowKeyText,      playerData.GetKeyCount(KeyType.Yellow));
        SetNumber(blueKeyText,        playerData.GetKeyCount(KeyType.Blue));
        SetNumber(redKeyText,         playerData.GetKeyCount(KeyType.Red));
        SetNumber(aeonKeyText,        playerData.GetKeyCount(KeyType.Aeon));
        SetNumber(aleText,            playerData.AleCount);
        SetNumber(manaEssenceText,    playerData.ManaEssenceCount);
        SetNumber(upTeleporterText,   playerData.UpTeleporterCount);
        SetNumber(downTeleporterText, playerData.DownTeleporterCount);
        SetNumber(enemyHalveItemText, playerData.EnemyHalveItemCount);

        RefreshFloor();
    }

    /// <summary>
    /// 右侧面板顶部标题：显示当前楼层名。
    /// 优先取地图 JSON 的 name 字段（如「第24层」），没有 name 时回退为「第{编号}层」。
    /// 与 MonsterManualUI 的楼层标题口径保持一致。
    /// </summary>
    void RefreshFloor()
    {
        if (floorText == null || mapGenerator == null) return;

        MapData map = mapGenerator.CurrentMap;
        if (map == null) return;   // 楼层尚未加载（CurrentFloor == -1），保持占位文本

        string label = string.IsNullOrEmpty(map.name) ? $"第{map.floor}层" : map.name;
        if (floorText.text != label)
            floorText.text = label;
    }

    /// <summary>
    /// 按节点名取文本组件。只认直接子物体（面板层级很浅，不做递归，避免误抓装饰件）。
    /// 找不到只报一次警告、返回 null —— 面板照常显示，只是这一行不刷新，不至于整块 HUD 崩掉。
    /// </summary>
    TMPro.TextMeshProUGUI FindText(Transform parent, string nodeName)
    {
        if (parent == null)
        {
            Debug.LogWarning($"[PlayerHUD] 找不到面板，无法绑定 {nodeName}");
            return null;
        }

        Transform node = parent.Find(nodeName);
        if (node == null)
        {
            Debug.LogWarning($"[PlayerHUD] {parent.name} 下找不到节点 {nodeName}，该行不会刷新");
            return null;
        }

        TMPro.TextMeshProUGUI text = node.GetComponent<TMPro.TextMeshProUGUI>();
        if (text == null)
            Debug.LogWarning($"[PlayerHUD] {parent.name}/{nodeName} 上没有 TextMeshProUGUI 组件");

        return text;
    }

    void SetLabel(TMPro.TextMeshProUGUI text, string label, int value)
    {
        if (text == null) return;
        text.text = label + value;
    }

    void SetNumber(TMPro.TextMeshProUGUI text, int value)
    {
        if (text == null) return;
        text.text = value.ToString();
    }
}
