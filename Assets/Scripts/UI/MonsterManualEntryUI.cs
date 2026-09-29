using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 怪物手册单个条目 — 挂载在条目 Prefab 上。
/// 显示敌人图片、数值、战斗所需生命值。
/// </summary>
public class MonsterManualEntryUI : MonoBehaviour
{
    [Header("UI 组件")]
    [SerializeField] private Image enemyImage;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI requiredHPText;

    /// <summary>
    /// 填充条目数据。
    /// </summary>
    /// <param name="enemy">敌人数据资产</param>
    /// <param name="sim">
    /// 战斗模拟结果。由调用方算好传进来 —— 列表排序用的就是它，
    /// 传进来能保证「排序依据」和「显示数字」必然一致，也省掉一次重复模拟。
    /// </param>
    public void Setup(EnemyStats enemy, BattleSimResult sim)
    {
        if (enemy == null) return;

        // 图片
        if (enemyImage != null)
        {
            enemyImage.sprite = enemy.enemySprite;
            enemyImage.enabled = enemy.enemySprite != null;
        }

        // 第一行：名称 / 第二行：基础战斗属性 / 第三行：特殊属性
        if (statsText != null)
        {
            statsText.text = $"<b>{enemy.enemyName}</b>\n"
                           + $"生命值: {enemy.hp}  攻击力: {enemy.attack}  防御力: {enemy.defense}  段数: {enemy.attackCount}  减伤: {enemy.damageReduction}%  速度: {enemy.speed}\n"
                           + $"魔力值: {enemy.manaCharge}  魔力输出: {enemy.manaMax}  吸血: {enemy.lifeSteal}%  反伤: {enemy.reflectDamage}%  金币: {enemy.goldReward}";
        }

        // 战斗所需生命值
        if (requiredHPText != null)
        {
            if (!sim.CanWin)
            {
                requiredHPText.text = "<color=#FF4444>无法战胜</color>";
            }
            else if (sim.HpDelta >= 0)
            {
                requiredHPText.text = $"损失: <color=#FFDD88>{sim.HpDelta}</color> HP";
            }
            else
            {
                // 净回血：保持负数显示（吸血/回复量超过了这一场受到的伤害）
                requiredHPText.text = $"损失: <color=#44FF44>{sim.HpDelta}</color> HP";
            }
        }
    }

    // ========================================================================
    //  战斗模拟 —— 直接跑实战那同一份结算内核（BattleResolver），只是不演出
    //
    //  所以「伤害公式 / 吸血 / 反伤 / 减伤 / 死亡判定 / 回合上限」这些永远与实战同步，
    //  不再需要人工对照 —— 这正是抽内核的目的。
    //
    //  ⚠️ 唯一偏差：**不跑祝福系统**（内核的挂钩一个都不设）。
    //     爱的祝福加攻与扣血、深渊/静默对敌的真伤、工匠的濒死回复、智慧的回血、
    //     异乡人的战斗减伤、灵知的免耗魔力、真理的魔力增幅 —— 都不计入。
    //     玩家带着这些祝福时，图鉴的数字会与实战有偏差。要消掉它，
    //     得先把 BlessingEffect 的入参从 MonoBehaviour 解耦（根治方向的第二步）。
    // ========================================================================

    /// <summary>战斗模拟结果。</summary>
    public struct BattleSimResult
    {
        /// <summary>
        /// 能否打赢。完全伤不到敌人（自己打不出伤害、反伤也造不成伤害）、玩家先阵亡、
        /// 或到回合上限仍未击败敌人 → false。
        /// 注意「自己打 0 伤害」本身不判负 —— 反伤打得动就会照常打下去。
        /// </summary>
        public bool CanWin;

        /// <summary>净 HP 变化。正数 = 损失，<b>负数 = 反而回血</b>（不做 0 下限钳制）。</summary>
        public int HpDelta;
    }

    /// <summary>
    /// 模拟与敌人的战斗，返回能否战胜 + 净 HP 变化。
    /// 玩家阵亡时 HpDelta 是「扣到 0 为止」的实际值（与实战一致，HP 不会变成负数）。
    /// </summary>
    public static BattleSimResult SimulateBattle(PlayerData player, EnemyStats enemy)
    {
        BattleSimResult result = default;   // CanWin = false, HpDelta = 0
        if (player == null || enemy == null) return result;

        // 纯数据副本 —— 图鉴绝不能碰到玩家或敌人的真身
        SimBattleUnit simPlayer = SimBattleUnit.FromPlayer(player);
        SimBattleUnit simEnemy  = SimBattleUnit.FromEnemyStats(enemy);

        // 神圣剑：图鉴在战斗外打开，直接读玩家身上的组件
        HolySwordEffect amplifier = player.GetComponent<HolySwordEffect>();
        int magicPercent = amplifier != null ? amplifier.MultiplierPercent : 100;

        // 与实战同一份内核；挂钩一个都不设 → 不跑祝福
        BattleResolver resolver = new BattleResolver(simPlayer, simEnemy, magicPercent);
        resolver.RunToEnd();

        result.CanWin = resolver.EndReason == BattleEndReason.EnemyDefeated;
        result.HpDelta = simPlayer.InitialHP - simPlayer.HP;   // 正数 = 损失，负数 = 回血
        return result;
    }

}
