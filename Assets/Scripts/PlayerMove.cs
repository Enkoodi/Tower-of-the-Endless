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

    [Header("碰撞检测")]
    [SerializeField] private LayerMask doorLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float checkRadius = 0.4f;

    private bool isMoving = false;
    private Vector3 targetPosition;
    private float lastMoveTime = 0f;

    // 按键栈：当前按住的方向，栈顶 = 最后按下的方向
    private List<Vector2> keyStack = new List<Vector2>();

    void Start()
    {
        targetPosition = transform.position;

        if (doorLayer == 0)
            Debug.LogWarning("[PlayerMove] DoorLayer 未设置");
        if (wallLayer == 0)
            Debug.LogWarning("[PlayerMove] WallLayer 未设置");
    }

    void Update()
    {
        TrackKeyPress(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyPress(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyPress(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyPress(KeyCode.D, KeyCode.RightArrow, Vector2.right);

        TrackKeyRelease(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyRelease(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyRelease(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyRelease(KeyCode.D, KeyCode.RightArrow, Vector2.right);

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
        Vector3 target = transform.position + (Vector3)direction * moveDistance;

        // 1. 检测目标位置是否有门
        Collider2D doorHit = Physics2D.OverlapCircle(target, checkRadius, doorLayer);
        if (doorHit != null)
        {
            DoorController door = doorHit.GetComponent<DoorController>();
            if (door != null)
            {
                bool opened = door.TryOpen(this);
                Debug.Log(opened ? "[PlayerMove] 门已打开！" : "[PlayerMove] 无法打开这扇门");
                return;
            }
            // 命中了 doorLayer 但不是 DoorController，可能是墙或其他
            Debug.Log($"[PlayerMove] 目标位置命中 doorLayer 物体：{doorHit.name}，无 DoorController 组件");
        }

        // 2. 检测目标位置是否有墙
        Collider2D wallHit = Physics2D.OverlapCircle(target, checkRadius, wallLayer);
        if (wallHit != null)
        {
            Debug.Log($"[PlayerMove] 前方是墙（{wallHit.name}），无法通行");
            return;
        }

        // 3. 无障碍，执行移动
        targetPosition = target;
        isMoving = true;
        StartCoroutine(SmoothMove());
    }

    private IEnumerator SmoothMove()
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            transform.position = Vector3.Lerp(start, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
    }

    public bool HasKey(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  return yellowKeys > 0;
            case KeyType.Blue:    return blueKeys > 0;
            case KeyType.Red:     return redKeys > 0;
            case KeyType.Scarlet: return scarletKeys > 0;
            case KeyType.Aeon:    return aeonKeys > 0;
            default:              return false;
        }
    }

    public void UseKey(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  if (yellowKeys > 0) yellowKeys--; break;
            case KeyType.Blue:    if (blueKeys > 0) blueKeys--; break;
            case KeyType.Red:     if (redKeys > 0) redKeys--; break;
            case KeyType.Scarlet: if (scarletKeys > 0) scarletKeys--; break;
            case KeyType.Aeon:    if (aeonKeys > 0) aeonKeys--; break;
        }
    }

    public void AddKey(KeyType keyType, int amount = 1)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  yellowKeys += amount; break;
            case KeyType.Blue:    blueKeys += amount; break;
            case KeyType.Red:     redKeys += amount; break;
            case KeyType.Scarlet: scarletKeys += amount; break;
            case KeyType.Aeon:    aeonKeys += amount; break;
        }
    }

    public int GetKeyCount(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Yellow:  return yellowKeys;
            case KeyType.Blue:    return blueKeys;
            case KeyType.Red:     return redKeys;
            case KeyType.Scarlet: return scarletKeys;
            case KeyType.Aeon:    return aeonKeys;
            default:              return 0;
        }
    }
}