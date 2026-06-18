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
        
        // 初始化外观
        if (doorData != null && doorData.doorSprite != null)
            sr.sprite = doorData.doorSprite;
    }

    // 供外部（如地图生成器或交互管理器）调用的开门接口
    public bool TryOpen(IKeyInventory playerInventory)
    {
        if (isOpened) return false;

        if (doorData == null)
        {
            Debug.LogError($"[DoorController] {name} 的 DoorData 未设置！请在 Inspector 中拖入 DoorData 资产");
            return false;
        }

        // 检查玩家是否有对应钥匙
        if (playerInventory.HasKey(doorData.requiredKeyType))
        {
            Open();
            if (doorData.consumeKey)
                playerInventory.UseKey(doorData.requiredKeyType);
            return true;
        }
        
        Debug.Log($"需要 {doorData.requiredKeyType} 钥匙才能打开！");
        return false;
    }

    private void Open()
    {
        isOpened = true;
        // 禁用碰撞体，让玩家可以通过
        col.enabled = false;
        // 视觉反馈：隐藏或播放动画
        sr.enabled = false; 
        
        // TODO: 这里可以播放开门音效或粒子特效
        // AudioManager.Play("door_open");
    }
}