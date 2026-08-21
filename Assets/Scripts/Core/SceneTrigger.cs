using UnityEngine;

/// <summary>
/// 场景跳转触发器 — 当玩家走到指定楼层的指定格子时，跳转到指定场景（.unity）。
/// 挂载在场景中的任意 GameObject 上（例如 MapGenerator 或一个空物体）。
///
/// 使用步骤：
/// 1. 把本脚本挂到场景中的某个物体上；
/// 2. 在 Inspector 中填写目标楼层 targetFloor、目标格子 targetGridPos、目标场景名 sceneName；
/// 3. 确保目标场景已加入 Build Settings（跳转走 ScreenFader，自带淡入淡出过场）。
/// </summary>
public class SceneTrigger : MonoBehaviour
{
    [Header("触发条件")]
    [Tooltip("目标楼层编号（对应 floor_XX.json 的 floor 字段，如 30）")]
    [SerializeField] private int targetFloor = 30;

    [Tooltip("目标网格坐标：x=列（从左往右数，0 开始），y=行（从上往下数，0 开始）")]
    [SerializeField] private Vector2Int targetGridPos = new Vector2Int(8, 3);

    [Header("跳转目标")]
    [Tooltip("要跳转的场景名（.unity 文件名，需加入 Build Settings）")]
    [SerializeField] private string sceneName = "Ending";

    [Header("选项")]
    [Tooltip("是否只触发一次（true=整个游戏只触发一次，false=每次走到该格都会触发）")]
    [SerializeField] private bool oneShot = true;

    [Tooltip("判定容差：玩家与目标格子中心的距离小于该值才判定到达（防止移动途中误触发）")]
    [SerializeField] private float triggerTolerance = 0.1f;

    private bool hasTriggered;
    private bool wasOnTarget;
    private PlayerMove playerMove;

    private void Start()
    {
        playerMove = FindAnyObjectByType<PlayerMove>();
        if (playerMove == null)
            Debug.LogWarning($"[SceneTrigger] {name} 未找到玩家（PlayerMove），无法检测位置");
    }

    private void Update()
    {
        if (hasTriggered) return;

        MapGenerator mapGen = FindAnyObjectByType<MapGenerator>();
        if (mapGen == null || mapGen.CurrentMap == null || playerMove == null) return;

        // 楼层不符则不判定（并重置边沿状态，避免跨层残留）
        if (mapGen.CurrentFloor != targetFloor)
        {
            wasOnTarget = false;
            return;
        }

        bool onTarget = IsPlayerOnTargetCell(mapGen);
        if (onTarget && !wasOnTarget)
        {
            Trigger();
        }
        wasOnTarget = onTarget;
    }

    /// <summary>玩家是否已站到目标格子上（世界坐标 → 网格坐标，与 MapGenerator 的换算保持一致）</summary>
    private bool IsPlayerOnTargetCell(MapGenerator mapGen)
    {
        float offsetX = -(mapGen.CurrentMap.width  - 1) / 2f;
        float offsetY =  (mapGen.CurrentMap.height - 1) / 2f;

        Vector3 cellCenter = new Vector3(targetGridPos.x + offsetX, -targetGridPos.y + offsetY, 0f);
        return Vector3.Distance(playerMove.transform.position, cellCenter) < triggerTolerance;
    }

    private void Trigger()
    {
        if (oneShot) hasTriggered = true;

        Debug.Log($"[SceneTrigger] 玩家到达第 {targetFloor} 层 ({targetGridPos.x}, {targetGridPos.y})，跳转场景 '{sceneName}'");

        // 通过 ScreenFader 完成淡出 → 加载 → 淡入的过场
        ScreenFader.FadeToScene(sceneName);
    }
}
