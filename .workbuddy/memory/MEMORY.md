# Tower of the Endless — 项目长期备忘

## 工程环境
- Unity 项目，路径 `D:\zzzMyWork\Game\unity\Tower of the Endless`。
- 5 个场景都在 Build Settings 里 enabled：Opening / Game / Credits / Ending / Setting。
- 改场景 `.unity` 前先查 `Unity.exe` 进程和 `Temp/` 目录：本机常只有 Unity Hub 在跑，
  编辑器没开时改 YAML 是安全的。

## UI 按钮交互统一约定
- 菜单按钮统一挂 **`OpeningButton`**：外框是 Awake 里用 4 条无 sprite 的 Image 细条
  **代码画出来的**（FrameTop/Bottom/Left/Right，thickness=6，raycastTarget=false），
  不依赖任何图片资源。`SetLocked(bool)` = 常驻选中框，用于互斥选择。
- 按钮的 Image 一律 **alpha=0 全透明**当热区，可见画面来自父级 Panel 的 sprite。
- **绑定方式约定：`[SerializeField]` 拖引用 + `Awake` 里代码 `AddListener`**，
  不要用 `switch(button.name)` 那套。名字匹配的失败是运行时静默的。
  详见 `Assets/Scripts/UI/SettingMenu.cs`（2026-09-18 重构）。
- **控制器挂载位置约定：挂在「最小能包含它管辖的全部 UI 的节点」上**，
  不要挂 Canvas。Setting 场景里 `SettingMenu` 挂在 **Panel** 上（2026-09-19 从 Canvas 搬下来）。
  挂 Canvas 会让 `GetComponentsInChildren<Button>()` 扫到无关 UI。
- 场景跳转统一走 `ScreenFader.FadeToScene(name[, duration])`。
- ESC 开关设置走 `SettingsToggle` 单例（`RuntimeInitializeOnLoadMethod` 自建 + DontDestroyOnLoad）。

## 战斗速度档位
- 4 档：正常 1f / 两倍 0.5f / 四倍 0.25f / **跳过 0.01f**。
- 0.01 会命中 `BattleManager.IsSkipMode`（`logDelay <= skipDelayThreshold`）
  → 战斗只结算，不打开战斗界面。
- 档位值存在 `OpeningButton.speedDelay`（Inspector），**不再靠按钮名字反查**。
- 持久化在全局存档 `battleSpeed` 字段（`SaveManager.SaveBattleSpeed/LoadBattleSpeed`）。
  进场景时按「最近档位」匹配，不依赖浮点精确相等。

## 战斗数值口径：战斗前快照 + 战斗内修正层（2026-09-19 定稿，已落地）

**核心规则**：
1. **战斗前值和战斗时值是两份数据。** 战斗开始时 `PlayerData.BeginBattleStats()`
   把真实字段快照成 `battleBase*`，战斗中一切属性改动**只写修正层，绝不碰真实字段**。
2. **实时值 = base + Σ固定值 + base × Σ百分比 / 100。**
   百分比之间是**加法叠加**：先 +10% 再 +20% → `base×1.30`，**不是** `base×1.1×1.2=1.32`。
3. **战斗内的固定值不乘 `attackMultiplier` / `defenseMultiplier`** ——
   那是战斗外商店、祝福的成长系数，战斗内临时加成再乘一次会失控。
4. **战斗结束 `EndBattleStats()` 整体清零**，不做「记账式负号抵消」，
   所以战斗异常中断也不会把加成残留在玩家身上。
   每次 `BeginBattleStats()` 都重新快照，天然自愈。

**接口分工**：
- 战斗中改属性：`AddBattleAttackFlat` / `AddBattleAttackPercent` / `AddBattleDefenseFlat` /
  `AddBattleDefensePercent` / `AddBattleAttackCountFlat` / `AddBattleDamageReductionFlat`。
  战斗外调用会被 `RequireBattle` 拦下并警告。
- 战斗外改属性：`AddAttack` / `AddDefense` / `AddAttackCount` / `AddDamageReduction`
  （内部带 `WarnIfInBattle` 警告，防止再被战斗中误用）。
- **存档一律读 `BaseAttack` / `BaseDefense` / `BaseAttackCount` / `BaseDamageReduction`**，
  不要读 `Attack` 等 —— 战斗中它们返回含 buff 的实时值。

