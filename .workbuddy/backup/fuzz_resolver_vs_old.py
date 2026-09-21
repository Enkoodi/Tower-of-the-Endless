"""对拍验证：旧 BattleCoroutine 的结算逻辑 vs 新 BattleResolver 的结算逻辑。

两边都按 C# 源码逐行转写成 Python（祝福一律不跑，两边口径一致），
用随机输入对拍 (胜负, 双方终血, 回合数)。任何不一致都说明转写时改了语义。

旧代码来自 .workbuddy/backup/BattleManager.cs.20260920-2149.kernel.bak
新代码来自 Assets/Scripts/Battle/BattleResolver.cs

2026-09-20 追加：新内核把「打不出伤害就判负」放宽成
「自己打不出伤害、但**反伤**打得动，就照常开打」。
所以差异分成三类：
  · 仅回合数不同                      → 已知差异，忽略
  · 旧版判「伤不到敌人」而新版能反伤   → **预期的行为变化**
  · 其它胜负/血量不同                  → 真·转写错误
"""

import random
import sys

MAX_TURNS = 200


class Unit:
    """对应 IBattleUnit，行为照抄 PlayerData.SubtractHP / EnemyController.TakeRawDamage。"""

    def __init__(self, hp, attack, defense, count, lifesteal, reflect, dr,
                 mana_charge, mana_max, speed):
        self.hp = hp
        self.initial_hp = hp
        self.attack = attack
        self.defense = defense
        self.count = count
        self.lifesteal = lifesteal
        self.reflect = reflect
        self.dr = dr
        self.mana_charge = mana_charge
        self.mana_max = mana_max
        self.speed = speed

    def receive_damage(self, amount):
        """amount * (100 - dr) / 100，整数除法；HP 下限 0"""
        reduced = int(amount * (100 - self.dr) / 100)   # C# 整数除法 = 向零截断
        self.hp -= reduced
        if self.hp < 0:
            self.hp = 0
        return reduced

    def heal_raw(self, amount):
        if amount <= 0:
            return
        self.hp += amount


def clone_unit(u):
    """战斗会把单位打残，所以对拍/用例一律用副本。"""
    return Unit(u.hp, u.attack, u.defense, u.count, u.lifesteal, u.reflect, u.dr,
                u.mana_charge, u.mana_max, u.speed)


def can_damage_enemy(p, e, magic_percent):
    """新版 CanDamageEnemy：直接伤害 > 0，或者反伤打得动。构造时定格。"""
    player_physical = max(0, (p.attack - e.defense) * p.count)
    mana_cost = min(p.mana_charge, p.mana_max)
    magic = int(mana_cost * magic_percent / 100)
    if player_physical + magic > 0:
        return True

    # 反伤：敌人物理伤害 × 玩家反伤系数，再过敌人减伤
    enemy_physical = max(0, (e.attack - p.defense) * e.count)
    if p.reflect <= 0 or enemy_physical <= 0:
        return False
    reflect = int(enemy_physical * p.reflect / 100)
    if reflect <= 0:
        return False
    return int(reflect * (100 - e.dr) / 100) > 0


