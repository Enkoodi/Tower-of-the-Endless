"""一次性脚本：MonsterManualEntryUI.SimulateBattle 改成调用 BattleResolver。

把原来那份「手抄的战斗循环」整段换掉 —— 现在图鉴直接跑实战同一份内核，
伤害/吸血/反伤/减伤/死亡/回合上限永远同步。

按行号替换（边界断言）：第 62-205 行。
"""

import io
import sys

PATH = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Scripts\UI\MonsterManualEntryUI.cs"

START = 62    # 注释块开头
END = 205     # SimulateBattle 的结尾大括号

NEW_CODE = '''    // ========================================================================
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
        /// <summary>能否打赢。打不出伤害、玩家先阵亡、或到回合上限仍未击败敌人 → false。</summary>
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

        // 魔力增幅器：图鉴在战斗外打开，直接读玩家身上的组件
        MagicAmplifier amplifier = player.GetComponent<MagicAmplifier>();
        int magicPercent = amplifier != null ? amplifier.MultiplierPercent : 100;

        // 与实战同一份内核；挂钩一个都不设 → 不跑祝福
        BattleResolver resolver = new BattleResolver(simPlayer, simEnemy, magicPercent);
        resolver.RunToEnd();

        result.CanWin = resolver.EndReason == BattleEndReason.EnemyDefeated;
        result.HpDelta = simPlayer.InitialHP - simPlayer.HP;   // 正数 = 损失，负数 = 回血
        return result;
    }
'''


def main():
    with io.open(PATH, "r", encoding="utf-8", newline="") as f:
        lines = f.readlines()

    if "// =====" not in lines[START - 1] or "战斗模拟" not in lines[START]:
        print(f"[FAIL] 第 {START}-{START+1} 行不是注释块开头：{lines[START - 1]!r} / {lines[START]!r}")
        return 1
    if lines[END - 1].rstrip("\r\n") != "    }":
        print(f"[FAIL] 第 {END} 行不是方法结尾：{lines[END - 1]!r}")
        return 1
    if lines[END].rstrip("\r\n") != "}":
        print(f"[FAIL] 第 {END + 1} 行不是类结尾：{lines[END]!r}")
        return 1

    new_lines = [l + "\n" for l in NEW_CODE.split("\n")]
    out = lines[:START - 1] + new_lines + lines[END:]

    with io.open(PATH, "w", encoding="utf-8", newline="") as f:
        f.writelines(out)

    print(f"[OK] MonsterManualEntryUI.cs：第 {START}-{END} 行（{END - START + 1} 行）替换为 {len(new_lines)} 行")
    print(f"     文件现在共 {len(out)} 行")
    return 0


if __name__ == "__main__":
    sys.exit(main())