**调用顺序（不能颠倒）**：
- `BattleManager.StartBattle`：先 `playerData.BeginBattleStats()`，**再**
  `BlessingManager.OnBattleStart`（异乡人在 OnBattleStart 里写减伤）。
- `BattleManager.BattleCoroutine`：物理伤害 `playerPhysical` / `enemyPhysical`
  已从「开头算一次」改为**在 `ComputeRoundDamage()` 里每轮重算**；
  循环内 `OnTurnStart` 之后要立刻再调一次 `ComputeRoundDamage()`，
  否则爱的祝福/朗基努斯当回合的加攻要等到下一回合才生效。
- `BattleManager.EndBattle`：`OnBattleEnd` → 恢复魔力 → `playerData.EndBattleStats()`。

**已改造的祝福**：`AgapeBlessingEffect`（加攻）、`LonginusEffect`（加攻/加段）、
`AllotrioiEffect`（减伤）全部走修正层。
`KabbalahTreeEffect` 的加段/减伤是**进楼层**触发的永久加成，继续走真实字段，不用改。

## 四个 Multiplier 的适用范围（2026-09-19 用户定稿）

`hpMultiplier` / `attackMultiplier` / `defenseMultiplier` / `goldMultiplier`
**只作用于「战前数值加成」——探索期的商店购买、属性道具、祝福获得**（成长系数）。
**战斗时的数值一律不走它们。**

配套约定：
- **扣血不算 Multiplier。** `SubtractHP` 只乘减伤，从不乘 `hpMultiplier`。
- **「不该致死的扣血」有下限：最低只扣到 1 点。** 统一用
  `PlayerData.SubtractRawHPKeepAlive()`。已用的地方：夹击（`PincerAttack.cs:249`）、
  爱的祝福每回合扣血。祝福的 HP 负成长在 `ApplyStatBonus` 里也有 `hp < 1 → 1`。
  **注意 `SubtractRawHP()`（能扣死的版本）已删除**，别再照抄旧代码。
- **回血分两个接口**：
  - `Heal(amount)` = 基础治疗量，会乘 `hpMultiplier`。只给**战前成长**用
    （如智慧的祝福 `OnAcquired/OnLevelUp` 的 +1000 HP、未来的药水）。
  - `HealRaw(amount)` = 加多少就是多少。给**战斗时按公式算出来的回复**用
    （吸血 `playerPhysical × LifeSteal%`、智慧的祝福每回合按受伤量回复）。
  - 曾经的坑：吸血走 `Heal()` → 被 `hpMultiplier` 放大，而扣血侧不缩小，两边不对称。
- **唯一还挂在战斗侧的 Multiplier 是 `AddGold`**（战斗奖励金币 × `goldMultiplier`）。
  它是 `goldMultiplier` 的**唯一**消费点，去掉会让「金币系数」变成死属性 ——
  所以按「奖励系数」对待，保留。2026-09-19 已向用户报备，待其确认。

## 战斗模块技术债（2026-09-19 整合，详见当天日志）

四组，按「先降风险再加功能」排序：
- **P1 清理** —— ✅ 已完成（2026-09-20 00:04）。`PlayerData.TryFight()` /
  `PlayerData.TakeDamage()` / `PlayerData.FightResult()` / `EnemyController.TakeDamage()`
  四个死符号已删，伤害公式从 3 套减到 2 套（`BattleManager` + 图鉴）。
- **P2 图鉴 vs 实战** —— 已大幅收敛（2026-09-20 晚）：图鉴现在**追踪玩家血量**、
  有阵亡即收场（含反伤致死、「阵亡后不再反伤」）、回合上限改用
  `BattleManager.MaxTurnCount`（不再是自己的 10000）、净回血返回负数。
  返回值从 `int` + `-1` 哨兵改为 `BattleSimResult{ CanWin, HpDelta }`
  —— 因为「负数 = 回血」和原来的「-1 = 无法战胜」会撞车。
  **唯一剩下的偏差：不跑祝福系统**（爱/深渊/静默/工匠/智慧/异乡人/灵知/真理）。
  玩家带着这些祝福时图鉴数字仍会偏。要彻底消除只能抽共享内核。
