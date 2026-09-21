# Tower of the Endless — 项目长期备忘

> 只留「还在生效的约定」。过程叙述在 `.workbuddy/memory/2026-09-*.md` 里。

## 工程环境
- Unity 项目 `D:\zzzMyWork\Game\unity\Tower of the Endless`。5 个场景全 enabled：
  Opening / Game / Credits / Ending / Setting。
- 改 `.unity` / `.prefab` 前先查 `Unity.exe` 进程（本机常只有 Unity Hub 在跑，编辑器没开就能安全改 YAML）。
- **编译校验限制**：`csc` 被安全策略拦（等价 Add-Type），离线只能做「括号配对 + grep 核 API 签名」。
  CS1612 这类语言错误只有编译器能查 → **改完必须让用户回 Unity 编译一次**。

## 存档（SaveManager）
主动档 `game_save.json` 与自动档 `auto_save.json` **共用** `SaveGameTo` / `LoadGameFrom`
→ 新字段只写这两个方法，两者天然一致，**不要为自动存档另开分支**。
`specialBlessings` = 当前生效的特殊祝福层数（Conditional 型）；
`obtainedBlessings` = 本局获得清单（含 DirectBonus 型，value = 获得次数），**两份别合并**。
获得祝福的登记点 = `PlayerData.ApplyBlessing`（守卫之后，玩家获得祝福的唯一出口）。
进程内 static / DontDestroyOnLoad 的本局状态（`NPCController.purchaseCounts` 商店购买次数、
`BlessingManager.activeEffects`）**必须在 `SettingMenu.StartNewGame` 里重置**，否则读档→新游戏会串局。
读档对新字段缺失的旧档要兜底（如 obtainedBlessings 为空 → 用 specialBlessings 层数当种子）。

## 特殊祝福（Conditional 型）的等级
11 个 Effect 全部有 Level 缩放（Create 映射之外的都是 DirectBonus，没有等级概念）。
重复获得 = 升级：走 `BlessingManager.AddEffect` 里 `existing.AddLevel()` + `OnLevelUp`。
`OnAcquired` **只在首次获得**时调用（`PlayerData.ApplyConditional` 用 `HasEffect` 判定首次）——
升级时传进来的 effect 是新 new 的实例，无条件调用它会让『智慧』每级多 +1000。
"每场战斗重置次数"的状态（KabbalahTree/Demiurge 的 `remainingTriggers`）必须带未初始化回退
（`-1` + 回退到 `Level`），否则战斗外获得后直接上 30 层 / 读档后立即上 30 层会按 0 次算。
asset 的 `description` 是写死的 Lv.1 文案；卡片升级提示由 `BlessingPanel.BuildOwnedHint` 动态追加。

## UI 约定
- 菜单按钮统一挂 `OpeningButton`：外框是 Awake 里代码画的 4 条无 sprite Image 细条
  （thickness=6、raycastTarget=false），不依赖图片；`SetLocked(bool)` = 常驻选中框（互斥选择）。
- 按钮 Image 一律 alpha=0 当热区，可见画面来自父级 Panel 的 sprite。
- 绑定一律 `[SerializeField]` 拖引用 + Awake 里 `AddListener`，**不要** `switch(button.name)`（失败是静默的）。
  参考 `UI/SettingMenu.cs`。
- 控制器挂在「最小能包住它管辖的全部 UI 的节点」上，**不要挂 Canvas**（会扫到无关 UI）。
- 场景跳转统一 `ScreenFader.FadeToScene(name[, duration])`；ESC 设置走 `SettingsToggle` 单例。

## 战斗速度档位
4 档：正常 1f / 两倍 0.5f / 四倍 0.25f / **跳过 0.01f**。0.01 命中 `BattleManager.IsSkipMode`
（`logDelay <= skipDelayThreshold`）→ 只结算，不打开战斗界面。档位值存 `OpeningButton.speedDelay`
（Inspector），**不靠按钮名字反查**；持久化在全局存档 `battleSpeed`，进场景按「最近档位」匹配。

## 战斗结算内核 `Battle/BattleResolver.cs`（实战与图鉴共用，结算逻辑只有这一份）
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

## 图鉴（怪物手册）
`MonsterManualEntryUI.SimulateBattle` 跑同一个内核 → 公式/取整/吸血/反伤/死亡判定/回合上限
**永远不会与实战漂**。返回 `BattleSimResult { bool CanWin; int HpDelta; }`（`HpDelta>0` 损失，
`<0` 绿字负数 = 反而回血；取代了旧 `-1` 哨兵）。**唯一残留偏差 = 不跑祝福**；
要消掉得先把 `BlessingEffect` 的入参从 MonoBehaviour 解耦 —— **不要在图鉴里抄第二遍祝福规则**。

## 战斗模块技术债
- ✅ P1 死符号清理。✅ P2 结算合并（剩「图鉴不跑祝福」）。✅ P4-1 战前族硬拦截。
- ⬜ P3 僵持判负措辞（实战「战斗失败」vs 图鉴「无法战胜」）。
- ⬜ `ApplyBlessing` 的两个分支仍直写字段（目前只会在战斗外触发）。

