"""一次性脚本：把 BattleManager.BattleCoroutine 改成「驱动 BattleResolver + 演出」。

按行号替换（边界断言），因为要换掉 ~196 行，文本精确匹配太脆。
新代码严格保留原有的演出节奏：只有 Sneak / PlayerAttack / EnemyCounter 会停顿一拍，
TurnStart / TurnEnd 不停顿（否则每回合会平白多出两拍）。
"""

import io
import sys

PATH = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Scripts\Core\BattleManager.cs"

START = 168   # private IEnumerator BattleCoroutine(...)
END = 363     # 该协程的结束大括号

NEW_CODE = '''    private IEnumerator BattleCoroutine(PlayerData playerData, EnemyController enemy,
                                        int playerManaSnapshot, int enemyManaSnapshot)
    {
        // 魔力增幅器倍率（拾取魔力增幅器后生效；只放大魔力伤害，不影响魔力消耗）
        MagicAmplifier amp = playerData.GetComponent<MagicAmplifier>();
        int magicPercent = amp != null ? amp.MultiplierPercent : 100;

        // 结算内核 —— 实战与图鉴共用同一份，这里只负责把每一步「演」出来。
        // 祝福必须挂在回调里、由内核在正确时机调用，不能挪到表现层：
        // 工匠的濒死回复要在死亡判定之前生效。
        BattleResolver resolver = new BattleResolver(playerData, enemy, magicPercent)
        {
            OnTurnStart        = () => BlessingManager.Instance?.OnTurnStart(playerData, enemy, battleUI),
            OnTurnEnd          = () => BlessingManager.Instance?.OnTurnEnd(playerData, enemy, battleUI),
            ShouldConsumeMana  = () => BlessingManager.Instance?.ShouldConsumeMana(playerData) ?? true,
            OnPlayerDealDamage = d => BlessingManager.Instance?.OnPlayerDealDamage(playerData, enemy, battleUI, d),
            OnEnemyTakeDamage  = d => BlessingManager.Instance?.OnEnemyTakeDamage(playerData, enemy, battleUI, d),
            OnEnemyDealDamage  = d => BlessingManager.Instance?.OnEnemyDealDamage(playerData, enemy, battleUI, d),
            OnPlayerTakeDamage = d => BlessingManager.Instance?.OnPlayerTakeDamage(playerData, enemy, battleUI, d),
        };

        // —— 先手偷袭 ——（伤害在内核的 Sneak 步里结算，这里先播一句预告）
        if (resolver.WillSneak)
        {
            battleUI.AddLog($"受到<color=#779977>{enemy.EnemyName}</color>偷袭！");
            if (!silentBattle) yield return new WaitForSeconds(logDelay);

            resolver.Step();
            PlaySneak(resolver.LastStep, playerData);
            battleUI.UpdatePlayerPanel(playerData);
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
        }
        else
        {
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
        }

        // 被偷袭打死 → 判负
        if (resolver.IsFinished)
        {
            EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
            yield break;
        }

        // 破不了防 → 立刻收场判负。用伤害构成而不是一个光秃秃的数字，让玩家看出为什么打不动
        if (!resolver.CanBreakThrough)
        {
            battleUI.AddLog($"<color=#7799CC>玩家</color>攻击，造成 {FormatDamage(playerData.AttackCount, resolver.PlayerPhysical, resolver.PlayerManaCost, resolver.PlayerPhysical + resolver.PlayerMagicDamage)} 点伤害");
            playerData.PlayAttackAnimation();
            if (!silentBattle) yield return new WaitForSeconds(logDelay);
            EndBattle(false, playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
            yield break;
        }

        // —— 主循环：内核推进一步，这里演一步 ——
        while (resolver.Step())
        {
            BattleStep step = resolver.LastStep;

            switch (step.Kind)
            {
                case BattleStepKind.TurnStart:
                    // 回合开始本身没有演出，也不停顿 —— 停了每回合会平白多出两拍。
                    // 只有被深渊/静默提前打死时才说一句。
                    if (step.EnemyDied)
                    {
                        battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;

                case BattleStepKind.PlayerAttack:
                    PlayPlayerAttack(step, playerData, enemy);
                    if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    // 打死敌人的宣告往后挪一拍，保留「先看到伤害、再看到击败」的原节奏
                    if (step.EnemyDied)
                    {
                        battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;

                case BattleStepKind.EnemyCounter:
                    PlayEnemyCounter(step, playerData, enemy);
                    if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    if (step.EnemyDied)
                    {
                        battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 被击败！");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;

                case BattleStepKind.TurnEnd:
                    battleUI.UpdateTurn(step.Turn);
                    // 僵持保护：双方都打不动对方时内核会判 Stalemate
                    if (resolver.EndReason == BattleEndReason.Stalemate)
                    {
                        Debug.LogWarning($"[BattleManager] 与 {enemy.EnemyName} 的战斗超过 {MaxTurnCount} 回合仍未分出胜负，强制收场");
                        battleUI.AddLog($"战斗陷入僵持，与 <color=#779977>{enemy.EnemyName}</color> 的战斗中止");
                        if (!silentBattle) yield return new WaitForSeconds(logDelay);
                    }
                    break;
            }

            if (resolver.IsFinished) break;
        }

        EndBattle(resolver.EndReason == BattleEndReason.EnemyDefeated,
                  playerData, enemy, playerManaSnapshot, enemyManaSnapshot);
    }

    // ============================================================
    //  演出 —— 把内核推进的一步变成日志 / 动画 / 面板刷新。
    //  只做表现，不改任何战斗数值；所有数字都从 BattleStep 里取。
    // ============================================================

    private void PlaySneak(BattleStep step, PlayerData playerData)
    {
        // 被偷袭算受伤
        if (!silentBattle && step.DamageToPlayer > 0)
            playerData.PlayHurtAnimation();

        if (step.DamageToPlayer <= 0)
            battleUI.AddLog($"偷袭似乎不起作用");
        else
            battleUI.AddLog($"<color=#7799CC>玩家</color>受到 <color=#FF4444>{step.DamageToPlayer}</color> 点伤害");
    }

    private void PlayPlayerAttack(BattleStep step, PlayerData playerData, EnemyController enemy)
    {
        playerData.PlayAttackAnimation();
        // 敌人受击表现：等头像冲到敌人面前才抖一下并叠出斩击（时序在 BattleUI 内控制）
        if (step.DamageToEnemy > 0)
            battleUI.PlayEnemyHit(playerData.AttackCount);
        battleUI.AddLog($"<color=#7799CC>玩家</color>攻击，造成 <color=#FF4444>{step.DamageToEnemy} </color>点伤害");
        battleUI.UpdateEnemyPanel(enemy);

        if (!step.ManaConsumed)
            battleUI.AddLog($"<color=#88CCFF>灵知的祝福</color>：本回合不消耗魔力充能");

        battleUI.UpdatePlayerPanel(playerData);
    }

    private void PlayEnemyCounter(BattleStep step, PlayerData playerData, EnemyController enemy)
    {
        // 被反击算受伤
        if (!silentBattle && step.DamageToPlayer > 0)
            playerData.PlayHurtAnimation();

        // 敌人完全破不了防（伤害为 0）时显示伤害构成，
        // 与玩家侧「无法破防」那条日志对称 —— 否则只报一个 0，看不出是物理是 0 还是魔力是 0
        if (step.AttackerRawDamage <= 0)
            battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 反击，未能破防（{FormatDamage(step.AttackerAttackCount, step.AttackerPhysical, step.AttackerManaCost, step.AttackerRawDamage)}）");
        else
            battleUI.AddLog($"<color=#779977>{enemy.EnemyName}</color> 反击，造成 <color=#FF4444>{step.DamageToPlayer}</color> 点伤害");

        battleUI.UpdateEnemyPanel(enemy);
        battleUI.UpdatePlayerPanel(playerData);
    }
'''


def main():
    with io.open(PATH, "r", encoding="utf-8", newline="") as f:
        lines = f.readlines()

    first = lines[START - 1]
    last = lines[END - 1]

    if "private IEnumerator BattleCoroutine(" not in first:
        print(f"[FAIL] 第 {START} 行不是协程声明：{first!r}")
        return 1
    if last.rstrip("\r\n") != "    }":
        print(f"[FAIL] 第 {END} 行不是协程结尾：{last!r}")
        return 1

    new_lines = [l + "\n" for l in NEW_CODE.split("\n")]
    out = lines[:START - 1] + new_lines + lines[END:]

    with io.open(PATH, "w", encoding="utf-8", newline="") as f:
        f.writelines(out)

    print(f"[OK] BattleManager.cs：第 {START}-{END} 行（{END - START + 1} 行）替换为 {len(new_lines)} 行")
    for i in range(START - 4, START + 6):
        print(f"     {i + 1:>4} | {out[i].rstrip()}")
    print("     ...")
    for i in range(START + len(new_lines) - 2, START + len(new_lines) + 4):
        print(f"     {i + 1:>4} | {out[i].rstrip()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