- **P3 流程边界**：僵持判负的措辞（实战「战斗失败」vs 图鉴「无法战胜」）。
- **P4 防御性** —— 部分完成：
  - ✅ `AddAttack` / `AddAttackCount` / `AddDefense` / `AddDamageReduction` 战斗中调用
    **已改成硬拦截**（`RequireOutOfBattle`，不再只是警告）。`AddGold` 刻意不拦。
  - ⬜ 修正层 3 个写接口（`AddBattleAttackPercent` / `AddBattleDefenseFlat` /
    `AddBattleDefensePercent`）零调用，倾向保留。
  - ⬜ `ApplyBlessing` 的两个分支直接写字段，绕过守卫，待设计。

**根治方向**：把「战斗结算」从 `BattleManager` 协程抽成无副作用的纯函数，实战与图鉴共用，
三套公式合一。未排期。具体形态（2026-09-20 晚定的设计，尚未动工）：

- 抽一个 `BattleResolver`：只算，不碰 UI、不 yield、不播动画。
  对外是 **`bool Step()` + `Report`（这一步发生了什么）+ `Outcome`（胜负/结束原因）**。
- **必须是「一步一停」而不是「先算完再回放」**：实战的祝福会在回合之间改状态
  （爱每回合加攻），先算完就意味着先把玩家的血改掉再播动画 —— 面板会开场就跳到最终值，演出全乱。
- 实战：`while (resolver.Step()) { PlayStep(resolver.Report); yield return WaitForSeconds(...); }`
  图鉴：`while (resolver.Step()) { }` 跑到底，不看演出。
- **祝福是第二步（也是图鉴算祝福的前提）**：`BlessingEffect.OnXxx(PlayerData, EnemyController, BattleUI)`
  的入参是 MonoBehaviour，图鉴拿不到可安全跑的真实实例（跑真实祝福会改玩家数据、
  污染 effect 的内部状态、`Defeat()` 还会触发掉落与楼层记忆）。
  所以第一步只能共享「流程」，图鉴仍不算祝福；要把祝福算进去，得先把
  `BlessingEffect` 的入参从 MonoBehaviour 解耦成「战斗单位状态」。
- **不要在图鉴里再抄一遍祝福规则** —— 那是把第二份复制品升级成第三份。

## 数值口径三则（2026-09-20 用户定稿）

### 1. 取整方向：**一律偏向玩家**

- **加成**（有符号增量，可能为正也可能为负）→ `Mathf.CeilToInt`（向 +∞）：
  正增量变大（+0.7 → +1），负增量变小（-0.7 → 0）。
- **以正数表示的「损失量」**（如爱的祝福每回合扣血）→ `Mathf.FloorToInt`（向 0）：
  玩家少扣（HP 50 的 3% = 1.5 → 扣 1 而不是 2）。
- **绝不要用整数除法 `/ 100`** —— 那是向下截断，对正加成等于少给，
  基础值小时会把加成整个抹掉（攻 7 的 +10% 会变成 0，玩家看不到任何变化）。
- 已按此对齐：`PlayerData.Attack/Defense` 的战斗内百分比、`ApplyPercentBonus`、
  `AgapeBlessingEffect` 扣血、`Bythos`/`Sige` 对敌真伤、`PincerAttack` 打玩家。
- **例外：夹击用的 `Mathf.FloorToInt(hp * 0.5f)` 不要改成 `CeilToInt`。**
  夹击的口径是「减少 50% 生命值，但**不应该打死人**」，floor 正好保证了这一点
  （HP 为 1 时伤害为 0、为 2 时扣 1 剩 1，永远扣不到 0）；换成 ceil 会把 HP 为 1 的单位直接夹死。
  玩家侧除了 floor 还会再走一道 `SubtractRawHPKeepAlive` 兜底。
  **「不致死」这个约束优先于「取整偏向玩家」。**
- **用户明确要求保持现状的**：各处 `* (100 - 减伤) / 100` 的整数除法**保持向下截断**，不要动。
- **系数换算已全部对齐**（2026-09-20）：`Heal` / `AddAttack` / `AddDefense` / `AddHP` / `AddGold`
  / `ApplyStatBonus` 里所有 `amount * xxxMultiplier / 100` 都是 `Mathf.CeilToInt(...)`。
- **带系数的加成接口都返回「实际增量」**（`int`）：
  `AddAttack` / `AddDefense` / `AddHP` / `AddGold`。
  要在 UI/日志里显示「加了多少」，**用返回值，不要自己再抄一遍公式**（抄了就会和真实值漂移）。

