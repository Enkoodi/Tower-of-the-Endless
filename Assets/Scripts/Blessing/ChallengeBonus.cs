using System.Collections.Generic;
using UnityEngine;

/// <summary>一个发放节点的进度。remaining = 0 的记录要保留 —— 它就是「这层已经发过」的凭据。</summary>
[System.Serializable]
public class ChallengeBonusNode
{
    public int floor;
    public int remaining;
}

/// <summary>
/// 挑战模式额外祝福的本局进度。
/// recordFloor 在第一次触发时冻结 —— 读档后不会因为 RunRecord 恢复时序不同而换一张表。
/// </summary>
[System.Serializable]
public class ChallengeBonusState
{
    public int recordFloor = GameSaveData.NoFloor;
    public List<ChallengeBonusNode> nodes = new List<ChallengeBonusNode>();
}

/// <summary>
/// 挑战模式的额外祝福：按『安特罗波斯』的掉落楼层 + 开局移涌之钥数，在 1/9/19/26F 发若干次三选一。
///
/// 行 = 掉落楼层（= 本局继承到的阵亡楼层），列 = 开局定格的钥匙数（EnemyScaling.KeyCount）。
/// 表是手调的，**不要试图用公式算**（9-18 行不是统一向上取整）。
///
/// 防重复靠「先记账后弹」：进层时先把 remaining 写进 state 并落盘，再开始弹。
/// 中途强退 → 重进按 remaining 继续；每选一张立刻存档 → 不会白刷。
/// </summary>
public static class ChallengeBonus
{
    private const int MaxKeyCount = 8;

    /// <summary>发放入口楼层。1F 就是开始游戏时（MapGenerator.Start 加载 floor_01）。</summary>
    private static readonly int[] NodeFloors = { 1, 9, 19, 26 };

    /// <summary>
    /// [行][钥匙数-1] = { 1F, 9F, 19F, 26F } 各发几个。
    /// 行：0 = 1-8F，1 = 9-18F，2 = 19-25F，3 = -7~0F 与 26F 以上。
    /// </summary>
    private static readonly int[][][] Table =
    {
        // 1-8F：全部堆在 1F
        new[]
        {
            new[] { 1, 0, 0, 0 }, new[] { 2, 0, 0, 0 }, new[] { 3, 0, 0, 0 }, new[] { 4, 0, 0, 0 },
            new[] { 5, 0, 0, 0 }, new[] { 6, 0, 0, 0 }, new[] { 7, 0, 0, 0 }, new[] { 8, 0, 0, 0 },
        },
        // 9-18F：1F + 9F
        new[]
        {
            new[] { 1, 0, 0, 0 }, new[] { 1, 1, 0, 0 }, new[] { 2, 1, 0, 0 }, new[] { 2, 2, 0, 0 },
            new[] { 3, 2, 0, 0 }, new[] { 3, 3, 0, 0 }, new[] { 4, 3, 0, 0 }, new[] { 4, 4, 0, 0 },
        },
        // 19-25F：1F / 9F / 19F
        new[]
        {
            new[] { 1, 0, 0, 0 }, new[] { 1, 1, 0, 0 }, new[] { 1, 1, 1, 0 }, new[] { 2, 1, 1, 0 },
            new[] { 2, 2, 1, 0 }, new[] { 2, 2, 2, 0 }, new[] { 3, 2, 2, 0 }, new[] { 3, 3, 2, 0 },
        },
        // -7~0F 与 26F 以上：1F / 9F / 19F / 26F
        new[]
        {
            new[] { 1, 0, 0, 0 }, new[] { 1, 1, 0, 0 }, new[] { 1, 1, 1, 0 }, new[] { 1, 1, 1, 1 },
            new[] { 2, 1, 1, 1 }, new[] { 2, 2, 1, 1 }, new[] { 2, 2, 2, 1 }, new[] { 2, 2, 2, 2 },
        },
    };

    private static ChallengeBonusState state = new ChallengeBonusState();

    /// <summary>待发放的节点（按楼层升序）。运行时队列，不进存档 —— 存档只记 remaining。</summary>
    private static readonly Queue<int> nodeQueue = new Queue<int>();

    /// <summary>正在弹（防止重入）。</summary>
    private static bool running;

    /// <summary>状态版本号。读档/新局会 +1，旧回调据此自行作废。</summary>
    private static int session;

    // ============================================================
    //  生命周期
    // ============================================================

    /// <summary>进 Game 场景时清空 —— 真正的新局/读档随后由 RestoreRun 覆盖。</summary>
    public static void BeginRun() => Reset();

    /// <summary>读档：整份覆盖。旧档（null）就是空状态，第一次进层时会把「当前层之前」的节点标成已发完。</summary>
    public static void RestoreRun(ChallengeBonusState saved)
    {
        CancelRunning();
        state = saved ?? new ChallengeBonusState();
    }

    /// <summary>新游戏：清掉上一局的进度。</summary>
    public static void Reset()
    {
        CancelRunning();
        state = new ChallengeBonusState();
    }

    private static void CancelRunning()
    {
        session++;
        nodeQueue.Clear();
        running = false;

        // 读档/新局时面板可能还开着：连弹状态一起取消。
        // 不取消的话玩家接着选，祝福会加到一个已经回退的世界里。
        BlessingManager.Instance?.CancelSequence();
    }

    /// <summary>存档用。SaveGameTo 会立刻序列化，直接返回引用即可。</summary>
    public static ChallengeBonusState Snapshot() => state;

    // ============================================================
    //  触发
    // ============================================================

