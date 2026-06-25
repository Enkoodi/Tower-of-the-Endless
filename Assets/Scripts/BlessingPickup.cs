using UnityEngine;

/// <summary>
/// 祝福拾取物 — 挂载在祝福道具 Prefab 上。
/// 玩家拾取后弹出选择面板，从 2-3 个随机 BlessingData 中选择一个。
/// （Blessing 选择系统尚未实现，当前占位）
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class BlessingPickup : MonoBehaviour
{
    [Header("数据引用")]
    [SerializeField] private Sprite blessingSprite;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        if (sr != null && blessingSprite != null)
            sr.sprite = blessingSprite;
    }

    /// <summary>
    /// 被 PlayerMove 调用。TODO: 弹出 Blessing 选择面板。
    /// </summary>
    public bool TryPickup(PlayerData playerData)
    {
        if (playerData == null)
        {
            Debug.LogError("[BlessingPickup] playerData 为 null");
            return false;
        }

        Debug.Log("[BlessingPickup] 拾取祝福道具（选择面板尚未实现）");
        Destroy(gameObject);
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null && blessingSprite != null)
            sr.sprite = blessingSprite;
    }
#endif
}