### 1.1 商店的「逆推」是干什么的（`NPCInteractionUI.OnBuyAttack/OnBuyDefense/OnBuyHP`）

```csharp
int rawGain  = currentPlayer.Attack * currentNPC.AtkPercent / 100;   // 标称要给的量
int inversed = rawGain * 100 / Mathf.Max(1, currentPlayer.AttackMultiplier);
currentPlayer.AddAttack(inversed);   // 内部还会 amount * attackMultiplier / 100
```

商店卖的是「**+X% 当前攻击力**」这个绝对值，但 `AddAttack` 的语义是「传进来的值会被系数放大」。
所以先把标称值除掉系数，让 `AddAttack` 内部那次乘法抵消回来 —— 这就是「逆推」。
作者的原意就是**抵消**（`NPCInteractionUI.cs:159` 的注释原文：
「逆推换算后通过 AddHP（内部会乘以 hpMultiplier / 100，正好抵消）」）。

**关键发现：三条属性获取路径的口径不一致，商店是唯一的异类。**

| 获取路径 | 代码 | 会被系数放大？ |
|---|---|---|
| 属性道具 `StatBoostPickup` | `ApplyStatBoost(value)` → `AddAttack(value)` → `× attackMultiplier/100` | ✅ 放大 |
| 祝福 `ApplyStatBonus` | `attack += b.attackBonus * attackMultiplier / 100` | ✅ 放大 |
| 商店 `NPCInteractionUI` | 逆推抵消 | ❌ **不放大** |

**逆推的实际代价**（两次整数截断，乘法反解）：

| 攻击力 | 攻击系数 | 买 +10% | rawGain | inversed | 实际到手 |
|---|---|---|---|---|---|
| 100 | 100% | | 10 | 10 | 10 ✓ |
| 100 | 150% | | 10 | 6 | **9** ✗ |
| 10 | 150% | | 1 | 0 | **0** ✗ 花钱白买 |

**用户 2026-09-20 拍板：去掉逆推，让系数正常放大购买所得。✅ 已落地。**

改法（三处 `OnBuyHP` / `OnBuyAttack` / `OnBuyDefense` 一致）：

```csharp
int rawGain    = Mathf.CeilToInt(currentPlayer.Attack * currentNPC.AtkPercent / 100f);
int actualGain = currentPlayer.AddAttack(rawGain);   // 内部 × attackMultiplier，让系数生效
Debug.Log($"[商店] 攻击力 +{...}%（=+{actualGain}，含攻击系数 {currentPlayer.AttackMultiplier}%）");
```

改动清单：
1. 删掉三处 `inversed` 逆推，直接 `AddXxx(rawGain)`。
2. `rawGain` 用 `Mathf.CeilToInt`（原来整数除法会吃到截断）。
3. `AddAttack` / `AddDefense` / `AddHP` / `AddGold` 改成**返回实际增量**（`int`），
   商店日志改打 `actualGain`（原来打标称 `rawGain`，改完会骗人）。
   返回 int 对老调用方是兼容的（语句形式调用忽略返回值即可）。
4. `PlayerData` 的「属性修改」段补上取整口径说明。

**数值影响（有意为之）**：商店购买开始被系数放大。

| 攻击力 | 攻击系数 | 买 +10% | 改前 | 改后 |
|---|---|---|---|---|
| 100 | 100% | | 10 | 10 |
| 100 | 150% | | 9 | **15** |
| 10 | 150% | | **0**（白买） | **1** |

副作用（已知并接受）：出现「先堆攻击系数、再回商店买属性」的最优解。



### 2. 术语：魔力值 vs 魔力输出（容易搞混）

- `manaCharge`（**魔力值 / 魔力充能**）= 玩家实际持有的魔力。会被战斗消耗，战斗结束恢复。
- `manaMax`（**魔力输出上限**）= 单回合魔力输出的最大值，是输出伤害的上限。
- **单回合实际魔力输出 = `min(manaCharge, manaMax)`**，而且**消耗的也是同一个值**。
  例：魔力值 900、魔力输出上限 500 → 第 1 回合打 500（余 400），第 2 回合打 400（余 0）。
