using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗门控制器 — 挂载在战斗门 Prefab 上。
/// 不需要钥匙，通过击败指定位置的敌人后自动打开。
/// 支持两种使用方式：
///   1. 预放置：在 JSON 地图 objects 层直接放置，一开始就可见
///   2. 动态生成：由 BattleTrigger 在触发时生成到指定坐标
/// 当所有关联敌人被击败后自动开门消失。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BattleDoorController : MonoBehaviour
{
    [Header("敌人追踪")]
    [Tooltip("需要击败的敌人所在的网格坐标列表")]
    public Vector2Int[] requiredEnemyPositions;

    [Header("外观")]
    [SerializeField] private Sprite doorSprite;

    private BoxCollider2D col;
    private SpriteRenderer sr;
    private bool isOpened = false;
    private bool initialized = false;

    /// <summary>等待击败的敌人数量</summary>
    private int remainingEnemies;

    /// <summary>在地图网格中的坐标</summary>
    [HideInInspector] public Vector2Int gridPosition;

    /// <summary>所属楼层编号</summary>
    [HideInInspector] public int floorNumber;

    public bool IsOpened => isOpened;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (doorSprite != null && sr != null)
            sr.sprite = doorSprite;

        remainingEnemies = requiredEnemyPositions != null ? requiredEnemyPositions.Length : 0;
    }

    void Start()
    {
        // 预放置模式：MapGenerator 在 Instantiate 后设置了 gridPosition/floorNumber，
        // Start() 调用时这些值已就绪
        Initialize();
    }

    /// <summary>
    /// 初始化战斗门状态。
    /// 预放置模式由 Start() 调用；动态生成模式由触发器在 Instantiate 后立即调用。
    /// 检查楼层记忆，若已开启则直接消失；若关联敌人已全部死亡也直接消失。
    /// </summary>
    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        // 检查楼层记忆中是否已经开启过（重返楼层时）
        FloorState state = FloorMemoryManager.Instance?.GetState(floorNumber);
        if (state != null && state.IsBattleDoorOpened(gridPosition))
        {
            Open();
            return;
        }

        // 检查已击败的敌人（从楼层记忆）
        if (state != null && requiredEnemyPositions != null)
        {
            foreach (var pos in requiredEnemyPositions)
            {
                if (state.IsEnemyDefeated(pos))
                    remainingEnemies--;
            }
        }

        // 所有敌人已死，直接开门
        if (remainingEnemies <= 0)
        {
            Open();
            return;
        }

        // 订阅剩余敌人的击败事件
        FindAndSubscribeToEnemies();
    }

    private void FindAndSubscribeToEnemies()
    {
        if (requiredEnemyPositions == null) return;

        EnemyController[] allEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
        {
            if (enemy.floorNumber != floorNumber) continue;

            foreach (var pos in requiredEnemyPositions)
            {
                if (enemy.gridPosition == pos)
                {
                    enemy.OnDefeated += OnEnemyDefeated;
                    break;
                }
            }
        }
    }

    private void OnEnemyDefeated(EnemyController enemy)
    {
        enemy.OnDefeated -= OnEnemyDefeated;
        remainingEnemies--;

        Debug.Log($"[BattleDoor] 敌人 {enemy.EnemyName}({enemy.gridPosition}) 被击败，" +
                  $"剩余 {remainingEnemies} 个敌人 (门位置: {gridPosition})");

        if (remainingEnemies <= 0)
        {
            Open();
        }
    }

    private void Open()
    {
        if (isOpened) return;
        isOpened = true;

        FloorMemoryManager.Instance?.GetOrCreateState(floorNumber).MarkBattleDoorOpened(gridPosition);

        col.enabled = false;
        if (sr != null) sr.enabled = false;

        Debug.Log($"[BattleDoor] 战斗门已打开！位置：{gridPosition}");
    }
}
