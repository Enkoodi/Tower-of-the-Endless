using UnityEngine;

/// <summary>
/// 物品掉落配置 — 挂载在敌人 Prefab 上，定义该敌人击败后会掉落哪些物品。
/// 实际的生成、状态同步由 DropManager 统一管理。
/// </summary>
public class ItemDrop : MonoBehaviour
{
    [Header("掉落配置（可配置多个）")]
    [SerializeField] private DropEntry[] drops;

    [System.Serializable]
    public struct DropEntry
    {
        [Tooltip("掉落的物品预制体（KeyPickup / StatBoostPickup 等）")]
        public GameObject prefab;

        [Tooltip("掉落位置偏移（相对于敌人世界坐标）")]
        public Vector2 offset;
    }

    public DropEntry[] Drops => drops;

    // ========================================================================
    //  Editor 调试
    // ========================================================================

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (drops == null || drops.Length == 0) return;

        for (int i = 0; i < drops.Length; i++)
        {
            if (drops[i].prefab == null) continue;

            Vector3 dropWorldPos = transform.position + (Vector3)drops[i].offset;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(dropWorldPos, 0.15f);
            Gizmos.DrawLine(transform.position, dropWorldPos);

            Gizmos.color = Color.yellow;
            UnityEditor.Handles.Label(
                dropWorldPos + Vector3.up * 0.25f,
                $"掉落[{i}]\n({drops[i].offset.x}, {drops[i].offset.y})"
            );
        }
    }
#endif
}
