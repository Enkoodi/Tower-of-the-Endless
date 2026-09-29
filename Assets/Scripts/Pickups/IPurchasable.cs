using UnityEngine;

/// <summary>
/// 商店可交易道具 —— 神秘商人（MerchantPurchase）能发放 / 回收的道具都实现它。
///
/// 为什么要有这个接口：`MerchantPurchase` 原先在 `ValidateItems`（出售校验）和
/// `ApplyItem`（发放 / 回收）里**各维护一份手写的组件清单，而且两份还不一样**：
///   · 出售校验只认：KeyPickup / FloorUpTeleporter / FloorDownTeleporter
///   · 发放只认：KeyPickup / StatBoostPickup / BlessingPickup / 两个传送器
/// 两边都漏掉了圣水、神圣剑、神圣盾、神圣火花、麦酒、魔力精华。
///
/// 更糟的是 `TryPurchase` **先扣金币、再发放**，所以买这些道具的表现是
/// 「金币照扣、东西不给」，只打一条「未识别的道具类型」警告 —— 玩家只会觉得商店坏了。
/// （实测受影响：10F Shop 卖麦酒、10F / 19F Shop 2 卖魔力精华、3F 卖圣水。）
///
/// 现在改成按接口取组件，新增道具只要实现本接口，商店侧一行都不用改。
/// </summary>
public interface IPurchasable
{
    /// <summary>
    /// 出售前的持有量校验 —— 只在 quantity 为负的交易里被调用，sellAmount 恒为正。
    /// 数量不足、或该道具根本不可出售，都返回 false；**具体原因由实现自己 LogWarning**，
    /// 因为只有它自己知道「钥匙不足」和「祝福不可出售」的区别。
    /// </summary>
    bool CanSell(PlayerData player, int sellAmount);

    /// <summary>
    /// 发放（amount &gt; 0）或回收（amount &lt; 0）。amount 不会为 0。
    /// 数量型道具直接加减计数；效果型道具按份数叠加效果。
    /// </summary>
    void ApplyPurchase(PlayerData player, int amount);
}
