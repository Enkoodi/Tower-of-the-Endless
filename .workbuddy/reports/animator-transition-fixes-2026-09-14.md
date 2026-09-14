# 玩家动画转移修正记录 — 2026-09-14 22:40

改动对象：`Assets/Animations/Player.controller`、`Assets/Animations/PlayerUI.controller`
改动前提：Unity 编辑器已关闭（`Temp/UnityLockfile` 不存在），直接编辑 YAML。

---

## 一、Exit Time 是什么

| 字段 | 含义 | 注意 |
|---|---|---|
| `m_ExitTime`（0~1 归一化） | 「源状态播到百分之几，才允许离开」 | `m_HasExitTime: 0` 时**完全无效**（Inspector 里变灰） |
| `m_HasExitTime` | 是否启用上面那个门槛 | 与 Conditions 是 **AND** 关系 |
| `m_TransitionDuration` | **混合**时长，不是等待时长 | > 0 时源/目标状态交叉淡化 |
| `m_TransitionOffset` | 目标片段从百分之几开始播 | 归一化，0 最干净 |

**关键陷阱**：`duration > 0` 的那段混合是**从 ExitTime 那一刻开始算**的。
所以 `ExitTime 0.516129` + `duration 0.2478` 不是「播到 0.2667s 才开始淡」，而是「**0.2667s 就开始淡**，0.5145s 淡完」。

**两种标准形态**

- 进入（要立刻响应）：`HasExitTime = 0`，`duration = 0`，`offset = 0` → 条件满足的下一帧硬切
- 收尾（播完再回）：`HasExitTime = 1`，`ExitTime = 1`，`duration = 0` → 片段播到结尾硬切回

---

## 二、两个 hurt 白闪不同步的根因

跟「30 帧 / 60 帧」无关。真正原因是一条渐变、一条硬切：

| | 白闪帧出现 | 开始淡出/切走 | 观感 |
|---|---|---|---|
| **改前** 本体 `Player_Hurt → PlayerIdle` | 0.25s | **0.2667s**（duration 0.2478 渐变） | 白闪只亮约 17ms 就被淡掉，偏暗偏短 |
| **改前** 头像 `PlayerUI_Hurt → PlayerIdle` | 0.25s | 0.5144s（duration 0 硬切） | 白闪满亮保留到结束 |

一个「慢慢淡」，一个「满亮突然消失」→ 就是肉眼看到的细微偏差。

---

## 三、逐条改动

### `Player.controller`（场景本体 Player）

| 转移 | 字段 | 改前 | 改后 |
|---|---|---|---|
| `Player_Hurt → PlayerIdle` | `m_TransitionDuration` | `0.24778962` | **`0`** |
| | `m_ExitTime` | `0.516129` | **`1`** |
| `PlayerIdle → Player_Hurt` | — | duration 0 / exit 0 / HasExitTime 0 | **未动（本来就是对的）** |

### `PlayerUI.controller`（BattleCanvas / PlayerImage 头像）

| 转移 | 字段 | 改前 | 改后 |
|---|---|---|---|
| `PlayerIdle → PlayerUI_Hurt` | `m_TransitionOffset` | `0.0044816514` | **`0`** |
| `PlayerIdle → PlayerUI_Attack` | `m_TransitionOffset` | `0.0055123605` | **`0`** |
| `PlayerUI_Hurt → PlayerIdle` | `m_TransitionOffset` | `0.0022405977` | **`0`** |
| | `m_ExitTime` | `0.9956811` | **`1`** |
| `PlayerUI_Attack → PlayerIdle` | `m_TransitionOffset` | `0.011692371` | **`0`** |
| `PlayerUI_Attack → PlayerUI_Hurt` | 整条 | 不存在 | **新增** |

新增转移的参数：

```
fileID            -5426103890126771843
条件              isHurt（Trigger）
m_TransitionDuration  0
m_TeleportTransitionOffset  0
m_ExitTime        0.75
m_HasExitTime     0
目标状态          PlayerUI_Hurt
```

`PlayerUI_Attack` 的出边顺序：`[-5426103890126771843, 2399931390518586052]`
（受伤优先级高于自然收尾）

