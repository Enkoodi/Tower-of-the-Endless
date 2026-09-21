# UI / 场景 / 门 约定

## UI 约定
- 菜单按钮统一挂 `OpeningButton`：外框是 Awake 里代码画的 4 条无 sprite Image 细条
  （thickness=6、raycastTarget=false），不依赖图片；`SetLocked(bool)` = 常驻选中框（互斥选择）。
- 按钮 Image 一律 alpha=0 当热区，可见画面来自父级 Panel 的 sprite。
  > 后果：**Button 的 Color Tint 在菜单按钮上完全无效** —— 它只作用于 `m_TargetGraphic`，
  > 而那是 alpha=0 的热区，`m_DisabledColor` 乘完仍是 alpha 0。
- **「未解锁」的视觉反馈由 `OpeningButton` 负责**：按 `Button.interactable` 把子物体 TMP 标签换成
  `lockedLabelColor`（默认 0.45 灰），恢复可交互时还原原色。TMP 的顶点渐变与 `color` 是**相乘**关系，
  所以设 `color` 就够、渐变不用关。刷新时机 = `Awake` 一次 + `Update` 轮询对比
  （控制器与按钮的 Awake 顺序 Unity 不保证）。**锁定状态只认 `interactable`，别再往控制器里加视觉代码。**
- 绑定一律 `[SerializeField]` 拖引用 + Awake 里 `AddListener`，**不要** `switch(button.name)`（失败是静默的）。
  参考 `UI/SettingMenu.cs`。
- 控制器挂在「最小能包住它管辖的全部 UI 的节点」上，**不要挂 Canvas**（会扫到无关 UI）。
- 场景跳转统一 `ScreenFader.FadeToScene(name[, duration])`；ESC 设置走 `SettingsToggle` 单例。
- **设置界面进出刻意不做转场**（2026-09-21 用户要求）：走 `ScreenFader.LoadSceneInstant(name)`
  —— 同步 `LoadScene` + 先把遮罩 alpha 归零（防止上一次淡入淡出残留黑屏），仍保留
  Build Settings 场景存在性检查。共 4 个出口：`SettingsToggle.EnterSettings` /
  `ExitSettings`、`SettingMenu.BackToTitle` / `StartNewGame`。
  其它场景跳转（Opening↔Game、Credits、Ending）**保持淡入淡出不变**。

## 战斗速度档位
4 档：正常 1f / 两倍 0.5f / 四倍 0.25f / **跳过 0.01f**。0.01 命中 `BattleManager.IsSkipMode`
（`logDelay <= skipDelayThreshold`）→ 只结算，不打开战斗界面。档位值存 `OpeningButton.speedDelay`
（Inspector），**不靠按钮名字反查**；持久化在全局存档 `battleSpeed`，进场景按「最近档位」匹配。

## 战斗窗口的开关与输入锁定时序（2026-09-21 修）

输入锁 = `PlayerMove.isInBattle`（订阅 `BattleManager.OnBattleOpen/OnBattleClose`），
与 `BattleManager.IsFighting`（`SaveManager` 的 P/O、`SettingsToggle` 的 ESC 用它守卫）同源。

**铁律：解锁必须发生在界面真正消失之后，不是结算结束的那一刻。**
`EndBattle()` 只负责结算（祝福回调、魔力恢复、`EndBattleStats`、发金币、`Enemy.Defeat`、阵亡演出），
**不动 `isFighting`、不发 `OnBattleClose`**；这些连同 `onBattleEnd` 回调一起放进
`CloseAfterDelay(won)`：`等待(非跳过档 1s，阅读结算用，不跟倍速加速) → battleUI.CloseBattle()
→ isFighting=false → OnBattleClose → onBattleEnd`。

改之前是 `EndBattle` 里立刻解锁，而窗口还要 1 秒才关 →
「禁止输入的时长比界面存续时间短 1 秒」。副作用更糟：这 1 秒里能走动，
若走到旁边的敌人身上就能再开一场战斗，而上一场残留的 `CloseAfterDelay` 醒来会把**新**窗口关掉。

跳过档（`silentBattle`）整段协程同帧跑完，改动只动了同帧内的相对次序：
`CloseBattle → OnBattleClose → onBattleEnd` 与改前完全一致，跳过档行为不变。

## 门 / 开门动画（2026-09-21）
- 两类门：`DoorController`（钥匙门 / HP 门 / 数量门，钥匙/HP 判定在 `TryOpen`）+
  `BattleDoorController`（击败指定坐标敌人后自动开）。
- **开门动画由 `DoorOpenAnimation`（静态工具，不挂节点）驱动**，两个控制器共用：
  `Prepare(animator)` 在 Awake 里把 Animator 关掉（否则门一生成就自己播动画），
  `PlayOpen(host, animator, onFinished)` 在开门时启用 + `Play(0,0,0f)` 播一次，播完回调。
  没有 Animator 时 onFinished 立刻执行 = 原来的「开门即消失」，所以移涌/心灵门行为不变。