def sim_old(p, e, magic_percent):
    """旧 BattleCoroutine（去掉祝福调用后的纯结算）。返回 (won, p.hp, e.hp, turns, reason)"""
    turns = 1

    player_physical = mana_cost = player_magic = dmg_to_enemy = 0
    enemy_physical = enemy_mana_cost = dmg_to_player = 0

    def compute_round():
        nonlocal player_physical, mana_cost, player_magic, dmg_to_enemy
        nonlocal enemy_physical, enemy_mana_cost, dmg_to_player
        player_physical = max(0, (p.attack - e.defense) * p.count)
        mana_cost = min(p.mana_charge, p.mana_max)
        player_magic = int(mana_cost * magic_percent / 100)
        dmg_to_enemy = player_physical + player_magic

        enemy_physical = max(0, (e.attack - p.defense) * e.count)
        enemy_mana_cost = min(e.mana_charge, e.mana_max)
        dmg_to_player = enemy_physical + enemy_mana_cost

    compute_round()
    # 旧代码里物理伤害在循环开头也算一次；这里按「每轮重算」的最终形态转写
    # （重构前 BattleManager 已经是每轮重算，见 ComputeRoundDamage）

    # 先手偷袭
    if e.speed > p.speed:
        actual = p.receive_damage(dmg_to_player * 2)
        if p.hp <= 0:
            return (False, p.hp, e.hp, turns, "player")

    if dmg_to_enemy <= 0:
        return (False, p.hp, e.hp, turns, "unkillable")

    while e.hp > 0 and p.hp > 0:
        # —— 回合开始 ——
        compute_round()
        if e.hp <= 0:
            return (True, p.hp, e.hp, turns, "enemy")

        # —— 玩家攻击 ——
        e.receive_damage(dmg_to_enemy)
        # 魔力消耗（无祝福 → 恒为 true）
        p.mana_charge -= mana_cost
        # 吸血
        steal = int(player_physical * p.lifesteal / 100)
        p.heal_raw(steal)
        # 敌人反伤
        if e.hp > 0:
            reflect = int(player_physical * e.reflect / 100)
            if reflect > 0:
                p.receive_damage(reflect)
        compute_round()

        if p.hp <= 0:
            return (False, p.hp, e.hp, turns, "player")
        if e.hp <= 0:
            return (True, p.hp, e.hp, turns, "enemy")

        # —— 敌人反击 ——
        p.receive_damage(dmg_to_player)
        e.mana_charge -= enemy_mana_cost
        enemy_steal = int(enemy_physical * e.lifesteal / 100)
        e.heal_raw(enemy_steal)
        if p.hp > 0:
            player_reflect = int(enemy_physical * p.reflect / 100)
            if player_reflect > 0:
                e.receive_damage(player_reflect)
        compute_round()

        if p.hp <= 0:
            return (False, p.hp, e.hp, turns, "player")

        turns += 1
        if turns > MAX_TURNS:
            return (False, p.hp, e.hp, turns, "stalemate")

    return (p.hp > 0, p.hp, e.hp, turns, "enemy" if p.hp > 0 else "player")


def sim_new(p, e, magic_percent):
    """新 BattleResolver（无挂钩 = 不跑祝福）。返回 (can_win, p.hp, e.hp, turns)"""
    turns = 1
    state = {"phase": "Sneak" if e.speed > p.speed else "TurnStart", "done": False}

    player_physical = mana_cost = player_magic = dmg_to_enemy = 0
    enemy_physical = enemy_mana_cost = dmg_to_player = 0

    def compute_round():
        nonlocal player_physical, mana_cost, player_magic, dmg_to_enemy
        nonlocal enemy_physical, enemy_mana_cost, dmg_to_player
        player_physical = max(0, (p.attack - e.defense) * p.count)
        mana_cost = min(p.mana_charge, p.mana_max)
        player_magic = int(mana_cost * magic_percent / 100)
        dmg_to_enemy = player_physical + player_magic
        enemy_physical = max(0, (e.attack - p.defense) * e.count)
        enemy_mana_cost = min(e.mana_charge, e.mana_max)
        dmg_to_player = enemy_physical + enemy_mana_cost

    compute_round()
    # 构造时定格，之后重算不影响 —— 自己打不出伤害时，反伤算第二条路
    can_damage = can_damage_enemy(p, e, magic_percent)
    reason = None

    def step():
        nonlocal reason, turns
        if state["done"]:
            return False
        # 偷袭之后才判「伤不到敌人」（与旧实现一致：先挨偷袭，再收场）
        if state["phase"] != "Sneak" and not can_damage:
            reason = "unkillable"
            state["done"] = True
            return False
        ph = state["phase"]
        if ph == "Sneak":
            p.receive_damage(dmg_to_player * 2)
            if p.hp <= 0:
                reason = "player"
                state["done"] = True
            state["phase"] = "TurnStart"
        elif ph == "TurnStart":
            compute_round()
            if e.hp <= 0:
                reason = "enemy"
                state["done"] = True
            state["phase"] = "PlayerAttack"
        elif ph == "PlayerAttack":
            e.receive_damage(dmg_to_enemy)
            p.mana_charge -= mana_cost
            steal = int(player_physical * p.lifesteal / 100)
            if steal > 0:
                p.heal_raw(steal)
            if e.hp > 0:
                reflect = int(player_physical * e.reflect / 100)
                if reflect > 0:
                    p.receive_damage(reflect)
            compute_round()
            if p.hp <= 0:
                reason = "player"
                state["done"] = True
            elif e.hp <= 0:
                reason = "enemy"
                state["done"] = True
            state["phase"] = "EnemyCounter"
        elif ph == "EnemyCounter":
            p.receive_damage(dmg_to_player)
            e.mana_charge -= enemy_mana_cost
            enemy_steal = int(enemy_physical * e.lifesteal / 100)
            if enemy_steal > 0:
                e.heal_raw(enemy_steal)
            if p.hp > 0:
                player_reflect = int(enemy_physical * p.reflect / 100)
                if player_reflect > 0:
                    e.receive_damage(player_reflect)
            compute_round()
            if e.hp <= 0:
                reason = "enemy"
                state["done"] = True
            elif p.hp <= 0:
                reason = "player"
                state["done"] = True
            state["phase"] = "TurnEnd"
        elif ph == "TurnEnd":
            turns += 1
            if turns > MAX_TURNS:
                reason = "stalemate"
                state["done"] = True
            state["phase"] = "TurnStart"
        return True

    while step():
        if state["done"]:
            break

    return (reason == "enemy", p.hp, e.hp, turns, reason)


