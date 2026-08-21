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
    /// <param name="player">当前玩家数据（用于计算战斗损失）</param>
    public void Setup(EnemyStats enemy, PlayerData player)
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
            int hpLoss = SimulateBattle(player, enemy);
            if (hpLoss < 0)
            {
                requiredHPText.text = "<color=#FF4444>无法战胜</color>";
            }
            else
            {
                requiredHPText.text = $"损失: <color=#FFDD88>{hpLoss}</color> HP";
            }
        }
    }

    // ========================================================================
    //  战斗模拟（与 BattleManager 逻辑一致，但不执行实际扣血和动画）
    // ========================================================================

    /// <summary>
    /// 模拟与敌人的战斗，返回玩家会损失的总HP。
    /// 返回 -1 表示真正无法战胜（无法破防或敌人无法被击杀）。
    /// 损失HP超过玩家当前HP时仍返回实际数值，而非 -1。
    /// </summary>
    public static int SimulateBattle(PlayerData player, EnemyStats enemy)
    {
        if (player == null || enemy == null) return -1;

        int enemyHP          = enemy.hp;
        int playerManaCharge = player.ManaCharge;
        int enemyManaCharge  = enemy.manaCharge;

        // 物理伤害（每轮不变）
        int playerPhysical = Mathf.Max(0, (player.Attack - enemy.defense) * player.AttackCount);
        int enemyPhysical  = Mathf.Max(0, (enemy.attack - player.Defense) * enemy.attackCount);

        int ComputePlayerDmg()
        {
            int mana = Mathf.Min(playerManaCharge, player.ManaMax);
            return playerPhysical + mana;
        }
        int ComputeEnemyDmg()
        {
            int mana = Mathf.Min(enemyManaCharge, enemy.manaMax);
            return enemyPhysical + mana;
        }

        int playerDmg = ComputePlayerDmg();
        int enemyDmg  = ComputeEnemyDmg();

        // 无法破防 → 真正无法战胜
        if (playerDmg <= 0) return -1;

        int totalHPLost = 0;
        const int maxTurns = 10000;

        // 先手判定：敌人速度更高则偷袭
        if (enemy.speed > player.Speed)
        {
            int sneakDamage = enemyDmg * 2;
            int actualSneak = sneakDamage * (100 - player.DamageReduction) / 100;
            totalHPLost += actualSneak;
        }

        for (int turn = 0; turn < maxTurns; turn++)
        {
            // —— 玩家攻击 ——
            int actualToEnemy = playerDmg * (100 - enemy.damageReduction) / 100;
            enemyHP -= actualToEnemy;

            // 吸血（不影响 totalHPLost，但记录到回合中 — 注意这里不再追踪玩家HP，只算损失）
            // 反伤
            int reflect = playerPhysical * enemy.reflectDamage / 100;
            if (reflect > 0)
            {
                int reflectActual = reflect * (100 - player.DamageReduction) / 100;
                totalHPLost += reflectActual;
            }

            // 消耗魔力并重算
            int manaCost = Mathf.Min(playerManaCharge, player.ManaMax);
            playerManaCharge -= manaCost;
            playerDmg = ComputePlayerDmg();
            enemyDmg  = ComputeEnemyDmg();

            if (enemyHP <= 0) return totalHPLost;

            // —— 敌人反击 ——
            int actualToPlayer = enemyDmg * (100 - player.DamageReduction) / 100;
            totalHPLost += actualToPlayer;

            // 敌人吸血
            int enemySteal = actualToPlayer * enemy.lifeSteal / 100;
            if (enemySteal > 0) enemyHP += enemySteal;

            // 玩家反伤
            int playerReflect = enemyPhysical * player.ReflectDamage / 100;
            if (playerReflect > 0)
            {
                int reflectActual = playerReflect * (100 - enemy.damageReduction) / 100;
                enemyHP -= reflectActual;
                if (enemyHP <= 0) return totalHPLost;
            }

            // 敌人消耗魔力并重算
            int enemyManaCost = Mathf.Min(enemyManaCharge, enemy.manaMax);
            enemyManaCharge -= enemyManaCost;
            playerDmg = ComputePlayerDmg();
            enemyDmg  = ComputeEnemyDmg();
        }

        // 超过最大回合数仍未能击杀 → 无法战胜（如敌人回血超过输出）
        return -1;
    }
}