    /// <summary>
    /// 每次加载完楼层调用（MapGenerator.ApplyMapData 末尾）。
    /// 本层是节点层就发本层的份数，顺手补发「本层之前还没记录」的节点；
    /// 另外把「弹到一半退出」留下的 remaining 重新排进队列。
    /// </summary>
    public static void TryTriggerOnFloor(int floor)
    {
        if (!GameSession.IsChallenge) return;
        if (floor == GameSaveData.NoFloor) return;

        AnthroposRecord record = AnthroposBlessing.RunRecord;
        if (record == null || !record.IsValid()) return;

        PlayerData player = Object.FindAnyObjectByType<PlayerData>();
        if (player == null || player.HP <= 0) return;

        // 本局第一次触发：冻结掉落楼层；旧档/异常起点里「已经过去的节点」直接标成已发完（不补）
        if (state.recordFloor == GameSaveData.NoFloor)
        {
            state.recordFloor = record.floor;
            MarkPastNodesDone(floor);
        }

        bool added = false;
        int key = Mathf.Clamp(EnemyScaling.KeyCount, 0, MaxKeyCount);

        if (key > 0)
        {
            int[] counts = Table[RowOf(state.recordFloor)][key - 1];

            // 补发：当前层之前、表里该发、但还没记录的节点（正常流程走不到，防 bug / 开挂跳层）
            for (int i = 0; i < NodeFloors.Length; i++)
            {
                if (NodeFloors[i] >= floor) break;
                if (counts[i] <= 0) continue;
                if (FindNode(NodeFloors[i]) != null) continue;

                state.nodes.Add(new ChallengeBonusNode { floor = NodeFloors[i], remaining = counts[i] });
                added = true;
                PlayerLog.Note($"挑战奖励：补发 {NodeFloors[i]}F ×{counts[i]}");
            }

            // 当前层自己的份数
            int index = System.Array.IndexOf(NodeFloors, floor);
            if (index >= 0 && counts[index] > 0 && FindNode(floor) == null)
            {
                state.nodes.Add(new ChallengeBonusNode { floor = floor, remaining = counts[index] });
                added = true;
                PlayerLog.Note($"挑战奖励：{floor}F ×{counts[index]}");
            }
        }

        if (running) return;

        // 重建队列：remaining > 0 的都要发，包括「弹到一半强退/读档」留下来的那些
        nodeQueue.Clear();
        foreach (int f in NodeFloors)
        {
            ChallengeBonusNode node = FindNode(f);
            if (node != null && node.remaining > 0) nodeQueue.Enqueue(f);
        }

        if (nodeQueue.Count == 0) return;

        // 先记账再弹：新增节点时立刻落盘，中途强退也能续上
        if (added) SaveManager.Instance?.SaveAutoGame();
        Pump();
    }

    // ============================================================
    //  发放
    // ============================================================

    /// <summary>按队列逐节点发放，每个节点连弹 remaining 次三选一。</summary>
    private static void Pump()
    {
        if (running) return;

        // 跳过已经发完的节点
        while (nodeQueue.Count > 0)
        {
            ChallengeBonusNode head = FindNode(nodeQueue.Peek());
            if (head == null || head.remaining <= 0)
            {
                nodeQueue.Dequeue();
                continue;
            }
            break;
        }

        if (nodeQueue.Count == 0) return;

        PlayerData player = Object.FindAnyObjectByType<PlayerData>();
        BlessingManager manager = BlessingManager.Instance;
        if (player == null || manager == null)
        {
            Debug.LogWarning("[ChallengeBonus] 玩家或 BlessingManager 不在，剩余奖励本次不发放");
            nodeQueue.Clear();
            return;
        }

        ChallengeBonusNode node = FindNode(nodeQueue.Peek());
        running = true;
        int mySession = session;

        // 池子传 null = 用 BlessingManager 的默认池（就是 BlessingPool1）
        manager.ShowSequence(player, null, node.remaining,
            onEachDone: () =>
            {
                if (mySession != session) return;   // 期间读档/新局了，这次回调作废
                node.remaining--;
                SaveManager.Instance?.SaveAutoGame();
            },
            onAllDone: () =>
            {
                if (mySession != session)
                {
                    running = false;
                    return;
                }

                // 抽取为空会让 remaining 没被扣掉，这里兜一下，否则会原地死循环
                if (node.remaining > 0)
                {
                    Debug.LogWarning($"[ChallengeBonus] {node.floor}F 还剩 {node.remaining} 次没发（抽取为空？），按已发完处理");
                    node.remaining = 0;
                }

                // 队列可能已被重建过，确认队首还是这一层再出队
                if (nodeQueue.Count > 0 && nodeQueue.Peek() == node.floor) nodeQueue.Dequeue();

                running = false;
                SaveManager.Instance?.SaveAutoGame();
                Pump();
            });
    }

    // ============================================================
    //  工具
    // ============================================================

    /// <summary>掉落楼层 → 表格行。-7~0 与 26F 以上同属最后一档。</summary>
    private static int RowOf(int floor)
    {
        if (floor >= 1 && floor <= 8) return 0;
        if (floor >= 9 && floor <= 18) return 1;
        if (floor >= 19 && floor <= 25) return 2;
        return 3;
    }

    /// <summary>本局第一次触发时，把当前层之前的所有节点记成「已发完」。</summary>
    private static void MarkPastNodesDone(int currentFloor)
    {
        foreach (int node in NodeFloors)
        {
            if (node >= currentFloor) break;
            if (FindNode(node) != null) continue;
            state.nodes.Add(new ChallengeBonusNode { floor = node, remaining = 0 });
        }
    }

    private static ChallengeBonusNode FindNode(int floor)
    {
        foreach (ChallengeBonusNode node in state.nodes)
            if (node.floor == floor) return node;
        return null;
    }
}