def rnd_unit(rng, hp_max, atk_max, def_max, scale):
    return Unit(
        hp=rng.randint(1, hp_max),
        attack=rng.randint(0, atk_max),
        defense=rng.randint(0, def_max),
        count=rng.randint(1, 4),
        lifesteal=rng.randint(0, 60),
        reflect=rng.randint(0, 60),
        dr=rng.randint(0, 90),
        mana_charge=rng.randint(0, 500),
        mana_max=rng.randint(1, 500),
        speed=rng.randint(1, 200),
    )


def scenario_checks():
    """针对本次改动的手工用例：打不出伤害时该不该照常开打。"""
    print("=" * 62)
    print("用例检查：自己打 0 伤害时，能不能靠反伤打下去")
    print("=" * 62)

    cases = []

    # ① 打不动（攻10 vs 防100）、但反伤 200% → 应能反杀
    p = Unit(1000, 10, 10, 1, 0, 200, 0, 0, 0, 100)
    e = Unit(100, 50, 100, 1, 0, 0, 0, 0, 0, 50)
    cases.append(("反伤 200% 反杀", p, e, True))

    # ② 同样打不动、反伤 0 → 仍应立刻判负
    p = Unit(1000, 10, 10, 1, 0, 0, 0, 0, 0, 100)
    e = Unit(100, 50, 100, 1, 0, 0, 0, 0, 0, 50)
    cases.append(("无反伤 → 判负", p, e, False))

    # ③ 反伤有，但敌人减伤 100% → 反伤打不进去，仍判负
    p = Unit(1000, 10, 10, 1, 0, 200, 0, 0, 0, 100)
    e = Unit(100, 50, 100, 1, 0, 0, 100, 0, 0, 50)
    cases.append(("反伤被 100% 减伤吃掉", p, e, False))

    # ④ 反伤有，但敌人完全打不出物理伤害（魔力伤害不触发反伤）→ 判负
    p = Unit(1000, 10, 10, 1, 0, 200, 0, 0, 0, 100)
    e = Unit(100, 50, 100, 1, 0, 0, 0, 500, 500, 50)
    p.defense = 999          # 敌人物理打不动玩家，只能靠魔力
    cases.append(("敌人只有魔力伤害", p, e, False))

    # ⑤ 反伤这条路成立、但反伤太慢打不完 → 交给僵持上限，不提前判负
    p = Unit(50, 10, 10, 1, 0, 200, 0, 0, 0, 100)
    e = Unit(100000, 50, 100, 1, 0, 0, 0, 0, 0, 50)
    cases.append(("反伤打得动但会先被打死", p, e, True))   # can_damage 为真 → 照常开打

    ok = True
    for name, pp, ee, expect_can in cases:
        can = can_damage_enemy(pp, ee, 100)
        sim = sim_new(clone_unit(pp), clone_unit(ee), 100)
        mark = "OK " if can == expect_can else "!! "
        if can != expect_can:
            ok = False
        print(f"  {mark}{name:<22} can_damage={str(can):<5} 预期={str(expect_can):<5}"
              f" → 结算 {sim[4]} 玩家HP={sim[1]} 敌人HP={sim[2]} 回合={sim[3]}")

    print()
    return ok