---

## 四、改后的完整状态机

### Player.controller
```
[PlayerIdle] ──isHurt, dur 0, 无 ExitTime──▶ [Player_Hurt]
[Player_Hurt] ──无条件, dur 0, ExitTime = 1, HasExitTime──▶ [PlayerIdle]
```

### PlayerUI.controller
```
[PlayerIdle] ──isHurt,   dur 0, 无 ExitTime──▶ [PlayerUI_Hurt]
[PlayerIdle] ──isAttack, dur 0, 无 ExitTime──▶ [PlayerUI_Attack]
[PlayerUI_Attack] ──isHurt,   dur 0, 无 ExitTime──▶ [PlayerUI_Hurt]
[PlayerUI_Attack] ──无条件, dur 0, ExitTime = 1──▶ [PlayerIdle]
[PlayerUI_Hurt]   ──无条件, dur 0, ExitTime = 1──▶ [PlayerIdle]
```

两个 hurt 现在时间线完全一致：

```
Trigger ──下一帧──▶ 0.00s 正常图 ──▶ 0.25s 白闪 ──▶ 0.50s 正常图 ──▶ 0.5167s 退出
本体 与 头像 完全同步
```

---

## 五、明确没改的地方

**三个片段的 `m_StopTime` 是 `0.51666665`（31 帧），不是 `0.5`（30 帧）**
（`PlayerIdle` / `PlayerHurt` / `PlayerUI_Hurt`；`PlayerUI_Attack` 是 1.0，本来就一致）

- 影响：待机循环末尾多 1 帧 `33_white`（A 15 帧 → B 15 帧 → A 1 帧），受伤末尾多 1 帧正常图。
- 因为两个 hurt 片段时长完全一样，**这跟白闪同步无关**，肉眼也基本看不出，所以没动。
- 想改成正好 30 帧：把这三个片段的 `m_StopTime` 改成 `0.5`（或在 Animation 窗口把结束帧拖到 30）。

---

## 六、附带发现（精灵层面）

`Assets/Sprites/魔塔/MONSTER/` 三个精灵逐像素解码结果：

| 文件 | 尺寸 | 不透明像素 | 近白像素 | 说明 |
|---|---|---|---|---|
| `32.png` | 32×32 | 1024 | 140 (13%) | 正常骑士 |
| `33_white.png` | 32×32 | 1024 | 140 (13%) | **轮廓与 32.png 完全相同**，只有 230 个内部像素有低对比阴影差异 |
| `32_twinkle.png` | 32×32 | 730 | 730 (**100%**) | 完整纯白骑士剪影 |

结论：

1. **白闪设计是对的** —— `32_twinkle.png` 是干净的全白剪影，和正常图对比强烈。
2. **`PlayerIdle.anim` 那个「2 帧待机」实际上看不出动画** —— 它交替的两张（`33_white` / `32.png`）轮廓像素级相同，
   内部只有细微阴影差异，肉眼几乎分不出。如果待机想要「呼吸 / 眨眼」效果，需要重新做两张有明显差异的图。
3. guid 对照：
   - `0f02ad79f9753894291f2738360b2b83` = `33_white.png`
   - `3fab2fe23ffaba34b9805172a69d2bc3` = `32.png`
   - `43ad5c966ae611b429fa75b6abf83b2c` = `32_twinkle.png`

---

## 七、顺带更正（之前报告里的一处误判）

早前那份 `prefab-override-report` 里写过「`PlayerImage.m_AnchoredPosition.x`：场景 0 / 预制体 175，场景改对了没同步」——**这条是错的**。

场景里被改成 0 的三个 target 是 `EnemyStatsText`、`EnemyImage`、`BattleCanvas` 的 RectTransform；
`PlayerImage` 的 RectTransform（`&9199026810018236996`）在 `Game.unity` 里**没有任何 override**，
沿用预制体的 `(175, -50)`。

而 `PlayerUI_Attack.anim` 的 x 曲线静止值正好是 **175** —— **两边一致，攻击动画没有坐标错位问题。**
