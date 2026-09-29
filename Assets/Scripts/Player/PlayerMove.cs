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
        bool inputLocked = isInBattle || isChoosingBlessing || isInteractingWithNPC || isInDialogue || isViewingManual;

        // 怪物手册快捷键 — 战斗/祝福/NPC/对话时禁用；手册打开时仍允许再次按 Tab 关闭
        bool manualShortcutLocked = isInBattle || isChoosingBlessing || isInteractingWithNPC || isInDialogue;
        if (!manualShortcutLocked && Input.GetKeyDown(KeyCode.Tab))
        {
            MonsterManualUI manual = FindAnyObjectByType<MonsterManualUI>();
            if (manual != null) manual.Toggle();
        }

        if (inputLocked) return;

        TrackKeyPress(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyPress(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyPress(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyPress(KeyCode.D, KeyCode.RightArrow, Vector2.right);

        TrackKeyRelease(KeyCode.W, KeyCode.UpArrow, Vector2.up);
        TrackKeyRelease(KeyCode.S, KeyCode.DownArrow, Vector2.down);
        TrackKeyRelease(KeyCode.A, KeyCode.LeftArrow, Vector2.left);
        TrackKeyRelease(KeyCode.D, KeyCode.RightArrow, Vector2.right);

        // 快速跳层：Q上楼梯，E下楼梯（需在楼梯十字五格内）
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

        // 战斗前消耗品：主键盘数字排 与 小键盘 都绑定，效果都只作用于下一场战斗
        //   1 = 麦酒（每层攻 +1% / 防 -1%，最多 3 层）
        //   2 = 魔力精华（战斗开始时魔力充能 +50，不叠加）
        //   3 = 圣水（下一场战斗敌人血量减半）
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            TryUseAle();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            TryUseManaEssence();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            TryUseEnemyHalveItem();
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(target, checkRadius, ObstacleMask);

        // 门优先：门未打开时会遮挡身后的道具，必须先处理门，避免隔门直接捡起道具
        DoorController blockingDoor = null;
        foreach (Collider2D c in hits)
        {
            blockingDoor = c.GetComponent<DoorController>();
            if (blockingDoor != null) break;
        }

        if (blockingDoor != null)
        {
            if (playerData != null)
                blockingDoor.TryOpen(playerData, playerData);
            else
                Debug.LogError("[PlayerMove] playerData 为 null，无法开门");
            return;
        }

        // 战斗门触发器不阻挡通行、走过即激活，且不参与下面的占位物分流 ——
        // 这样它与敌人/道具同格时也照样触发（Trigger 内部幂等，重复调用无副作用）。
        Collider2D hit = null;
        foreach (Collider2D c in hits)
        {
            BattleTrigger trigger = c.GetComponent<BattleTrigger>();
            if (trigger != null)
            {
                trigger.Trigger();
                continue;
            }
            if (hit == null) hit = c;
        }

        if (hit == null)
        {
            targetPosition = target;
            isMoving = true;
            StartCoroutine(SmoothMove());
            return;
        }

        // 楼层拾取物：走上去即拾取，拾完继续走到该格。
        // 统一按 IFloorPickup 取，不再逐个类型枚举 —— 钥匙 / 属性碎片 / 祝福 / 传送器 /
        // 神圣剑 / 神圣盾 / 圣水 / 神圣火花 / 麦酒…… 新增拾取物只要实现该接口，这里不用改。
        if (hit.TryGetComponent(out IFloorPickup pickup))
        {
            targetPosition = target;
            isMoving = true;
            if (playerData != null)
                pickup.TryPickup(playerData);
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

    /// <summary>移动与占位检测用的层：门 + 墙 + 敌人 + 道具 + 楼梯 + NPC</summary>
    private LayerMask ObstacleMask => doorLayer | wallLayer | enemyLayer | itemLayer | stairLayer | npcLayer;

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

        // 落点补检：战斗胜利后是「先开打再走进该格」，没走过 TryMove 的分流，
        // 所以在这里把落点上的触发器再激活一次（幂等）。
        TriggerOnCell(transform.position);

        // 夹击检测
        PincerAttack.CheckPincerFormation(playerData);
    }

    /// <summary>激活指定格上的战斗门触发器（没有则什么都不做）。</summary>
    private void TriggerOnCell(Vector3 worldPos)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, checkRadius, ObstacleMask);
        foreach (Collider2D c in hits)
        {
            BattleTrigger trigger = c.GetComponent<BattleTrigger>();
            if (trigger != null) trigger.Trigger();
        }
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
    /// 快速跳层：检测玩家是否在楼梯的十字五格内，若是则跳到指定方向的已访问楼层。
    /// </summary>
    /// <param name="goingUp">true=上楼(Q)，false=下楼(E)</param>
    private void TryQuickFloorJump(bool goingUp)
    {
        if (!IsNearStair())
        {
            Debug.Log($"[QuickJump] 不在楼梯十字五格范围内，无法快速跳层");
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

        // 快速跳层后自动存档
        SaveManager.Instance?.SaveAutoGame();
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

        // 上楼后自动存档
        SaveManager.Instance?.SaveAutoGame();
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

        // 检查目标楼层是否存在（第0层及负楼层同样按文件是否存在判断）
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

        // 下楼后自动存档
        SaveManager.Instance?.SaveAutoGame();
    }

    /// <summary>使用麦酒：下一场战斗攻击 +1% / 防御 -1%，最多叠 3 层（满层后再喝会被浪费）。</summary>
    private void TryUseAle()
    {
        if (playerData == null)
        {
            Debug.LogError("[PlayerMove] 未找到 PlayerData，无法使用麦酒");
            return;
        }

        playerData.UseAle();
    }

    /// <summary>使用魔力精华：下一场战斗开始时魔力充能 +50（不叠加、不累计场次）。</summary>
    private void TryUseManaEssence()
    {
        if (playerData == null)
        {
            Debug.LogError("[PlayerMove] 未找到 PlayerData，无法使用魔力精华");
            return;
        }

        playerData.UseManaEssence();
    }

    /// <summary>使用敌人减半道具：消耗一个，下一场战斗敌人血量减半。</summary>
    private void TryUseEnemyHalveItem()
    {
        if (playerData == null)
        {
            Debug.LogError("[PlayerMove] 未找到 PlayerData，无法使用敌人减半道具");
            return;
        }

        if (!playerData.UseEnemyHalveItem())
        {
            Debug.Log("[PlayerMove] 没有敌人减半道具可用");
        }
    }

    /// <summary>快速跳层范围：以楼梯所在格为中心的十字五格（楼梯格 + 上下左右各一格）</summary>
    private const int QuickJumpRange = 1;

    /// <summary>检测玩家是否在楼梯的十字五格内（以上/下楼梯所在格为中心）</summary>
    private bool IsNearStair()
    {
        // 先放宽半径捞出附近的楼梯，再按格子曼哈顿距离精确判定十字范围。
        // 不能直接拿 OverlapCircleAll 的半径当范围：楼梯碰撞体有半格宽，
        // 半径 1.5 连距离 2 格的楼梯都算命中，实际覆盖外接 5×5（13 格菱形）。
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2f, stairLayer);
        foreach (Collider2D hit in hits)
        {
            StairController stair = hit.GetComponent<StairController>();
            if (stair == null) continue;

            Vector3 delta = stair.transform.position - transform.position;
            int dx = Mathf.RoundToInt(delta.x / moveDistance);
            int dy = Mathf.RoundToInt(delta.y / moveDistance);

            // 曼哈顿距离 ≤1 = 十字五格；斜角格距离为 2，不计入
            if (Mathf.Abs(dx) + Mathf.Abs(dy) <= QuickJumpRange)
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
