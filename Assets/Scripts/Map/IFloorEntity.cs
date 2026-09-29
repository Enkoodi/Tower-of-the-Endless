using UnityEngine;

/// <summary>
/// 楼层实体统一契约 —— 地图上一切「需要知道自己在第几层、第几格」的对象都实现它。
///
/// 为什么要有这个接口：`MapGenerator` 原先在每个 Spawn 方法里手写一份组件清单
/// （SpawnObject 写 DoorController / BattleDoorController / BattleTrigger，
///  SpawnEnemy 只写 EnemyController，SpawnNpc 写 NPCController / NpcRemover /
///  DialogueTrigger / NpcBattler）。**清单之间互不相通**，所以预制体一旦跨层使用就会漏：
/// 例如 `Enemies/VampireLord/VampireLord.prefab` 是放在 enemies 层的，
/// 但它挂的是 NpcBattler + DialogueTrigger、没有 EnemyController ——
/// SpawnEnemy 的清单里没有 NpcBattler，于是它的 gridPosition / floorNumber 一直是默认的
/// (0,0) / 0，击败后往**第 0 层**写了一条「已移除」，重返本层自然就「复活」了。
///
/// 现在改成按接口一网打尽：对象上所有实现本接口的组件（含子物体）都会被写入，
/// 跨层使用、一个物体挂多个身份都不会再漏。
/// </summary>
public interface IFloorEntity
{
    /// <summary>
    /// 由 MapGenerator 在生成时写入所属楼层与网格坐标。
    /// 这两个值决定之后往哪一层的记忆里写「已清除」，写错就会复活。
    /// </summary>
    void SetFloorInfo(Vector2Int gridPos, int floor);
}