- **收尾顺序**：门开的瞬间**碰撞体保留**，等动画播完（或没有动画）才在 `RemoveDoor()` 里
  一起撤掉碰撞体 + 画面。理由：碰撞体先撤玩家会从没开完的门里穿过去。
  安全性已确认：`MapGenerator` 换楼层是 **`Destroy` 掉 `mapContainer` 的子物体**（不是 SetActive），
  动画期间切楼层只会连门一起销毁、不会留下一个「已开但还挡路」的门。
- 动画资产：`Assets/Animations/{Yellow,Blue,Red,Battle}Door.anim|.controller`，
  素材来自 `Sprites/魔塔/LCG/{2..5}.png`（**32×288 竖向 9 格**）。4 段动画都是
  **8 关键帧 / 0.6s**；controller 无参数、单状态。
  已验证「门静止图 ≈ 动画首帧」（差异 1~8%），开播不跳图。
- ⚠️ **`m_LoopTime` 必须是 0（Loop Time 关）**。这 4 段是「播一次就完」的开门动画；
  之前是 1（循环），播到末尾会绕回首帧 = 关门图，在收画面之前闪一下。
  同时代码也以 `GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f` 判定收尾
  （而不是死等 `length` 秒），避免 deltaTime 过冲时多吃一帧绕回后的首帧。
  `3FBattleDoor` / `29FBattleDoor` 用的是 `GROUND/6.png` 那一套，**故意不做动画**，用户已确认不用管。
- `BattleDoorController.Open(bool animate)`：敌人被击败 = `true` 播动画；
  读档 / 重返楼层的恢复流程 = `false` 直接撤掉。
- 重返楼层时 `MapGenerator.SpawnObject` 本来就会跳过「记忆里已开」的门，所以不会残留动画。

## DataCanvas HUD 的绑定约定（2026-09-21 改名后）

`PlayerHUD`（挂 DataCanvas）**不再按下标绑定，改为按节点名查找**（`panel.Find("HPText")`）。
节点名与 `PlayerData` 字段一一对应，面板里再增删行不会让其它行错位。
> 改名前是 `GetComponentsInChildren<TextMeshProUGUI>()` 取下标绑定，
> 往 RightPanel 插一个 `Floor` 就在**不报错**的情况下把 7 项道具数量整体挤歪一位。

| 面板 | 节点名（= 语义） |
| --- | --- |
| LeftPanel 头部 | `PlayerImage`（玩家头像，`MONSTER/32.png`，与 BattleCanvas.PlayerImage 同源） |
| LeftPanel 11 行 | `HPText` 生命 / `AttackText` 攻击 / `DefenseText` 防御 / `AttackCountText` 攻击段数 / `LifeStealText` 吸血 / `ReflectDamageText` 反伤 / `DamageReductionText` 减伤 / `ManaChargeText` 魔力充能 / `ManaMaxText` 魔力输出 / `SpeedText` 速度 / `GoldText` 金币 |
| RightPanel 标题 | `FloorText`（读 `MapGenerator.CurrentMap.name`，口径同 `MonsterManualUI`） |
| RightPanel 7 行 | `YellowKeyText` / `BlueKeyText` / `RedKeyText` / `AeonKeyText` / `UpTeleporterText` / `DownTeleporterText` / `EnemyHalveItemText`（圣水） |
| RightPanel 图标 | 同名 + `Icon` 后缀，与上一行逐行对齐（y=0 起每档 -100） |

- 命名沿用其他 UI 预制体的习惯：**PascalCase 英文 + `Text`/`Icon` 后缀**
  （对照 `BattleCanvas` 的 `PlayerStatsText`/`EnemyNameText`、`DialogueCanvas` 的 `NameText`）。
- 图标↔道具的对应靠 sprite 反推（`ITEM/0`=黄钥匙、`2`=蓝钥匙、`1`=红钥匙、`15`=上楼、`16`=下楼、`11`=圣水）。
- 改节点名前**必须**确认场景里没有 `propertyPath: m_Name` 的 override 指向这些子节点
  （否则场景会把名字盖回去）。Game.unity 里只有 6 个 Canvas 根节点有 m_Name override，子节点没有。
- 复用脚本（只读）：`.workbuddy/tools/datacanvas_hud_report.py`（层级+图标快照）、
  `datacanvas_binding_check.py`（**代码里的节点名 ↔ 预制体真实节点名 交叉校验 + 孤儿节点检查**）、
  `datacanvas_rename_nodes.py`（按 fileID 改名，带备份/回读校验）。
