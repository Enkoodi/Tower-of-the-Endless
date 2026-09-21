# 存档 / 祝福 / 数值口径（战前）

## 存档（SaveManager）
主动档 `game_save.json` 与自动档 `auto_save.json` **共用** `SaveGameTo` / `LoadGameFrom`
→ 新字段只写这两个方法，两者天然一致，**不要为自动存档另开分支**。
`specialBlessings` = 当前生效的特殊祝福层数（Conditional 型）；
`obtainedBlessings` = 本局获得清单（含 DirectBonus 型，value = 获得次数），**两份别合并**。
获得祝福的登记点 = `PlayerData.ApplyBlessing`（守卫之后，玩家获得祝福的唯一出口）。
进程内 static / DontDestroyOnLoad 的本局状态（`NPCController.purchaseCounts` 商店购买次数、
`BlessingManager.activeEffects`）**必须在 `SettingMenu.StartNewGame` 里重置**，否则读档→新游戏会串局。
读档对新字段缺失的旧档要兜底（如 obtainedBlessings 为空 → 用 specialBlessings 层数当种子）。

## 全局道具（跨存档）：Aeon 钥匙 / 神圣火花（2026-09-21 补齐 divineSpark）

`save/global.json`（`GlobalSaveData`）= `aeonKeys` / `divineSpark` / `battleSpeed`。两个道具的同步规则**完全一致**：

- **运行时事实来源是 `PlayerData`**（都是 `[SerializeField]`，可在 Inspector 里直接改）：
  `aeonKeys` 在「钥匙数量」区、`divineSpark` 在「全局道具（跨存档保留）」区。
- **进场景 / 读档 = 全局覆盖玩家**：`PlayerData.Start` 与 `SaveManager.LoadGameFrom` 都调
  `ApplyGlobalAeonKeys()` + `ApplyGlobalDivineSpark()`。⇒ 在 Inspector 里改的值会被文件覆盖，**要改趁运行时改**。
- **存档 = 玩家写回全局**：`SaveGlobal()`。`aeonKeys` 没有玩家时写 0（老写法）；`divineSpark` 无玩家时保留旧值、**绝不清零**。
- **拾取写入**：`DivineSparkPickup → SaveManager.AddDivineSpark()` 必须**先改玩家的值再落盘**，
  否则之后 `SaveGlobal()` 会用玩家那边的旧值把这一颗覆盖回去。
- `OpeningMenu` 在 Opening 场景（**那里没有 PlayerData**）直接读 `SaveManager.LoadGlobalData()`（读文件），
  所以「无尽模式解锁」认的是 `global.json`：改完 Inspector 要触发一次存档（P / ESC 进设置 / 上下楼）才会同步过去。
- `HasDivineSpark()` / `GetDivineSparkCount()` 目前**无调用点**（预留）；divineSpark 的唯一消费点是 OpeningMenu 的解锁判定。

## 特殊祝福（Conditional 型）的等级
11 个 Effect 全部有 Level 缩放（Create 映射之外的都是 DirectBonus，没有等级概念）。
重复获得 = 升级：走 `BlessingManager.AddEffect` 里 `existing.AddLevel()` + `OnLevelUp`。
`OnAcquired` **只在首次获得**时调用（`PlayerData.ApplyConditional` 用 `HasEffect` 判定首次）——
升级时传进来的 effect 是新 new 的实例，无条件调用它会让『智慧』每级多 +1000。
"每场战斗重置次数"的状态（KabbalahTree/Demiurge 的 `remainingTriggers`）必须带未初始化回退
（`-1` + 回退到 `Level`），否则战斗外获得后直接上 30 层 / 读档后立即上 30 层会按 0 次算。
asset 的 `description` 是写死的 Lv.1 文案；卡片升级提示由 `BlessingPanel.BuildOwnedHint` 动态追加。

## 战斗数值口径（2026-09-19 定稿，已落地）
1. **战前值与战斗时值是两份数据**。`BeginBattleStats()` 把真实字段快照成 `battleBase*`，
   战斗中一切属性改动**只写修正层，绝不碰真实字段**；`EndBattleStats()` 整体清零（天然自愈）。
