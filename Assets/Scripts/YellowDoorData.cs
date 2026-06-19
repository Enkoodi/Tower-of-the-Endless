using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class DoorController : MonoBehaviour
{
    [Header("数据引用")]
    [SerializeField] private DoorData doorData;
    
    private BoxCollider2D col;
    private SpriteRenderer sr;
    private bool isOpened = false;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        
        if (doorData != null && doorData.doorSprite != null && sr != null)
            sr.sprite = doorData.doorSprite;
    }

    public bool TryOpen(IKeyInventory playerInventory)
    {
        if (isOpened)
        {
            Debug.Log($"[Door] {name} 已打开");
            return false;
        }

        if (doorData == null)
        {
            Debug.LogError($"[Door] {name} 的 DoorData 未设置！请在 Inspector 中拖入 DoorData 资产");
            return false;
        }

        if (playerInventory == null)
        {
            Debug.LogError($"[Door] {name} 传入的 playerInventory 为 null！");
            return false;
        }

        Debug.Log($"[Door] {name} 需要 {doorData.requiredKeyType} 钥匙，消耗={doorData.consumeKey}");

        if (playerInventory.HasKey(doorData.requiredKeyType))
        {
            if (doorData.consumeKey)
                playerInventory.UseKey(doorData.requiredKeyType);

            Open();
            Debug.Log($"[Door] {name} 已打开！");
            return true;
        }
        
        Debug.Log($"[Door] {name} 钥匙不足，无法打开");
        return false;
    }

    private void Open()
    {
        isOpened = true;
        col.enabled = false;
        if (sr != null) sr.enabled = false; 
    }
}
