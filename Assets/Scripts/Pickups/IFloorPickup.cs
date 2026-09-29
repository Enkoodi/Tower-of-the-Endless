using UnityEngine;

/// <summary>
/// 楼层拾取物统一契约 —— 地图上一切「走上去就能捡」的东西都实现它。
///
/// 为什么要有这个接口：「哪些类型算拾取物」这张清单原先散落在三处
/// （MapGenerator.SpawnItem / PlayerMove.TryMove / DropManager.SetPickupInfo），
/// 新增一种拾取物时只改了看得见的那处，其余地方**不报错、不警告，只是功能静默缺失**。
/// 麦酒、神圣剑、神圣盾就都因为 DropManager 那份清单没同步，出过
/// 「敌人掉落物捡起来之后，上下楼又刷出来」的 bug。
///
/// 现在三处一律按接口取组件，新增拾取物只要实现本接口，别处一行都不用改。
/// </summary>
public interface IFloorPickup
{
    /// <summary>
    /// 由 MapGenerator / DropManager 在生成时写入所属楼层与网格坐标。
    /// 这两个值决定拾取后往哪一层的记忆里写「已拾取」，写错就会重复刷出。
    /// </summary>
    void SetFloorInfo(Vector2Int gridPos, int floor);

    /// <summary>
    /// 由 PlayerMove 在玩家走到该格时调用。返回是否拾取成功。
    /// </summary>
    bool TryPickup(PlayerData playerData);
}