2. **实时值 = base + Σ固定值 + base × Σ百分比/100**；百分比之间**加法叠加**（+10% 再 +20% = ×1.30）。
3. 战斗内的固定值**不乘** `attackMultiplier` / `defenseMultiplier`（那是战前成长系数）。
4. 两族写入口，各有守卫，**不要新增第三条直写路径**：
   - 战前族 `Add*` / `ApplyStatBoost` / `ApplyBlessing` → 真实字段，`RequireOutOfBattle`（战斗中硬拦）。
   - 战时族 `AddBattle*`（6 个）→ 修正层，`RequireBattle`。
   - 两个**刻意的例外**：`AddGold`（战斗结算时加奖励金币，拦了会断路径）、`Set*`（读档恢复，不拦）。
   - 存档**一律读 `Base*`**（`Attack` 等在战斗中返回含 buff 的实时值）。
5. 顺序不能颠倒：`StartBattle` 先 `BeginBattleStats()` 再 `BlessingManager.OnBattleStart`；
   轮内 `OnTurnStart` 之后立刻重算伤害；`EndBattle` = `OnBattleEnd` → 恢复魔力 → `EndBattleStats()`。
6. 四个 Multiplier（hp/attack/defense/gold）**只作用于战前成长**（商店/属性道具/祝福获得），
   战斗时的数值一律不走。唯一例外 `AddGold`（战斗奖励）—— 它是 `goldMultiplier` 的唯一消费点，
   按「奖励系数」保留，已报备。
7. **扣血不乘 `hpMultiplier`**；「不该致死」的扣血统一用 `SubtractRawHPKeepAlive()`（最低扣到 1）。
   `SubtractRawHP()`（能扣死的版本）**已删除**。
8. 回血两个接口：`Heal()` 乘 `hpMultiplier`（只给战前成长）；`HealRaw()` 加多少是多少
   （战斗时按公式算的回复：吸血、智慧每回合回血）。历史上吸血误用 `Heal()` 造成两侧不对称。

## 数值口径：取整一律偏向玩家
- **加成**（有符号增量）→ `Mathf.CeilToInt`（+0.7→+1，-0.7→0）。
- **以正数表示的损失量**（如爱的祝福扣血）→ `Mathf.FloorToInt`（玩家少扣）。
- **绝不要用整数除法 `/100`** —— 向下截断会把小加成整个抹掉（攻 7 的 +10% → 0，玩家看不到变化）。
- **例外一**：各处 `* (100 - 减伤) / 100` **保持向下截断**（用户明确要求，不要动）。
- **例外二**：夹击 `Mathf.FloorToInt(hp*0.5f)` 不要改成 ceil ——「不致死」优先于「取整偏向玩家」
  （floor 保证 HP1→伤害 0、HP2→扣 1 剩 1；换 ceil 会直接夹死）。
- **减伤允许 >100%**（系数为负 = 挨打回血），是**有意机制**（吸伤类），**不要加钳制**。
- 带系数的加成接口都**返回实际增量**（`AddAttack/AddDefense/AddHP/AddGold`）——
  要显示「加了多少」用返回值，别自己抄公式（会漂）。

## 术语：魔力值 vs 魔力输出
`manaCharge` = 持有的**魔力值/充能**（战斗消耗、战终恢复）；`manaMax` = 单回合**输出上限**；
单回合实际输出 = `min(manaCharge, manaMax)`，**消耗也是同一个值**。
`PlayerHUD` 显示「魔力输出 = ManaMax」（上限，正确）；`BattleUI` 两块面板是「本回合魔力输出」
（面板在消耗后刷新，除开局外显示的是下一击）；图鉴显示 `enemy.manaMax`。

## 商店购买：不做逆推（2026-09-20 定稿）
`NPCInteractionUI.OnBuyHP/Attack/Defense` 原来是「先除掉系数再调 `AddXxx`」的逆推抵消，
会让系数失效且有两次整数截断（攻 10 + 系数 150% 买 +10% → 到手 0 = 花钱白买）。
**已删逆推**：`rawGain = Mathf.CeilToInt(当前值 × 百分比 / 100f)` 后直接 `AddXxx(rawGain)`，
让系数正常放大（日志打返回值）。副作用（已接受）：出现「先堆系数再买属性」的最优解。
