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

    // ============================================================
    //  特殊祝福效果管理
    // ============================================================

    /// <summary>
    /// 所有已获得的特殊祝福效果。Key = BlessingID 的字符串名（如 "FuryHeart"）。
    /// </summary>
    private Dictionary<string, BlessingEffect> activeEffects = new Dictionary<string, BlessingEffect>();

    // ============================================================
    //  调试（Inspector 可见）
    // ============================================================

    [Header("调试")]
    [SerializeField]
    [ContextMenuItem("添加此效果", nameof(DebugAddEffect))]
    private BlessingID debugAddEffectId = BlessingID.None;
    [SerializeField] private List<string> activeEffectInfos = new List<string>();

    private void RefreshInspectorList()
    {
        activeEffectInfos.Clear();
        foreach (var kv in activeEffects)
            activeEffectInfos.Add($"{kv.Key} (Lv.{kv.Value.Level})");
    }

    /// <summary>
    /// 在 Inspector 中右键 debugAddEffectId 下拉框 → 添加此效果。
    /// </summary>
    public void DebugAddEffect()
    {
        if (debugAddEffectId == BlessingID.None)
        {
            Debug.LogWarning("[BlessingManager] 请先在「调试→Debug Add Effect Id」中选择一个祝福 ID");
            return;
        }

        BlessingEffect effect = debugAddEffectId switch
        {
            BlessingID.BythosBlessing => new BythosBlessingEffect(),
            BlessingID.AgapeBlessing => new AgapeBlessingEffect(),
            BlessingID.AletheiaBlessing => new AletheiaBlessingEffect(),
            BlessingID.Allotrioi => new AllotrioiEffect(),
            BlessingID.CharisBlessing => new CharisBlessingEffect(),
            BlessingID.DemiurgeBlessing => new DemiurgeBlessingEffect(),
            BlessingID.GnosisBlessing => new GnosisBlessingEffect(),
            BlessingID.KabbalahTree => new KabbalahTreeEffect(),
            BlessingID.Longinus => new LonginusEffect(),
            BlessingID.SigeBlessing => new SigeBlessingEffect(),
            BlessingID.SophiaBlessing => new SophiaBlessingEffect(),
            // TODO: 其他特殊祝福在此注册
            _ => null,
        };

        if (effect != null)
        {
            AddEffect(debugAddEffectId.ToString(), effect);
            Debug.Log($"[BlessingManager] 手动添加了特殊祝福：{debugAddEffectId}");
        }
        else
        {
            Debug.LogWarning($"[BlessingManager] 未找到 {debugAddEffectId} 对应的 Effect 类");
        }
    }

    /// <summary>
    /// 获取指定特殊祝福的效果实例（无则返回 null）。
    /// </summary>
    public T GetEffect<T>(string effectId) where T : BlessingEffect
    {
        activeEffects.TryGetValue(effectId, out var effect);
        return effect as T;
    }

    /// <summary>
    /// 检查是否拥有指定特殊祝福。
    /// </summary>
    public bool HasEffect(string effectId) => activeEffects.ContainsKey(effectId);

    /// <summary>
    /// 检查指定特殊祝福的层数是否 ≥ minLevel。
    /// </summary>
    public bool HasEffectLevel(string effectId, int minLevel)
    {
        return activeEffects.TryGetValue(effectId, out var e) && e.Level >= minLevel;
    }

    /// <summary>
    /// 添加或叠加特殊祝福。对应 Conditional 型祝福在 ApplyBlessing 时调用。
    /// player 用于在升级时触发 OnLevelUp（可传 null，仅首次获得时调用 OnAcquired）。
    /// </summary>
    public void AddEffect(string effectId, BlessingEffect effect, PlayerData player = null)
    {
        if (activeEffects.TryGetValue(effectId, out var existing))
        {
            existing.AddLevel();
            existing.OnLevelUp(player);
        }
        else
        {
            activeEffects[effectId] = effect;
            Debug.Log($"[BlessingManager] 获得特殊祝福：{effectId}（Level 1）");
        }
        RefreshInspectorList();
    }

    // ============================================================
    //  生命周期 — 由 BattleManager 等外部调用，遍历所有 Effect
    // ============================================================

    /// <summary>战斗开始时，遍历所有特殊祝福。</summary>
    public void OnBattleStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnBattleStart(player, enemy, ui);
    }

    /// <summary>战斗结束时，遍历所有特殊祝福。</summary>
    public void OnBattleEnd(PlayerData player, EnemyController enemy, BattleUI ui, bool won)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnBattleEnd(player, enemy, ui, won);
    }

    /// <summary>每回合开始时，遍历所有特殊祝福。</summary>
    public void OnTurnStart(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnTurnStart(player, enemy, ui);
    }

    /// <summary>每回合结束时，遍历所有特殊祝福。</summary>
    public void OnTurnEnd(PlayerData player, EnemyController enemy, BattleUI ui)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnTurnEnd(player, enemy, ui);
    }

    /// <summary>玩家造成伤害后。</summary>
    public void OnPlayerDealDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnPlayerDealDamage(player, enemy, ui, damage);
    }

    /// <summary>玩家受到伤害后。</summary>
    public void OnPlayerTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnPlayerTakeDamage(player, enemy, ui, damage);
    }

    /// <summary>敌人造成伤害后。</summary>
    public void OnEnemyDealDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnEnemyDealDamage(player, enemy, ui, damage);
    }

    /// <summary>敌人受到伤害后。</summary>
    public void OnEnemyTakeDamage(PlayerData player, EnemyController enemy, BattleUI ui, int damage)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnEnemyTakeDamage(player, enemy, ui, damage);
    }

    /// <summary>任一 Effect 返回 false 则本回合不消耗魔力。</summary>
    public bool ShouldConsumeMana(PlayerData player)
    {
        foreach (var kv in activeEffects)
            if (!kv.Value.ShouldConsumeMana(player))
                return false;
        return true;
    }

    /// <summary>进入新楼层时，遍历所有特殊祝福。</summary>
    public void OnEnterFloor(PlayerData player, int floorNumber, BattleUI ui)
    {
        foreach (var kv in activeEffects)
            kv.Value.OnEnterFloor(player, floorNumber, ui);
    }

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

    /// <summary>
    /// 使用自定义祝福池弹出祝福选择面板。
    /// 供商店等外部系统使用。pool 为 null 时回退到默认池。
    /// </summary>
    public void ShowWithPool(PlayerData playerData, BlessingPool pool, BlessingPickup pickup = null)
    {
        if (pool == null)
        {
            Show(playerData, pickup);
            return;
        }

        // 临时切换池子，抽取完成后恢复
        BlessingPool savedPool = blessingPool;
        blessingPool = pool;
        Show(playerData, pickup);
        blessingPool = savedPool;
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
