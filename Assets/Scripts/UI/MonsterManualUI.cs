using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物手册 — 查看当前楼层所有敌人的数据及战斗所需生命值。
/// 按 M 键打开/关闭。
/// </summary>
public class MonsterManualUI : MonoBehaviour
{
    [Header("窗口根节点")]
    [SerializeField] private GameObject panelRoot;

    [Header("Scroll View")]
    [SerializeField] private Transform contentTransform;
    [SerializeField] private GameObject entryPrefab;

    [Header("楼层信息")]
    [SerializeField] private TMPro.TextMeshProUGUI floorTitleText;

    /// <summary>面板打开/关闭事件（供 PlayerMove 订阅以锁定/解锁移动）</summary>
    public static event System.Action OnPanelOpen;
    public static event System.Action OnPanelClose;

    public bool IsOpen => panelRoot != null && panelRoot.activeInHierarchy;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    // ========================================================================
    //  公开接口
    // ========================================================================

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        OnPanelOpen?.Invoke();
        RefreshContent();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        OnPanelClose?.Invoke();
    }

    // ========================================================================
    //  核心逻辑
    // ========================================================================

    private void RefreshContent()
    {
        if (contentTransform == null || entryPrefab == null) return;

        // 清除旧条目
        for (int i = contentTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(contentTransform.GetChild(i).gameObject);
        }

        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        PlayerData player = FindAnyObjectByType<PlayerData>();

        if (mapGen == null || player == null) return;

        // 楼层标题
        int floor = mapGen.CurrentFloor;
        if (floorTitleText != null)
        {
            string floorName = mapGen.CurrentMap?.name ?? $"第{floor}层";
            floorTitleText.text = $"{floorName} — 怪物手册";
        }

        // 获取当前楼层所有唯一敌人
        List<int> enemyIDs = mapGen.GetCurrentFloorUniqueEnemyIDs();
        if (enemyIDs.Count == 0)
        {
            Debug.Log("[MonsterManual] 当前楼层没有敌人");
            return;
        }

        // 按攻击力排序（高攻在前，方便玩家判断威胁）
        List<EnemyStats> enemies = new List<EnemyStats>();
        foreach (int id in enemyIDs)
        {
            EnemyStats stats = mapGen.GetEnemyStatsByID(id);
            if (stats != null) enemies.Add(stats);
        }
        enemies.Sort((a, b) => b.attack.CompareTo(a.attack));

        // 生成条目
        foreach (var enemy in enemies)
        {
            GameObject entryGO = Instantiate(entryPrefab, contentTransform);
            MonsterManualEntryUI entryUI = entryGO.GetComponent<MonsterManualEntryUI>();
            if (entryUI != null)
            {
                entryUI.Setup(enemy, player);
            }
        }

        Debug.Log($"[MonsterManual] 已刷新，当前楼层 {floor}，共 {enemies.Count} 种敌人");
    }
}
