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
            if (sim.Unbeatable)
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
        /// 是否「无法战胜」。**只有两种情况**：
        ///   ① 完全伤不到敌人（自己打不出伤害，反伤 / 撕裂也造不成伤害）→ 内核提前收场；
        ///   ② 打到回合上限仍未击败敌人（连无限血量都打不完）。
        /// 玩家血量不足**不算** —— 那说明敌人本身打得动，照常显示战损。
        /// </summary>
        public bool Unbeatable;

        /// <summary>
        /// 净 HP 变化 = **打完这一场真实要扣掉多少血**。正数 = 损失，<b>负数 = 反而回血</b>。
        ///
        /// ⚠️ 这是「玩家血量无限」跑出来的值，<b>不</b>受玩家当前血量限制 ——
        /// 所以它可能大于玩家的血量上限（那正是「你现在打不过」的信号），
        /// 而不是被扣到 0 截断成「玩家自己的血量」。
        /// </summary>
        public int HpDelta;
    }

    /// <summary>
    /// 模拟与敌人的战斗，返回「无法战胜」标志 + **真实战损**。
    ///
    /// **玩家按「理论无限血量」跑** ⇒ <c>HpDelta</c> = 打完这一场真实要扣掉多少血，
    /// 与玩家当前血量无关（可能大于血量上限 —— 那正是「你现在打不过」）。
    /// 连无限血量都打不完（伤不到敌人 / 到回合上限）才算「无法战胜」。
    /// </summary>
    public static BattleSimResult SimulateBattle(PlayerData player, EnemyStats enemy)
    {
        BattleSimResult result = default;   // Unbeatable = false, HpDelta = 0
        if (player == null || enemy == null) return result;

        // 纯数据副本 —— 图鉴绝不能碰到玩家或敌人的真身
        // **玩家血量无限**：图鉴要报的是「打完这一场真实要扣多少血」，不能被当前血量截断
        //   （旧做法打不赢就扣到 0 为止 → 显示的数字恒等于玩家自己的血量，与敌人无关）
        //   所以用 UnlimitedHP 的副本，一路打到敌人被击败；实战不走这里，不受影响
        // 衔尾之十字：它只在**战斗开始时**生效，图鉴在战斗外打开 → 这里先算出加成再建副本
        int ouroborosLifeSteal = 0, ouroborosReflect = 0;
        OuroborosCrucifixEffect ouroboros = player.GetComponent<OuroborosCrucifixEffect>();
        if (ouroboros != null)
            ouroboros.ComputeBonus(player.LifeSteal, player.ReflectDamage,
                                   out ouroborosLifeSteal, out ouroborosReflect);

        SimBattleUnit simPlayer = SimBattleUnit.FromPlayerUnlimitedHP(player, ouroborosLifeSteal, ouroborosReflect);
        SimBattleUnit simEnemy  = SimBattleUnit.FromEnemyStats(enemy);

        // 神圣剑：图鉴在战斗外打开，直接读玩家身上的组件
        HolySwordEffect amplifier = player.GetComponent<HolySwordEffect>();
        int magicPercent = amplifier != null ? amplifier.MultiplierPercent : 100;

        // 贝希摩斯之牙：撕裂（Rend）参数与实战同一份
        BehemothsFangEffect fang = player.GetComponent<BehemothsFangEffect>();
        RendSpec rendSpec = fang != null ? fang.ToRendSpec() : default;

        // 与实战同一份内核；祝福挂钩一个都不设 → 不跑祝福
        BattleResolver resolver = new BattleResolver(simPlayer, simEnemy, magicPercent, rendSpec);

        // 装备类效果与实战同步（神圣剑已在上面读取）：利维坦之髓每隔若干回合冻结敌人
        LeviathansMarrowEffect marrow = player.GetComponent<LeviathansMarrowEffect>();
        if (marrow != null)
            resolver.EnemyCanAct = turn => !marrow.FreezesOnTurn(turn);

        resolver.RunToEnd();

        // 只有「伤不到敌人」和「到回合上限仍打不完」才算无法战胜。
        // 玩家血量不足不属于此列 —— 无限血量下照样打得完，照常报战损。
        result.Unbeatable = resolver.EndReason == BattleEndReason.EnemyUnkillable
                         || resolver.EndReason == BattleEndReason.Stalemate;
        // 无限血量副本：InitialHP 是哨兵大数，差值就是「这一场真实要扣的血」。
        // 正数 = 损失，负数 = 反过来回血。
        result.HpDelta = simPlayer.InitialHP - simPlayer.HP;
        return result;
    }

}
