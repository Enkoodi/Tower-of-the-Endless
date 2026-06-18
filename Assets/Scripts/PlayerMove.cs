using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IKeyInventory
{
    bool HasKey(KeyType keyType);
    void UseKey(KeyType keyType);
}
public class PlayerMove : MonoBehaviour, IKeyInventory
{
    [Header("移动参数")]
    [SerializeField] private float moveDistance = 1f;
    [SerializeField] private float moveDelay = 0.2f;
    [SerializeField] private float moveDuration = 0.15f;

    [Header("钥匙数量")]
    [SerializeField] private int yellowKeys = 0;
    [SerializeField] private int blueKeys = 0;
    [SerializeField] private int redKeys = 0;
    [SerializeField] private int scarletKeys = 0;
    [SerializeField] private int aeonKeys = 0;

    [Header("交互设置")]
    [SerializeField] private LayerMask doorLayer;
    [SerializeField] private float raycastDistance = 0.6f;

    private bool isMoving = false;
    private Vector3 targetPosition;
    private float lastMoveTime = 0f;

    // 按键栈：按顺序记录当前按住的方向，栈顶 = 最后按下的方向
    private List<Vector2> keyStack = new List<Vector2>();

    void Start()
    {
        targetPosition = transform.position;
        
        if (doorLayer == 0)
        {
            Debug.LogWarning("[PlayerMove] DoorLayer 未设置，请在 Inspector 中指定 Door 层");
        }
    }

    void Update()
    {
        // === 输入追踪（每帧执行，不漏掉按键事件）===
        TrackKeyPress(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyPress(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyPress(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyPress(KeyCode.D, KeyCode.RightArrow, Vector2.right);

        TrackKeyRelease(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyRelease(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyRelease(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyRelease(KeyCode.D, KeyCode.RightArrow, Vector2.right);

        // === 移动执行（受冷却限制）===
        if (!isMoving && Time.time - lastMoveTime >= moveDelay && keyStack.Count > 0)
        {
            Vector2 direction = keyStack[keyStack.Count - 1];
            TryMove(direction);
            lastMoveTime = Time.time;
        }
    }

    private void TrackKeyPress(KeyCode primary, KeyCode alternative, Vector2 direction)
    {
        if (Input.GetKeyDown(primary) || Input.GetKeyDown(alternative))
        {
            // 如果该方向已在栈中，移到栈顶；否则压入栈顶
            keyStack.Remove(direction);
            keyStack.Add(direction);
        }
    }

    private void TrackKeyRelease(KeyCode primary, KeyCode alternative, Vector2 direction)
    {
        if (Input.GetKeyUp(primary) || Input.GetKeyUp(alternative))
        {
            keyStack.Remove(direction);
        }
    }

    private void TryMove(Vector2 direction)
    {
        Vector3 newPosition = transform.position + (Vector3)direction * moveDistance;

        // 先检测前方是否有门
        DoorController doorInFront = CheckDoorInDirection(direction);
        
        if (doorInFront != null)
        {
            // 尝试开门
            bool opened = doorInFront.TryOpen(this);
            if (opened)
            {
                Debug.Log("[PlayerMove] 门已打开！");
                return;
            }
            else
            {
                Debug.Log("[PlayerMove] 无法打开这扇门");
                return;
            }
        }

        // 如果没有门或门已打开，检测墙壁碰撞
        Collider2D hit = Physics2D.OverlapCircle(newPosition, 0.4f, LayerMask.GetMask("Wall"));
        if (hit == null)
        {
            targetPosition = newPosition;
            isMoving = true;
            StartCoroutine(SmoothMove());
        }
    }

    /// <summary>
    /// 使用射线检测检查玩家前方是否有门
    /// </summary>
    /// <param name="direction">检测方向</param>
    /// <returns>检测到的门控制器，如果没有则返回null</returns>
    private DoorController CheckDoorInDirection(Vector2 direction)
    {
        Vector2 origin = transform.position;
        Vector2 rayDirection = direction.normalized;
        
        RaycastHit2D hit = Physics2D.Raycast(origin, rayDirection, raycastDistance, doorLayer);
        
        if (hit.collider != null)
        {
            DoorController door = hit.collider.GetComponent<DoorController>();
            if (door != null)
            {
                return door;
            }
        }
        
        return null;
    }

    private IEnumerator SmoothMove()
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
    }

    public bool HasKey(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:
                return yellowKeys > 0;
            case KeyType.Blue:
                return blueKeys > 0;
            case KeyType.Red:
                return redKeys > 0;
            case KeyType.Scarlet:
                return scarletKeys > 0;
            case KeyType.Aeon:
                return aeonKeys > 0;
            default:
                return false;
        }
    }

    public void UseKey(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:
                if (yellowKeys > 0) yellowKeys--;
                break;
            case KeyType.Blue:
                if (blueKeys > 0) blueKeys--;
                break;
            case KeyType.Red:
                if (redKeys > 0) redKeys--;
                break;
            case KeyType.Scarlet:
                if (scarletKeys > 0) scarletKeys--;
                break;
            case KeyType.Aeon:
                if (aeonKeys > 0) aeonKeys--;
                break;
        }
    }

    public void AddKey(KeyType keyType, int amount = 1)
    {
        switch (keyType)
        {
            case KeyType.Yellow:
                yellowKeys += amount;
                break;
            case KeyType.Blue:
                blueKeys += amount;
                break;
            case KeyType.Red:
                redKeys += amount;
                break;
            case KeyType.Scarlet:
                scarletKeys += amount;
                break;
            case KeyType.Aeon:
                aeonKeys += amount;
                break;
        }
    }

    public int GetKeyCount(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:
                return yellowKeys;
            case KeyType.Blue:
                return blueKeys;
            case KeyType.Red:
                return redKeys;
            case KeyType.Scarlet:
                return scarletKeys;
            case KeyType.Aeon:
                return aeonKeys;
            default:
                return 0;
        }
    }
}