## Unity YAML 场景/预制体改动（踩过的坑）
1. 定位 MonoBehaviour **必须用 script guid 匹配**（从 `.cs.meta` 读 guid + 匹配 `m_GameObject`），
   不能猜组件 fileID（Canvas 上多个同类组件 fileID 连号）。
2. 追加字段前先读块内容，避免 YAML 重复键。
3. 改完必做：结构计数对比（GameObject/MonoBehaviour/RectTransform/m_Name）、
   引用完整性（所有 `{fileID: N}` 都要存在）、重复键检查。
4. 改前备份到 `.workbuddy/backup/`，补丁脚本写成幂等的。
5. 组件换宿主 GO = 改 `m_GameObject` + 源 GO 删条目 + 目标 GO 末尾追加（净零改动）。
6. **用户可能在编辑器里打开并保存过** → 动手前重新读当前文件，别沿用上次假设。

## 字体 / 文本（缺字方块问题的定论，2026-09-20）
`Assets/Font/smiley-sans-v2.0.1/` 下 4 个 TMP 字体资产，**根因是动态图集容量，不是设置写错**。
- `pointSize=90 / padding=9` → 每字形约 92×92px，单张 2048² **上限约 450 字形**。
  项目实需 **865 字符（680 汉字）**。已烘焙 SmileySans 450 / culiufangxin 390（占 89.8% / 93.1%）→ 早已到顶。
- 四个资产都 `m_IsMultiAtlasTexturesEnabled: 0` → 满了不再开新页。这是"没动设置突然缺字"的机制：
  每渲染一个没见过的字就刻一个，刻满后所有新字全变 □（U+25A1）。
- 实际在用：**SmileySans（45 组件）+ culiufangxin24x（16 组件）**。
  `xiangcuilingduhei` / `零ゴシック` 零引用；**零ゴシック 是日文字形集，缺 348 个常用简体字，永远别用**。
- 次要隐患：TMP Settings 默认字体 = LiberationSans（**无中文**），两处 fallback 表都是空的；
  `renderMode: 4165 = SMOOTH_HINTED`，**不是 SDF**（Unity 自带 LiberationSans 是 4169=SDFAA_HINTED）。
- **警告：动态字形会写回 `.asset`（8.6MB，git 里常年是 `M`）。git 回滚 / Unity 强退 = 已刻字形消失，
  且图集已满补不回来。绝对不要把字体 `.asset` 当普通文件回滚。**
- **已落地（2026-09-20 23:30）**：SmileySans 与 culiufangxin24x 两个字体资产已开
  `m_IsMultiAtlasTexturesEnabled: 1`；TMP Settings 的默认字体已从 LiberationSans（无中文）
  改为 **SmileySans-Oblique SDF**（顺带等于给所有文本加了一层隐式兜底，
  见 `TMPro_Private.cs:1176` 缺字链会查 defaultFontAsset）。
  xiangcuilingduhei / 零ゴシック 未动（零引用，别用）。
  备份：`.workbuddy/backup/font_atlas_20260920_2330/`（3 个文件，改前原始状态）。
  副作用：编辑器里补字会往 .asset 追加图集页，文件会长到 ~17MB/个、显存 +4MB/页，
  这是预期内；想收敛回去就走下面的方案 B。
- 修复：A 勾 Multi Atlas Textures 止血（多页由 `TMP_MaterialManager.GetFallbackMaterial` 处理，开箱可用）；
  B（推荐）**预烘焙全套字符** → SDFAA + pointSize 48/padding 5（单张容约 1225 字形，一张够）。
  **B 之后 populationMode 保持 Dynamic，不要切 Static**：预烘焙把常见字固化（运行期不再动态加字、无脏写），
  Dynamic 只当保险丝——以后冒出新字会自动补、不方块；切 Static 则每次新字都要重跑。
  代价仅是构建里多保留源字体（+约 7MB）。
- Font Asset Creator 的 **`Save` 会覆盖选中的同一个 asset**（GUID 不变，61 个引用不动），
  别点 `Save as...`（那会生成新文件、引用全断）。见 `TMPro_FontAssetCreatorWindow.cs:1099`。
- 重跑判定不需要肉眼：跑 `.workbuddy/tools/font_charset.py`，它和 `字符集_清单.json` 比对后会直接
  输出「一致 → 不用重做」或「有变化 → 列出新增的字」。字符集：`.workbuddy/font/字符集_单行.txt`（865 字）。
- 排查工具（可复用）：`.workbuddy/tools/font_report.py` 是**体检入口**（图集占用率/渲染模式/TMP Settings
  配置一次看完）；`font_charset.py` 导出字符集 + 清单比对；`font_coverage.py` 用字 vs 烘焙 vs 缺口；
  `font_risk.py` 容量核算；`font_audit.py` 是共用的解析库（import 无副作用）。
  统一用托管 Python 跑：`C:/Users/admin/.workbuddy/binaries/python/versions/3.13.12/python.exe`。
  缺字现场直接看 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 里的 `was not found in the [...] font asset`。

## 诊断脚本工具集
`.workbuddy/tools/` 下放可复用的只读分析脚本（字体、结算对拍等），不侵入 `Assets/`。
