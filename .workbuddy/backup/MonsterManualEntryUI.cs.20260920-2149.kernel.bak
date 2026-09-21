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
    //  战斗模拟 —— 逐行对照 BattleManager.BattleCoroutine，只算不演
    //
    //  已对齐实战的部分：
    //    先手偷袭、物理/魔力伤害、魔力消耗、双方吸血、双方反伤、
    //    减伤、魔力增幅器、死亡即收场（含「反伤打死玩家」与「阵亡后不再反伤」）、回合上限。
    //
    //  ⚠️ **不跑祝福系统** —— 这是目前唯一的已知偏差（有意保留）：
    //     爱的祝福的加攻与每回合扣血、深渊/静默对敌的真实伤害、工匠的濒死回复、
    //     智慧的回血、异乡人的战斗减伤、灵知的免耗魔力、真理的魔力增幅，全都不计入。
    //     玩家带着这些祝福时，图鉴的数字会与实战有偏差。
    //     要彻底消掉这个偏差，只能把结算从 BattleManager 抽成实战与图鉴共用的纯函数
    //     （见图鉴类头注释里的「根治方向」），目前没做。
    // ========================================================================

    /// <summary>战斗模拟结果。</summary>
    public struct BattleSimResult
    {
        /// <summary>能否打赢。打不出伤害、玩家先阵亡、或到回合上限仍未击败敌人 → false。</summary>
        public bool CanWin;

        /// <summary>净 HP 变化。正数 = 损失，<b>负数 = 反而回血</b>（不做 0 下限钳制）。</summary>
        public int HpDelta;
    }

    /// <summary>
    /// 模拟与敌人的战斗，返回能否战胜 + 净 HP 变化。
    /// 玩家阵亡时 HpDelta 仍返回实际损失量（可能超过当前 HP），而不是 0。
    /// </summary>
    public static BattleSimResult SimulateBattle(PlayerData player, EnemyStats enemy)
    {
        BattleSimResult result = default;   // CanWin = false, HpDelta = 0
        if (player == null || enemy == null) return result;

        // —— 战斗前状态（对应 BattleManager 的快照）——
        // 追踪玩家血量：不追踪就没法判断阵亡，也就模拟不出「阵亡后不再反伤 / 不再挨反击」
        int playerHP         = player.HP;
        int playerManaCharge = player.ManaCharge;
        int enemyHP          = enemy.hp;
        int enemyManaCharge  = enemy.manaCharge;

        // 魔力增幅器：只放大魔力那部分伤害，不影响魔力消耗（与 BattleManager 一致）
        MagicAmplifier amplifier = player.GetComponent<MagicAmplifier>();
        int magicPercent = amplifier != null ? amplifier.MultiplierPercent : 100;

        // 每轮重算（物理部分也重算，与实战一致；战斗外读属性就是真实字段）
        int playerPhysical = 0, enemyPhysical = 0;
        int ManaCost, damageToEnemy, EnemyManaCost, enemyDamageToPlayer;

        void ComputeRound()
        {
            playerPhysical = Mathf.Max(0, (player.Attack - enemy.defense) * player.AttackCount);
            enemyPhysical  = Mathf.Max(0, (enemy.attack - player.Defense) * enemy.attackCount);

            ManaCost      = Mathf.Min(playerManaCharge, player.ManaMax);
            damageToEnemy = playerPhysical + ManaCost * magicPercent / 100;

            EnemyManaCost       = Mathf.Min(enemyManaCharge, enemy.manaMax);
            enemyDamageToPlayer = enemyPhysical + EnemyManaCost;
        }

        ComputeRound();

        // 先手判定：敌人速度更高则偷袭（发生在回合开始之前，所以用上面的初值）
        if (enemy.speed > player.Speed)
        {
            int sneakDamage = enemyDamageToPlayer * 2;
            int actualSneak = sneakDamage * (100 - player.DamageReduction) / 100;
            playerHP -= actualSneak;
            result.HpDelta += actualSneak;
            if (playerHP <= 0) return result;      // 被偷袭打死 → 判负
        }

        // 破不了防 → 与实战一致：立刻收场判负（BattleManager 的 damageToEnemy <= 0 分支）
        if (damageToEnemy <= 0) return result;

        for (int turn = 0; turn < BattleManager.MaxTurnCount; turn++)
        {
            // —— 玩家攻击 ——
            int actualToEnemy = damageToEnemy * (100 - enemy.damageReduction) / 100;
            enemyHP -= actualToEnemy;

            // 玩家吸血：无条件结算，即使这一击打死了敌人（实战同）。
            // 基数是 playerPhysical（真实物理伤害），且是精确回血、不乘 hpMultiplier。
            int playerSteal = playerPhysical * player.LifeSteal / 100;
            if (playerSteal > 0)
            {
                playerHP += playerSteal;
                result.HpDelta -= playerSteal;
            }

            // 敌人反伤：仅在敌人未因本次攻击死亡时触发（实战同）
            if (enemyHP > 0)
            {
                int reflect = playerPhysical * enemy.reflectDamage / 100;
                if (reflect > 0)
                {
                    int actualReflect = reflect * (100 - player.DamageReduction) / 100;
                    playerHP -= actualReflect;
                    result.HpDelta += actualReflect;

                    // 反伤也可能把玩家打死 —— 实战这时立刻收场，不会再挨一次反击
                    if (playerHP <= 0) return result;
                }
            }

            // 消耗魔力并重算
            playerManaCharge -= ManaCost;
            ComputeRound();

            if (enemyHP <= 0) { result.CanWin = true; return result; }

            // —— 敌人反击 ——
            int actualToPlayer = enemyDamageToPlayer * (100 - player.DamageReduction) / 100;
            playerHP -= actualToPlayer;
            result.HpDelta += actualToPlayer;

            // 敌人吸血：基数是 enemyPhysical（未过玩家减伤），与实战一致
            int enemySteal = enemyPhysical * enemy.lifeSteal / 100;
            if (enemySteal > 0) enemyHP += enemySteal;

            if (playerHP > 0)
            {
                // 玩家反伤：仅在玩家未因本次伤害死亡时触发（实战同）
                int playerReflect = enemyPhysical * player.ReflectDamage / 100;
                if (playerReflect > 0)
                {
                    enemyHP -= playerReflect * (100 - enemy.damageReduction) / 100;
                    if (enemyHP <= 0) { result.CanWin = true; return result; }
                }
            }
            else
            {
                return result;      // 玩家阵亡 → 判负
            }

            // 敌人消耗魔力并重算
            enemyManaCharge -= EnemyManaCost;
            ComputeRound();
        }

        // 到回合上限仍未分出胜负 → 与实战一致：判负（实战此时日志是「战斗失败...」）
        return result;
    }
}
