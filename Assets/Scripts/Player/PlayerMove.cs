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
    [SerializeField] private LayerMask stairLayer;
    [SerializeField] private LayerMask npcLayer;
    [SerializeField] private float checkRadius = 0.4f;

    private bool isMoving = false;
    private bool isInBattle = false;
    private bool isChoosingBlessing = false;
    private bool isInteractingWithNPC = false;
    private bool isInDialogue = false;
    private bool isViewingManual = false;
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

        BlessingManager.OnPanelOpen += () =>
        {
            isChoosingBlessing = true;
            keyStack.Clear();
        };
        BlessingManager.OnPanelClose += () => isChoosingBlessing = false;

        NPCInteractionUI.OnPanelOpen += () =>
        {
            isInteractingWithNPC = true;
            keyStack.Clear();
        };
        NPCInteractionUI.OnPanelClose += () => isInteractingWithNPC = false;

        DialogueUI.OnPanelOpen += () =>
        {
            isInDialogue = true;
            keyStack.Clear();
        };
        DialogueUI.OnPanelClose += () => isInDialogue = false;

        // 战斗事件（覆盖对话触发的战斗，正常战斗 TryMove 也会自行设置 isInBattle）
        BattleManager.OnBattleOpen += () =>
        {
            isInBattle = true;
            keyStack.Clear();
        };
        BattleManager.OnBattleClose += () => isInBattle = false;

        MonsterManualUI.OnPanelOpen += () =>
        {
            isViewingManual = true;
            keyStack.Clear();
        };
        MonsterManualUI.OnPanelClose += () => isViewingManual = false;
    }

    void Update()
    {
        // 怪物手册快捷键 — 始终可响应，不受面板打开状态影响
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            MonsterManualUI manual = FindAnyObjectByType<MonsterManualUI>();
            if (manual != null) manual.Toggle();
        }

        if (isInBattle || isChoosingBlessing || isInteractingWithNPC || isInDialogue || isViewingManual) return;

        TrackKeyPress(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyPress(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyPress(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyPress(KeyCode.D, KeyCode.RightArrow, Vector2.right);

        TrackKeyRelease(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyRelease(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyRelease(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyRelease(KeyCode.D, KeyCode.RightArrow, Vector2.right);

        // 快速跳层：Q上楼梯，E下楼梯（需在楼梯9宫格内）
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryQuickFloorJump(true);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            TryQuickFloorJump(false);
        }

        // 传送器使用：X上楼，Z下楼（消耗数量，任意位置可用）
        if (Input.GetKeyDown(KeyCode.X))
        {
            TryUseUpTeleporter();
        }
        else if (Input.GetKeyDown(KeyCode.Z))
        {
            TryUseDownTeleporter();
        }

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

        // 统一检测：门 + 墙 + 敌人 + 道具 + 楼梯 + NPC，按组件类型分流
        LayerMask obstacleMask = doorLayer | wallLayer | enemyLayer | itemLayer | stairLayer | npcLayer;
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

        // 检查 FloorUpTeleporter — 上楼传送器，拾取后直接走到该格
        FloorUpTeleporter upTeleporter = hit.GetComponent<FloorUpTeleporter>();
        if (upTeleporter != null)
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                upTeleporter.TryPickup(playerData);
            StartCoroutine(SmoothMove());
            return;
        }

        // 检查 FloorDownTeleporter — 下楼传送器，拾取后直接走到该格
        FloorDownTeleporter downTeleporter = hit.GetComponent<FloorDownTeleporter>();
        if (downTeleporter != null)
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                downTeleporter.TryPickup(playerData);
            StartCoroutine(SmoothMove());
            return;
        }

        // 检查 AegisAmuletPickup — 护身符装备，拾取后直接走到该格
        AegisAmuletPickup amulet = hit.GetComponent<AegisAmuletPickup>();
        if (amulet != null)
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                amulet.TryPickup(playerData);
            StartCoroutine(SmoothMove());
            return;
        }

        // 检查 MagicAmplifierPickup — 魔力增幅器装备，拾取后直接走到该格
        MagicAmplifierPickup amplifier = hit.GetComponent<MagicAmplifierPickup>();
        if (amplifier != null)
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                amplifier.TryPickup(playerData);
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

        // 检查 BattleTrigger — 战斗门触发器，不阻挡，走过即激活
        BattleTrigger battleTrigger = hit.GetComponent<BattleTrigger>();
        if (battleTrigger != null)
        {
            targetPosition = target;
            isMoving = true;
            battleTrigger.Trigger();
            StartCoroutine(SmoothMove());
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

        // 检查 StairController — 楼梯切换楼层
        StairController stair = hit.GetComponent<StairController>();
        if (stair != null)
        {
            targetPosition = target;
            isMoving = true;
            StartCoroutine(SmoothMoveToStair(stair));
            return;
        }

        // 检查 DialogueTrigger — 对话NPC，不移动，触发对话
        DialogueTrigger dialogue = hit.GetComponent<DialogueTrigger>();
        if (dialogue != null)
        {
            DialogueUI dialogueUI = FindAnyObjectByType<DialogueUI>();
            if (dialogueUI != null)
            {
                dialogueUI.OpenDialogue(dialogue);
            }
            else
            {
                Debug.LogWarning("[PlayerMove] 未找到 DialogueUI，无法打开对话");
            }
            return;
        }

        // 检查 NPCController — 停止移动，打开NPC交互界面
        NPCController npc = hit.GetComponent<NPCController>();
        if (npc != null)
        {
            NPCInteractionUI npcUI = FindAnyObjectByType<NPCInteractionUI>();
            if (npcUI != null)
            {
                npcUI.OpenInteraction(npc, playerData);
            }
            else
            {
                Debug.LogWarning("[PlayerMove] 未找到 NPCInteractionUI，无法与NPC交互");
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

        // 夹击检测
        PincerAttack.CheckPincerFormation(playerData);
    }

    /// <summary>
    /// 走到楼梯格上，移动完成后触发楼层切换
    /// </summary>
    private IEnumerator SmoothMoveToStair(StairController stair)
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

        // 移动完成后切换楼层
        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        stair.Use(mapGen);
    }

    /// <summary>
    /// 快速跳层：检测玩家是否在楼梯9宫格内，若是则跳到指定方向的已访问楼层。
    /// </summary>
    /// <param name="goingUp">true=上楼(Q)，false=下楼(E)</param>
    private void TryQuickFloorJump(bool goingUp)
    {
        if (!IsNearStair())
        {
            Debug.Log($"[QuickJump] 不在楼梯9宫格范围内，无法快速跳层");
            return;
        }

        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen == null)
        {
            Debug.LogError("[QuickJump] 未找到 MapGenerator");
            return;
        }

        int currentFloor = mapGen.CurrentFloor;
        int targetFloor = FindNextVisitedFloor(currentFloor, goingUp);

        if (targetFloor == currentFloor)
        {
            Debug.Log($"[QuickJump] 没有{(goingUp ? "更高" : "更低")}的已访问楼层");
            return;
        }

        EntryDirection entryDir = goingUp ? EntryDirection.FromBelow : EntryDirection.FromAbove;
        Debug.Log($"[QuickJump] 快速跳层：第 {currentFloor} 层 → 第 {targetFloor} 层（{entryDir}）");
        mapGen.LoadFloor(targetFloor, entryDir);
    }

    /// <summary>使用上楼传送器：消耗一个，向上传送一层（出生在目标层下楼梯）。</summary>
    private void TryUseUpTeleporter()
    {
        if (playerData == null || playerData.UpTeleporterCount <= 0)
        {
            Debug.Log("[PlayerMove] 没有上楼传送器可用");
            return;
        }

        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen == null)
        {
            Debug.LogError("[PlayerMove] 未找到 MapGenerator，无法使用上楼传送器");
            return;
        }

        int targetFloor = mapGen.CurrentFloor + 1;

        // 检查目标楼层是否存在
        string path = $"floor_{targetFloor:D2}";
        if (Resources.Load<TextAsset>(path) == null)
        {
            Debug.LogWarning("[PlayerMove] 已是最高层，无法再向上传送");
            return;
        }

        playerData.UseUpTeleporter();

        // FromBelow = 从下层进入 → 出生在目标层的下楼梯(9)
        Debug.Log($"[PlayerMove] 使用上楼传送器：第 {mapGen.CurrentFloor} 层 → 第 {targetFloor} 层");
        mapGen.LoadFloor(targetFloor, EntryDirection.FromBelow);
    }

    /// <summary>使用下楼传送器：消耗一个，向下传送一层（出生在目标层上楼梯）。</summary>
    private void TryUseDownTeleporter()
    {
        if (playerData == null || playerData.DownTeleporterCount <= 0)
        {
            Debug.Log("[PlayerMove] 没有下楼传送器可用");
            return;
        }

        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen == null)
        {
            Debug.LogError("[PlayerMove] 未找到 MapGenerator，无法使用下楼传送器");
            return;
        }

        int targetFloor = mapGen.CurrentFloor - 1;

        if (targetFloor < 1)
        {
            Debug.LogWarning("[PlayerMove] 已经是第一层，无法再向下传送");
            return;
        }

        // 检查目标楼层是否存在
        string path = $"floor_{targetFloor:D2}";
        if (Resources.Load<TextAsset>(path) == null)
        {
            Debug.LogWarning($"[PlayerMove] 目标楼层 {targetFloor} 不存在");
            return;
        }

        playerData.UseDownTeleporter();

        // FromAbove = 从上层进入 → 出生在目标层的上楼梯(8)
        Debug.Log($"[PlayerMove] 使用下楼传送器：第 {mapGen.CurrentFloor} 层 → 第 {targetFloor} 层");
        mapGen.LoadFloor(targetFloor, EntryDirection.FromAbove);
    }

    /// <summary>检测玩家周围9宫格内是否有楼梯</summary>
    private bool IsNearStair()
    {
        // 9宫格最大距离为 sqrt(2) ≈ 1.414，用 1.5f 覆盖
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.5f, stairLayer);
        foreach (var hit in hits)
        {
            if (hit.GetComponent<StairController>() != null)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 在已访问楼层中查找下一个目标楼层。
    /// </summary>
    /// <param name="currentFloor">当前楼层</param>
    /// <param name="goingUp">true=向上找，false=向下找</param>
    /// <returns>目标楼层编号，若找不到则返回 currentFloor</returns>
    private int FindNextVisitedFloor(int currentFloor, bool goingUp)
    {
        if (FloorMemoryManager.Instance == null) return currentFloor;

        List<int> visited = FloorMemoryManager.Instance.GetVisitedFloors();
        if (visited == null || visited.Count == 0) return currentFloor;

        if (goingUp)
        {
            // 找比当前楼层高的最小已访问楼层
            foreach (int floor in visited)
            {
                if (floor > currentFloor)
                    return floor;
            }
        }
        else
        {
            // 找比当前楼层低的最大已访问楼层（倒序遍历）
            for (int i = visited.Count - 1; i >= 0; i--)
            {
                if (visited[i] < currentFloor)
                    return visited[i];
            }
        }

        return currentFloor;
    }
}
