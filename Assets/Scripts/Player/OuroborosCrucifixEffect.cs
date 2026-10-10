using UnityEngine;

/// <summary>
/// 衔尾之十字效果 — 拾取「衔尾之十字」装备时添加到玩家身上。
/// **战斗开始时**比较持有者的吸血与反伤：较低的一项获得「较高一项的 <see cref="PercentOfHigher"/>%」加成
/// （默认 50% = 一半）。两项相等时没有「较低者」，不加成。
///
/// 加成走 PlayerData 的**战斗内修正层**，战斗结束 EndBattleStats() 整体清零 ——
/// 和大部分祝福一样，出战即恢复原样。
/// </summary>
public class OuroborosCrucifixEffect : MonoBehaviour
{
    [Tooltip("较低的一项补上「较高一项」的百分之几（50 = 一半）")]
    [SerializeField] private int percentOfHigher = 50;

    /// <summary>较低项按较高项的百分之几加成。</summary>
    public int PercentOfHigher
    {
        get => Mathf.Max(0, percentOfHigher);
        set => percentOfHigher = Mathf.Max(0, value);
    }

    /// <summary>
    /// 算出加成量：两个 out 分别是要给吸血 / 反伤补的固定值（没补的那项为 0）。
    /// 两项相等时没有「较低者」，两个都是 0。
    /// 取整偏向玩家（Ceil），与项目「加成一律 Ceil」的取整口径一致。
    /// </summary>
    public void ComputeBonus(int lifeSteal, int reflectDamage,
                             out int bonusLifeSteal, out int bonusReflectDamage)
    {
        bonusLifeSteal = 0;
        bonusReflectDamage = 0;

        if (lifeSteal == reflectDamage) return;   // 相等 → 没有「较低者」

        int percent = PercentOfHigher;
        if (percent <= 0) return;

        if (lifeSteal < reflectDamage)
            bonusLifeSteal = Mathf.CeilToInt(reflectDamage * percent / 100f);
        else
            bonusReflectDamage = Mathf.CeilToInt(lifeSteal * percent / 100f);
    }
}