- 显示口径（2026-09-20 已统一标签）：
  - `PlayerHUD` 显示「魔力输出 = `ManaMax`」—— 那是**上限**，正确。
  - `BattleUI` 玩家/敌人两块面板已从「魔力输出」**改成「本回合魔力输出」**，
    显示的是 `min(manaCharge, manaMax)`，即那一击的实际输出。
  - 面板是在魔力消耗之后才刷新的，所以除开局那次以外，玩家看到的是**下一击**的输出值。
    这是「回合制里面板显示下一回合状态」的正常口径，不需要改。
  - 图鉴 `MonsterManualEntryUI` 显示的是 `enemy.manaMax`（上限），用词是「魔力输出」。
  - 战斗**日志**里没有魔力输出相关文案（只有灵知的祝福提到「魔力充能」），改标签不影响日志。

### 3. 减伤允许超过 100%，**这是有意机制，不要加钳制**

`amount * (100 - 减伤) / 100` 在减伤 > 100 时系数为负 → `hp -= 负数` → **挨打回血**。
已向用户确认这是设计（吸伤/吸收类机制），**不要**顺手改成 `Mathf.Clamp(100 - X, 0, 100)`。
涉及 `PlayerData.SubtractHP`、`EnemyController.TakeRawDamage`、`MonsterManualEntryUI`。
减伤恰好 100% 时是 0 伤害（用于做「无敌」敌人，见战斗僵持上限 `MaxTurnCount`）。

## 属性写入口的两族接口（2026-09-20 定稿，已落地）

`PlayerData` 里所有改属性的入口分成两族，各自有越界守卫，**不要新增第三条直写路径**：

| 族 | 成员 | 写哪 | 守卫 |
|---|---|---|---|
| **战前接口** | `Add*`（12 个）、`ApplyStatBoost`、`ApplyBlessing` | 真实字段（= 战斗前数值） | `RequireOutOfBattle` — 战斗中直接拦下，不写一个字节 |
| **战斗时接口** | `AddBattle*`（6 个） | 战斗内修正层 | `RequireBattle` — 战斗外拦下 |

**两个刻意的例外（看到"没守卫"别急着补）**：
- `AddGold` —— 战斗奖励和恩惠的祝福都在**战斗结算时**加金币，必须有守卫反而会打断
  `goldMultiplier` 的奖励路径。
- `Set*`（10 个 setter）—— 是**读档恢复**路径，静默失败比污染更糟，所以不拦（只在读档时调）。

`Heal` / `HealRaw` / `SetHP` / `SubtractHP` / `SubtractRawHPKeepAlive` 改的是 `hp`，
HP 是活资源、**不做战斗快照**，战斗中本来就该能改，不在两族之内。

## Unity YAML 场景/预制体改动（踩过坑，别忘）
1. **定位 MonoBehaviour 必须用 script guid 匹配**，不能靠「猜组件 fileID」。
   Canvas 上常有多个 MonoBehavour（GraphicRaycaster / CanvasScaler / 自定义脚本），
   它们 fileID 连号，极易写错。正确做法：从 `.cs.meta` 读 guid，
   再在场景里找同时满足 `guid: X` + `m_GameObject: {fileID: Y}` 的块。
2. **追加字段前必须先读块内容**，确认该键是否已存在，否则产生 YAML 重复键。
3. 块切分约定：`text.find('\n--- ', i)` 得到的边界**不含**下一块的换行，
   所以可以安全地在块尾 `rstrip('\n')` 后追加新行。
4. 改完必须做**结构计数对比**（GameObject/MonoBehaviour/RectTransform/m_Name 各数量）
   和**引用完整性检查**（所有 `{fileID: N}` 的 N 都要在场景里存在），
   以及**重复键检查**。
5. 改前先备份到 `.workbuddy/backup/`，补丁脚本写成幂等的（检测到已有字段就跳过）。
6. **组件搬移（换宿主 GO）= 三步**：改块内 `m_GameObject` + 源 GO 删条目 + 目标 GO 末尾追加。
   Transform/RectTransform 必须仍在 m_Component 第一位。净零改动，字节数不变。
   额外校验：组件只能归属一个 GO；组件 `m_GameObject` 必须等于「含它的那个 GO」。
7. **用户可能在编辑器里打开并保存过**（Unity 会重排/补齐序列化数据，例如给数组补
   `- {fileID: 0}`）。动手前**必须重新读当前文件**，不能沿用上次的假设。

## 本机编译校验限制
- 无法用 `csc` 编译校验（安全策略拦截，等价 Add-Type）。
- 替代手段：括号配对计数 + 逐个 grep 核对引用的 API 签名是否存在。
