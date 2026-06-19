using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 UI — 挂载在 BattleCanvas/BattlePanel/BattleWindow 上。
/// 按层级图绑定：敌人名、玩家/敌人图片与属性、战斗日志。
/// </summary>
public class BattleUI : MonoBehaviour
{
    [Header("窗口根节点")]
    [SerializeField] private GameObject battleWindow;

    [Header("顶部敌人名称")]
    [SerializeField] private TextMeshProUGUI enemyNameText;

    [Header("左侧玩家")]
    [SerializeField] private Image playerImage;
    [SerializeField] private TextMeshProUGUI playerStatsText;

    [Header("中间战斗日志")]
    [SerializeField] private TextMeshProUGUI battleLogText;

    [Header("右侧敌人")]
    [SerializeField] private Image enemyImage;
    [SerializeField] private TextMeshProUGUI enemyStatsText;

    private List<string> logLines = new List<string>();
    private bool isAnimating = false;

    public bool IsOpen => battleWindow != null && battleWindow.activeInHierarchy;

    void Awake()
    {
        // 默认关闭
        if (battleWindow != null)
            battleWindow.SetActive(false);
    }

    /// <summary>
    /// 打开战斗窗口并初始化双方数据
    /// </summary>
    public void OpenBattle(PlayerData playerData, EnemyController enemy)
    {
        if (battleWindow == null) return;

        logLines.Clear();
        battleWindow.SetActive(true);
        isAnimating = false;

        if (enemyNameText != null)
            enemyNameText.text = enemy != null ? enemy.EnemyName : "？？？";

        UpdatePlayerPanel(playerData);
        UpdateEnemyPanel(enemy);

        // 初始日志
        if(playerData.Speed >= enemy.Speed)
            AddLog($"<color=#7799CC>我方</color>速度更快，获得先手");
    }

    /// <summary>
    /// 关闭战斗窗口
    /// </summary>
    public void CloseBattle()
    {
        if (battleWindow == null) return;
        battleWindow.SetActive(false);
        logLines.Clear();
        isAnimating = false;
    }

    /// <summary>
    /// 更新玩家面板
    /// </summary>
    public void UpdatePlayerPanel(PlayerData playerData)
    {
        if (playerStatsText == null || playerData == null) return;

        playerStatsText.text =
            $"生命值：{playerData.HP}\n" +
            $"攻击力：{playerData.Attack}\n" +
            $"防御力：{playerData.Defense}\n" +
            $"攻击段数：{playerData.AttackCount}\n" +
            $"生命偷取：{playerData.LifeSteal}\n" +
            $"反伤系数：{playerData.ReflectDamage}\n" +
            $"魔力充能：{playerData.ManaCharge}\n" +
            $"速度：{playerData.Speed}";
    }

    /// <summary>
    /// 更新敌人面板
    /// </summary>
    public void UpdateEnemyPanel(EnemyController enemy)
    {
        if (enemyStatsText == null || enemy == null) return;

        enemyStatsText.text =
            $"生命值：{enemy.HP}\n" +
            $"攻击力：{enemy.Attack}\n" +
            $"防御力：{enemy.Defense}\n" +
            $"攻击段数：{enemy.AttackCount}\n" +
            $"生命偷取：{enemy.LifeSteal}\n" +
            $"反伤系数：{enemy.ReflectDamage}\n" +
            $"魔力充能：{enemy.ManaCharge}\n" +
            $"速度：{enemy.Speed}";
    }

    /// <summary>
    /// 向战斗日志添加一行
    /// </summary>
    public void AddLog(string message)
    {
        if (battleLogText == null) return;

        logLines.Add(message);
        if (logLines.Count > 6)
            logLines.RemoveAt(0);

        battleLogText.text = string.Join("\n", logLines);
    }

    /// <summary>
    /// 清空日志
    /// </summary>
    public void ClearLog()
    {
        logLines.Clear();
        if (battleLogText != null)
            battleLogText.text = string.Empty;
    }

    /// <summary>
    /// 直接设置玩家/敌人图片（可选）
    /// </summary>
    public void SetPlayerSprite(Sprite sprite)
    {
        if (playerImage != null && sprite != null)
            playerImage.sprite = sprite;
    }

    public void SetEnemySprite(Sprite sprite)
    {
        if (enemyImage != null && sprite != null)
            enemyImage.sprite = sprite;
    }
}
