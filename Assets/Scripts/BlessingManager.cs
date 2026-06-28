using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 祝福管理器 — 单例，挂载在场景中。
/// 控制 BlessingPanel 的弹出、祝福选择与应用流程。
/// </summary>
public class BlessingManager : MonoBehaviour
{
    public static BlessingManager Instance { get; private set; }

    public static event System.Action OnPanelOpen;
    public static event System.Action OnPanelClose;

    [Header("池子")]
    [SerializeField] private BlessingPool blessingPool;

    [Header("UI")]
    [SerializeField] private BlessingPanel blessingPanel;

    [Header("抽取数量")]
    [SerializeField] private int drawCount = 3;

    private PlayerData currentPlayerData;
    private BlessingPickup currentPickup;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 由 BlessingPickup / PlayerMove 调用，弹出祝福选择面板。
    /// </summary>
    public void Show(PlayerData playerData, BlessingPickup pickup = null)
    {
        if (blessingPool == null)
        {
            Debug.LogError("[BlessingManager] BlessingPool 未设置！");
            return;
        }

        if (blessingPanel == null)
        {
            Debug.LogError("[BlessingManager] BlessingPanel 未设置！");
            return;
        }

        currentPlayerData = playerData;
        currentPickup = pickup;

        // 抽取祝福
        List<BlessingData> drawn = blessingPool.Draw(drawCount);
        if (drawn.Count == 0)
        {
            Debug.LogWarning("[BlessingManager] 抽取结果为空！");
            Cleanup();
            return;
        }

        // 弹出面板
        blessingPanel.Show(drawn, OnBlessingChosen);
        OnPanelOpen?.Invoke();
    }

    private void OnBlessingChosen(BlessingData chosen)
    {
        if (currentPlayerData != null && chosen != null)
        {
            currentPlayerData.ApplyBlessing(chosen);
            Debug.Log($"[BlessingManager] 选择了祝福：{chosen.blessingName}");
        }

        Cleanup();
    }

    private void Cleanup()
    {
        currentPlayerData = null;

        if (currentPickup != null)
        {
            Destroy(currentPickup.gameObject);
            currentPickup = null;
        }

        OnPanelClose?.Invoke();
    }
}
