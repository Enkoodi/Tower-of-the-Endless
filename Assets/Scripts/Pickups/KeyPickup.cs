using UnityEngine;

/// <summary>
/// 钥匙拾取物 — 挂载在钥匙 Prefab 上。
/// 通过 KeyPickupData 资产配置类型和精灵。
/// 玩家走到该格子时自动拾取，钥匙数量 +1，物体消失。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class KeyPickup : MonoBehaviour
{
    [Header("数据引用")]
    [SerializeField] private KeyPickupData data;

    private SpriteRenderer sr;

    public KeyType KeyType => data != null ? data.keyType : KeyType.Yellow;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        ApplySprite();
    }

    /// <summary>
    /// 被 PlayerMove 调用，尝试拾取。
    /// </summary>
    public bool TryPickup(IKeyInventory playerInventory)
    {
        if (playerInventory == null)
        {
            Debug.LogError($"[KeyPickup] playerInventory 为 null，无法拾取钥匙");
            return false;
        }

        KeyType type = KeyType;
        Debug.Log($"[KeyPickup] 拾取 {type} 钥匙！");

        PlayerData pd = playerInventory as PlayerData;
        if (pd != null)
            pd.AddKey(type, 1);
        else
            Debug.LogError("[KeyPickup] 无法将 IKeyInventory 转换为 PlayerData");

        Destroy(gameObject);
        return true;
    }

    private void ApplySprite()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && data != null && data.keySprite != null)
            sr.sprite = data.keySprite;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        ApplySprite();
    }
#endif
}
