# 战斗结算内核 `Battle/BattleResolver.cs`（实战与图鉴共用，结算逻辑只有这一份）

文件：`IBattleUnit`（最小读写面）/ `BattleResolver`（`Step()` 一步一停、`LastStep`、`EndReason`、
`RunToEnd()`）/ `SimBattleUnit`（图鉴的纯数据副本，绝不碰真身）。
实现类：`PlayerData`（`ReceiveDamage => SubtractHP`）、`EnemyController`（`ReceiveDamage => TakeRawDamage`，
**不是**它的 `SubtractHP`，那是真实伤害不过减伤）、`SimBattleUnit`。

两条硬约束：
1. **必须一步一停**，不能先算完再回放（祝福会在回合之间改状态，面板会跳到最终值）。
2. **祝福回调必须在内核里调用**（经 `OnTurnStart` 等挂钩），不能挪到表现层 —— 工匠的濒死回复
   要在死亡判定之前生效。图鉴一个挂钩都不设 → 白板对打（P2 唯一残留偏差）。

踩过的坑（别重犯）：
- `CanDamageEnemy` **必须构造时定格**，不能写成读实时伤害的属性（魔力用光会误判）。
- 「伤不到敌人」的判定必须在**偷袭之后**（否则凭空少掉偷袭那一次伤害）。
- `LastStep` 是自动属性 → 结构体属性返回副本，**不能** `LastStep.X = v`（CS1612）；
  先拼局部变量再整体赋值。
- 表现层要用的数字一律从 `BattleStep` 取 —— 步骤末尾会重算，再去读内核属性就晚了。
- 玩家反伤打死敌人时，新内核在 `EnemyCounter` 步就收场（旧实现要多跑一次 `OnTurnStart`）。
  **新行为更合理，有意保留。**

`CanDamageEnemy`（2026-09-20 放宽）= ① 自己打得出伤害（物理 + 魔力）**或** ② **反伤打得动**。
反伤吃的是敌人的**物理**伤害，成立条件：敌人打得出物理伤害 + 玩家反伤系数 > 0 + 反伤过了敌人减伤仍为正。
所以「这一击打 0」不再判负 —— 两条路都断才提前收场（`EnemyUnkillable`）。
`ReflectToEnemy` = 构造时算出的「每回合反伤实伤」。已知未做的精度：没算「敌人吸血 > 反伤」的净进展，
那种局会跑到 200 回合上限判僵持。
**反伤一律不在战斗日志里显示**（用户 2026-09-20 明确要求：不需要在游戏内打印反伤日志）
—— 改这块时别顺手给反伤加 `AddLog`。

**验证手段（再动结算逻辑先跑它）**：`.workbuddy/backup/fuzz_resolver_vs_old.py`
—— 新旧两版结算分别转写成 Python 对拍 30000 组 (胜负, 双方终血, 回合数)。它抓到过上面两个坑。
差异分类：仅回合数 625（已知）/ 预期变化 10 / 真差异 0。

## 图鉴（怪物手册）
`MonsterManualEntryUI.SimulateBattle` 跑同一个内核 → 公式/取整/吸血/反伤/死亡判定/回合上限
**永远不会与实战漂**。返回 `BattleSimResult { bool CanWin; int HpDelta; }`（`HpDelta>0` 损失，
`<0` 绿字负数 = 反而回血；取代了旧 `-1` 哨兵）。**唯一残留偏差 = 不跑祝福**；
要消掉得先把 `BlessingEffect` 的入参从 MonoBehaviour 解耦 —— **不要在图鉴里抄第二遍祝福规则**。

## 战斗模块技术债
- ✅ P1 死符号清理。✅ P2 结算合并（剩「图鉴不跑祝福」）。✅ P4-1 战前族硬拦截。
- ⬜ P3 僵持判负措辞（实战「战斗失败」vs 图鉴「无法战胜」）。
- ⬜ `ApplyBlessing` 的两个分支仍直写字段（目前只会在战斗外触发）。
