using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("数据引用")]
    [SerializeField] private PlayerData playerData;

    [Header("移动参数")]
    [SerializeField] private float moveDistance = 1f;
    [SerializeField] private float moveDelay = 0.2f;
    [SerializeField] private float moveDuration = 0.15f;

    [Header("碰撞检测")]
    [SerializeField] private LayerMask doorLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float checkRadius = 0.4f;

    private bool isMoving = false;
    private Vector3 targetPosition;
    private float lastMoveTime = 0f;
    private List<Vector2> keyStack = new List<Vector2>();

    void Start()
    {
        targetPosition = transform.position;

        if (playerData == null)
            playerData = GetComponent<PlayerData>();

        if (playerData == null)
            Debug.LogError("[PlayerMove] 未找到 PlayerData！请在玩家上挂载 PlayerData 组件");
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

        // 统一检测：门 + 墙 + 敌人，按组件类型分流
        LayerMask obstacleMask = doorLayer | wallLayer | enemyLayer;
        Collider2D hit = Physics2D.OverlapCircle(target, checkRadius, obstacleMask);

        if (hit == null)
        {
            // 无障碍，执行移动
            targetPosition = target;
            isMoving = true;
            StartCoroutine(SmoothMove());
            return;
        }

        // 先检查 DoorController
        DoorController door = hit.GetComponent<DoorController>();
        if (door != null)
        {
            if (playerData != null)
                door.TryOpen(playerData);
            else
                Debug.LogError("[PlayerMove] playerData 为 null，无法开门");
            return;   // 开门不移动
        }

        // 再检查 EnemyController
        EnemyController enemy = hit.GetComponent<EnemyController>();
        if (enemy != null)
        {
            if (playerData != null)
            {
                bool won = playerData.TryFight(enemy);
                if (won)
                {
                    // 战胜 → 移动到敌人所在格
                    targetPosition = target;
                    isMoving = true;
                    StartCoroutine(SmoothMove());
                }
            }
            else
            {
                Debug.LogError("[PlayerMove] playerData 为 null，无法战斗");
            }
            return;
        }

        // 没找到任何组件 → 当墙处理
        Debug.Log($"[PlayerMove] 前方是墙（{hit.name}），无法通行");
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
}
