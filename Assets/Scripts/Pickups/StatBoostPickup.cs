using UnityEngine;

/// <summary>
/// 属性增益拾取物 — 挂载在增益道具 Prefab 上。
/// 通过 StatBoostData 资产配置类型、数值和精灵。
/// 玩家走到该格子时直接为 PlayerData 增加对应属性。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class StatBoostPickup : MonoBehaviour
{
    [Header("数据引用")]
    [SerializeField] private StatBoostData data;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        if (sr != null && data != null && data.pickupSprite != null)
            sr.sprite = data.pickupSprite;
    }

    public bool TryPickup(PlayerData playerData)
    {
        if (data == null)
        {
            Debug.LogError($"[StatBoostPickup] {name} 的 StatBoostData 未设置！");
            return false;
        }

        if (playerData == null)
        {
            Debug.LogError($"[StatBoostPickup] playerData 为 null");
            return false;
        }

        Debug.Log($"[StatBoostPickup] 拾取 {data.displayName}！");
        playerData.ApplyStatBoost(data.boostType, data.value);
        Destroy(gameObject);
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && data != null && data.pickupSprite != null)
            sr.sprite = data.pickupSprite;
    }
#endif
}