def main():
    rng = random.Random(20260920)
    trials = 30000

    only_turns = 0        # 只有回合数不同、胜负与双方血量都相同
    expected = 0          # 旧版「伤不到敌人」而新版有反伤这条路 —— 本次改动预期产生
    real_diff = 0         # 其它胜负或血量不同 —— 那才是真的转写错误
    samples = []
    exp_samples = []

    for i in range(trials):
        p = rnd_unit(rng, 3000, 200, 100, 1)
        e = rnd_unit(rng, 3000, 200, 100, 1)
        magic = rng.choice([100, 100, 150, 200, 300])

        def clone(u):
            return Unit(u.hp, u.attack, u.defense, u.count, u.lifesteal, u.reflect, u.dr,
                        u.mana_charge, u.mana_max, u.speed)

        old = sim_old(clone(p), clone(e), magic)
        new = sim_new(clone(p), clone(e), magic)

        same_core = (old[0], old[1], old[2]) == (new[0], new[1], new[2])

        if same_core and old[3] == new[3]:
            continue

        # 只比较 (胜负, 双方终血)
        if same_core:
            only_turns += 1
        elif old[4] == "unkillable" and can_damage_enemy(p, e, magic):
            # 旧版：打不出伤害 → 当场判负。新版：反伤打得动 → 照常开打，结果自然不同
            expected += 1
            if len(exp_samples) < 3:
                exp_samples.append((magic, p, e, old, new))
        else:
            real_diff += 1
            if len(samples) < 5:
                samples.append((magic, p, e, old, new))

    for magic, p, e, old, new in exp_samples:
        print(f"[预期变化] magic={magic}")
        print(f"   玩家 HP{p.hp} 攻{p.attack} 防{p.defense} 段{p.count} 吸{p.lifesteal} 反{p.reflect} 减伤{p.dr} 魔{p.mana_charge}/{p.mana_max} 速{p.speed}")
        print(f"   敌人 HP{e.hp} 攻{e.attack} 防{e.defense} 段{e.count} 吸{e.lifesteal} 反{e.reflect} 减伤{e.dr} 魔{e.mana_charge}/{e.mana_max} 速{e.speed}")
        print(f"   旧(won,php,ehp,turn,reason)={old}")
        print(f"   新(canwin,php,ehp,turn,reason)={new}")

    for magic, p, e, old, new in samples:
        print(f"[真差异] magic={magic}")
        print(f"   玩家 HP{p.hp} 攻{p.attack} 防{p.defense} 段{p.count} 吸{p.lifesteal} 反{p.reflect} 减伤{p.dr} 魔{p.mana_charge}/{p.mana_max} 速{p.speed}")
        print(f"   敌人 HP{e.hp} 攻{e.attack} 防{e.defense} 段{e.count} 吸{e.lifesteal} 反{e.reflect} 减伤{e.dr} 魔{e.mana_charge}/{e.mana_max} 速{e.speed}")
        print(f"   旧(won,php,ehp,turn,reason)={old}  新(canwin,php,ehp,turn,reason)={new}")

    print()
    print(f"总样本 {trials}")
    print(f"  仅回合数不同（胜负与双方终血完全一致）：{only_turns}")
    print(f"  预期变化（旧版伤不到敌人、新版靠反伤开打）：{expected}")
    print(f"  真差异（胜负或血量不同 = 转写错误）：{real_diff}")
    print()

    scenarios_ok = scenario_checks()
    return 0 if real_diff == 0 and scenarios_ok else 1


if __name__ == "__main__":
    sys.exit(main())
