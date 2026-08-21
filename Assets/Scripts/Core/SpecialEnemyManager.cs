using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特殊敌人信号管理器 — 单例，跨楼层记录已击败的特殊敌人。
/// 敌人通过在 EnemyStats.specialEnemyId 上配置非空 ID 来标记为特殊敌人，
/// 被击败后由 EnemyController 调用 MarkDefeated 记录。
/// 其它系统可调用 IsDefeated 检测某个特殊敌人是否已被击败。
/// </summary>
public class SpecialEnemyManager : MonoBehaviour
{
    public static SpecialEnemyManager Instance { get; private set; }

    /// <summary>某个特殊敌人被击败时触发，参数为 specialEnemyId。</summary>
    public event System.Action<string> OnSpecialEnemyDefeated;

    private HashSet<string> defeatedIds = new HashSet<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning($"[SpecialEnemy] 检测到重复实例，销毁 GameObject '{gameObject.name}'。已有实例在 '{Instance.gameObject.name}'");
            Destroy(gameObject);
        }
    }

    /// <summary>记录指定特殊敌人已被击败（重复调用无副作用）。</summary>
    public void MarkDefeated(string specialEnemyId)
    {
        if (string.IsNullOrEmpty(specialEnemyId))
        {
            Debug.LogWarning("[SpecialEnemy] MarkDefeated 收到空 ID，忽略");
            return;
        }

        if (defeatedIds.Add(specialEnemyId))
        {
            Debug.Log($"[SpecialEnemy] 已记录击败特殊敌人：{specialEnemyId}");
            OnSpecialEnemyDefeated?.Invoke(specialEnemyId);
        }
    }

    /// <summary>检测指定特殊敌人是否已被击败。</summary>
    public bool IsDefeated(string specialEnemyId)
    {
        return !string.IsNullOrEmpty(specialEnemyId) && defeatedIds.Contains(specialEnemyId);
    }

    /// <summary>获取所有已击败特殊敌人的 ID 列表（用于存档）。</summary>
    public List<string> GetDefeatedIds() => new List<string>(defeatedIds);

    /// <summary>从存档恢复已击败的特殊敌人（覆盖现有数据）。</summary>
    public void RestoreDefeated(List<string> ids)
    {
        defeatedIds.Clear();
        if (ids != null)
        {
            foreach (string id in ids)
                if (!string.IsNullOrEmpty(id))
                    defeatedIds.Add(id);
        }
        Debug.Log($"[SpecialEnemy] 从存档恢复了 {defeatedIds.Count} 个特殊敌人的击败信号");
    }

    /// <summary>清除所有击败信号（新游戏时调用）。</summary>
    public void ResetAll()
    {
        defeatedIds.Clear();
        Debug.Log("[SpecialEnemy] 所有特殊敌人击败信号已清除");
    }
}
