"""
祝福升级链路模型 —— 复刻 PlayerData.ApplyConditional + BlessingManager.AddEffect 的调用序，
验证 2026-09-20 两处修复的行为差异。改这块代码后请重跑本脚本。

建模的两条链路（与 C# 一一对应）：
  1. 重复获得同一个特殊祝福 = 升级：
       首次 → AddEffect 走 else（登记，Level=1）+ OnAcquired
       升级 → AddEffect 走 if（existing.AddLevel + existing.OnLevelUp）
     旧代码在升级时**无条件**再调一次 effect.OnAcquired（且作用在刚 new 出来、随即被丢弃的实例上）
     → 『智慧』升一级实际 +2000 HP（正确应为 +1000）。
  2. KabbalahTree / Demiurge 的 remainingTriggers 原本只在 OnBattleStart 初始化
     → 战斗外获得后未打仗就上 30 层 / 读档后立即上 30 层 → 次数按 0 算，不触发。
"""

# ---------------- 玩家 / 管理器模型 ----------------

class Player:
    def __init__(self):
        self.hp = 5000
        self.attack_count = 0
        self.damage_reduction = 0

    def heal(self, amount):          # 对应 Heal()（此处只需计数）
        self.hp += amount


class Manager:
    """对应 BlessingManager：activeEffects 字典 + AddEffect"""

    def __init__(self, faithful_on_acquired: bool):
        # faithful_on_acquired=True  → 修复后：OnAcquired 只在首次调用
        # faithful_on_acquired=False → 修复前：每次获得都无条件调 OnAcquired
        self.effects = {}
        self.faithful = faithful_on_acquired

    def add_effect(self, effect_id, effect, player):
        if effect_id in self.effects:
            existing = self.effects[effect_id]
            existing.add_level()
            existing.on_level_up(player)          # ← 真正的升级收益走这里
        else:
            self.effects[effect_id] = effect      # 首次登记，Level 已为 1

    def apply_blessing(self, effect_id, effect, player):
        first = effect_id not in self.effects
        self.add_effect(effect_id, effect, player)
        if first or not self.faithful:
            effect.on_acquired(player)            # ← 修复点：升级时不该再调
        return first


# ---------------- 效果模型（只建与本次修复相关的两个） ----------------

class SophiaEffect:
    HP_PER_LEVEL = 1000

    def __init__(self):
        self.level = 1

    def add_level(self):
        self.level += 1

    def on_acquired(self, player):
        player.heal(self.HP_PER_LEVEL)

    def on_level_up(self, player):
        player.heal(self.HP_PER_LEVEL)


class KabbalahTreeEffect:
    TRIGGER_FLOOR = 30

    def __init__(self, lazy_init: bool):
        self.level = 1
        self.lazy = lazy_init
        self.remaining = -1 if lazy_init else 0   # 修复前 = 0（只在 OnBattleStart 赋值）

    def add_level(self):
        self.level += 1

    @property
    def effective_remaining(self):
        return self.level if (self.lazy and self.remaining < 0) else self.remaining

    def on_acquired(self, player):
        if self.lazy:
            self.remaining = self.level

    def on_level_up(self, player):
        if self.lazy:
            self.remaining = self.level

    def on_battle_start(self, player):
        self.remaining = self.level

    def on_enter_floor(self, player, floor):
        if self.effective_remaining <= 0 or floor < self.TRIGGER_FLOOR:
            return False
        self.remaining = self.effective_remaining - 1
        player.hp *= 2
        player.attack_count += 1
        player.damage_reduction += 10
        return True


# ---------------- 场景 ----------------

def scenario_sophia(faithful: bool, picks: int):
    player, mgr = Player(), Manager(faithful)
    for _ in range(picks):
        mgr.apply_blessing("SophiaBlessing", SophiaEffect(), player)
    effect = mgr.effects["SophiaBlessing"]
    return player.hp - 5000, effect.level


def scenario_kabbalah(lazy: bool, battle_before: bool, picks: int, floor: int):
    player, mgr = Player(), Manager(True)
    for _ in range(picks):
        mgr.apply_blessing("KabbalahTree", KabbalahTreeEffect(lazy), player)
    eff = mgr.effects["KabbalahTree"]
    if battle_before:
        eff.on_battle_start(player)
    fired = eff.on_enter_floor(player, floor)
    return fired, eff.effective_remaining, player.attack_count, player.damage_reduction


print("=" * 76)
print("场景 1：连续 3 次选到『智慧』的祝福（每次 +1000 HP，升级应线性）")
print("=" * 76)
for faithful in (False, True):
    hp_gain, lvl = scenario_sophia(faithful, 3)
    tag = "修复后" if faithful else "修复前"
    print(f"  {tag}：Lv.{lvl}，HP 共 +{hp_gain}（应为 {lvl * 1000}）"
          f"{'  ← 多给了 1 次 OnAcquired' if hp_gain != lvl * 1000 else '  ✓'}")

print()
print("=" * 76)
print("场景 2：卡巴拉生命之树 —— 战斗外获得后直接走到 30 层（没打过任何仗）")
print("=" * 76)
for lazy in (False, True):
    fired, rem, ac, dr = scenario_kabbalah(lazy, battle_before=False, picks=1, floor=30)
    tag = "修复后" if lazy else "修复前"
    print(f"  {tag}：触发={fired}，剩余={rem}，段数+{ac}，减伤+{dr}"
          f"{'  ← 战斗外获得，次数没初始化，整条祝福白给' if not fired else '  ✓'}")

print()
print("=" * 76)
print("场景 3：战斗外获得 → 先打一场 → 再上 30 层（回归检查，行为应与修复前一致）")
print("=" * 76)
for lazy in (False, True):
    fired, rem, ac, dr = scenario_kabbalah(lazy, battle_before=True, picks=1, floor=30)
    tag = "修复后" if lazy else "修复前"
    print(f"  {tag}：触发={fired}，剩余={rem}，段数+{ac}，减伤+{dr}")

print()
print("=" * 76)
print("场景 4：连拿 2 层后直接上 30 层（次数应 = 层数 = 2）")
print("=" * 76)
for lazy in (False, True):
    fired, rem, ac, dr = scenario_kabbalah(lazy, battle_before=False, picks=2, floor=30)
    tag = "修复后" if lazy else "修复前"
    print(f"  {tag}：第 1 次触发={fired}，剩余={rem}，段数+{ac}")
