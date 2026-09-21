# 场景 ↔ 预制体 差异核对报告

生成时间：2026-09-21 16:41

> 比对方式：解析场景里的 `PrefabInstance.m_Modifications`，逐条回源预制体资源取值对比。
> 只有「场景值 ≠ 预制体资源值」的才算真正没同步的改动。



## 总览

| 场景 | 实例 | 源预制体 | 未同步差异 | 删除的组件 | 新增物体/组件 | 僵尸条目 |
|---|---|---|---|---|---|---|
| Game.unity | `&1659029357` | `Assets/Prefabs/UI/BattleCanvas.prefab` | **0** | 0 | 0 | 0 |
| Game.unity | `&1701914532898964265` | `Assets/Prefabs/UI/DialogueCanvas.prefab` | **0** | 0 | 0 | 0 |
| Game.unity | `&4743698551151404886` | `Assets/Prefabs/UI/ShopCanvas.prefab` | **0** | 0 | 0 | 0 |
| Game.unity | `&6780088997608248467` | `Assets/Prefabs/UI/DataCanvas.prefab` | **0** | 0 | 0 | 0 |
| Game.unity | `&8439140688188540330` | `Assets/Prefabs/UI/CheckCanvas.prefab` | **1** | 0 | 0 | 0 |
| Game.unity | `&8920489638242693106` | `Assets/Prefabs/UI/BlessingCanvas.prefab` | **0** | 0 | 0 | 0 |

---

## 场景 `Assets/Scenes/Game.unity`（6 个预制体实例）

### 实例 `&1659029357` ← `Assets/Prefabs/UI/BattleCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 21 |
| 与预制体一致（无需处理） | 21 |
| **场景独有差异（未同步）** | **0** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

### 实例 `&1701914532898964265` ← `Assets/Prefabs/UI/DialogueCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 25 |
| 与预制体一致（无需处理） | 25 |
| **场景独有差异（未同步）** | **0** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

### 实例 `&4743698551151404886` ← `Assets/Prefabs/UI/ShopCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 41 |
| 与预制体一致（无需处理） | 41 |
| **场景独有差异（未同步）** | **0** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

### 实例 `&6780088997608248467` ← `Assets/Prefabs/UI/DataCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 21 |
| 与预制体一致（无需处理） | 21 |
| **场景独有差异（未同步）** | **0** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

### 实例 `&8439140688188540330` ← `Assets/Prefabs/UI/CheckCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 29 |
| 与预制体一致（无需处理） | 28 |
| **场景独有差异（未同步）** | **1** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

#### 只在场景里改过 —— 共 1 项

| 对象 | 组件/脚本 | 属性 | 场景里的值 | 预制体里的值 |
|---|---|---|---|---|
| Scrollbar Vertical | RectTransform | `m_SizeDelta.y` | 1000 | 1003 |

### 实例 `&8920489638242693106` ← `Assets/Prefabs/UI/BlessingCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 33 |
| 与预制体一致（无需处理） | 33 |
| **场景独有差异（未同步）** | **0** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |


---

## 怎么把场景里的改动同步回预制体

### A. 整个实例一次性同步（最常用）
1. 在 Hierarchy 里选中场景中的预制体实例（例如 `BattleCanvas`）
2. Inspector 面板**最顶部那一行**（名字 + Revert/Apply 按钮那一行）右侧有个 **Overrides** 下拉
3. 展开后底部有两个按钮：
   - **Apply All** → 把该实例上所有差异推到预制体资源（场景和预制体都变成新值）
   - **Revert All** → 反过来，丢弃场景改动，实例恢复成预制体的样子

### B. 只同步某一项
- Overrides 下拉里每一项右侧都有单独的 **Apply** / **Revert**
- 或者在 Inspector 里**右键该字段** → `Apply to Prefab 'xxx'`
- 两种方式都只作用于这一项，不会动别的差异

### C. 物体/组件的增删
Overrides 下拉里会分组成 `Added Components` / `Removed Components` / `Added GameObjects` / `Removed GameObjects`，同样支持逐项 Apply / Revert。
**注意**：BattleCanvas 删掉的那 3 个 LayoutGroup 就在 `Removed Components` 组里。

### D. 反过来改（推荐的长期做法）
双击 Project 里的 `.prefab` 进入预制体编辑模式直接改。只要场景实例没有 override 该属性，所有实例会自动跟着变，也就不会再积累「只在场景里改过」的差异。

### Apply 之前的两条注意事项
1. **LayoutGroup 的删除和 RectTransform 坐标改动是配套的**。要么一起 Apply，要么一起 Revert。只 Apply 坐标却不 Apply 组件删除的话，LayoutGroup 会把手动坐标重新覆盖掉。
2. Apply All 会影响所有引用该预制体的地方。已确认本项目这 5 个 UI Canvas 预制体**只被 `Game.unity` 引用**，其它三个场景都没有引用，所以 Apply 是安全的。

---

## 重点解读

**1. `BattleCanvas` 的 42 项是最大一块，看起来是有意为之的整体改版**
- 布局重排：`Center` / `LeftSide` / `RightSide` / `EnemyImage` / `EnemyStatsText` / `PlayerStatsText` / `ContentArea ` 的锚点与坐标
- 删除了 3 个 LayoutGroup（`ContentArea ` 的 HorizontalLayoutGroup，`LeftSide` / `RightSide` 的 VerticalLayoutGroup）——与上面的坐标改动是配套动作
- `Round` 改名成 `Turn`
- `BattleWindow.turnText` 从空绑定到了文本组件
- 4 处 `m_RaycastTarget` 由 1 改成 0（关掉了挡点击的底板射线）

**2. 玩家头像攻击坐标的问题：场景里改对了，预制体没跟上**
`PlayerImage.m_AnchoredPosition.x`：场景 = `0`，预制体 = `175`。这正是之前分析 `PlayerUI_Attack` 曲线（x 走 0→125→0）时提到的坐标不匹配。场景已改成 0，预制体里还是 175 —— 必须同步的一项。

**3. TMP 字体差异（5 个 Canvas 都有，数量最多）**
所有含 TextMeshPro 文本的实例，场景里是 `SmileySans-Oblique SDF`，预制体里是 `culiufangxin24x SDF`。
已核对：TMP 的默认字体是 `LiberationSans SDF`，这两个都不是 —— 所以**不是 TMP 自动写入的噪音，而是一次真实的人为改动**，需要你决定统一成哪一个，然后 Apply 或 Revert。

**4. 僵尸条目可以无视**
`DialogueCanvas` 上有 4 条 `choice1Label / choice2Label / choice1Button / choice2Button` 修改记录，但脚本里这些字段已改名成 `exitButtonLabel / actionButtonLabel / exitButton / actionButton`。这些条目在预制体里找不到对应字段，不产生任何效果，Unity 下次保存场景时会自动清掉。

**5. `DataCanvas` 完全同步，0 项差异** —— 可以作为「干净」的参照。
