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
    [SerializeField] private LayerMask itemLayer;
    [SerializeField] private float checkRadius = 0.4f;

    private bool isMoving = false;
    private bool isInBattle = false;
    private bool isChoosingBlessing = false;
    private Vector3 targetPosition;
    private Vector2 battleDirection;
    private float lastMoveTime = 0f;
    private List<Vector2> keyStack = new List<Vector2>();

    void Start()
    {
        targetPosition = transform.position;

        if (playerData == null)
            playerData = GetComponent<PlayerData>();

        if (playerData == null)
            Debug.LogError("[PlayerMove] 未找到 PlayerData！请在玩家上挂载 PlayerData 组件");

        BlessingManager.OnPanelOpen += () => isChoosingBlessing = true;
        BlessingManager.OnPanelClose += () => isChoosingBlessing = false;
    }

    void Update()
    {
        if (isInBattle || isChoosingBlessing) return;

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

        // 统一检测：门 + 墙 + 敌人 + 道具，按组件类型分流
        LayerMask obstacleMask = doorLayer | wallLayer | enemyLayer | itemLayer;
        Collider2D hit = Physics2D.OverlapCircle(target, checkRadius, obstacleMask);

        if (hit == null)
        {
            targetPosition = target;
            isMoving = true;
            StartCoroutine(SmoothMove());
            return;
        }

        // 检查 KeyPickup — 钥匙不阻挡，拾取后直接走到该格
        KeyPickup key = hit.GetComponent<KeyPickup>();
        if (key != null)
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                key.TryPickup(playerData);
            StartCoroutine(SmoothMove());
            return;
        }

        // 检查 StatBoostPickup — 属性增益，拾取后直接走到该格
        StatBoostPickup statBoost = hit.GetComponent<StatBoostPickup>();
        if (statBoost != null)
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                statBoost.TryPickup(playerData);
            StartCoroutine(SmoothMove());
            return;
        }

        // 检查 BlessingPickup — 祝福选择，拾取后直接走到该格
        BlessingPickup blessing = hit.GetComponent<BlessingPickup>();
        if (blessing != null)
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                blessing.TryPickup(playerData);
            StartCoroutine(SmoothMove());
            return;
        }

        // 先检查 DoorController
        DoorController door = hit.GetComponent<DoorController>();
        if (door != null)
        {
            if (playerData != null)
                door.TryOpen(playerData, playerData);
            else
                Debug.LogError("[PlayerMove] playerData 为 null，无法开门");
            return;
        }

        // 再检查 EnemyController
        EnemyController enemy = hit.GetComponent<EnemyController>();
        if (enemy != null)
        {
            if (playerData != null && BattleManager.Instance != null)
            {
                isInBattle = true;
                battleDirection = direction;
                keyStack.Clear();
                BattleManager.Instance.StartBattle(playerData, enemy, OnBattleEnd);
            }
            else
            {
                Debug.LogError("[PlayerMove] playerData 或 BattleManager 为 null，无法战斗");
            }
            return;
        }

        // 没找到任何组件 → 当墙处理
        Debug.Log($"[PlayerMove] 前方是墙（{hit.name}），无法通行");
    }

    private void OnBattleEnd(bool won)
    {
        isInBattle = false;

        if (won)
        {
            targetPosition = transform.position + (Vector3)battleDirection * moveDistance;
            isMoving = true;
            StartCoroutine(SmoothMove());
        }
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
