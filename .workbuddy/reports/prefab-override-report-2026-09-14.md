# 场景 ↔ 预制体 差异核对报告

生成时间：2026-09-14 21:26

> 比对方式：解析场景里的 `PrefabInstance.m_Modifications`，逐条回源预制体资源取值对比。
> 只有「场景值 ≠ 预制体资源值」的才算真正没同步的改动。



## 总览

| 场景 | 实例 | 源预制体 | 未同步差异 | 删除的组件 | 新增物体/组件 | 僵尸条目 |
|---|---|---|---|---|---|---|
| Game.unity | `&1701914532898964265` | `Assets/Prefabs/UI/DialogueCanvas.prefab` | **11** | 0 | 0 | 4 |
| Game.unity | `&2052974500118925326` | `Assets/Prefabs/UI/BattleCanvas.prefab` | **42** | 3 | 0 | 0 |
| Game.unity | `&4743698551151404886` | `Assets/Prefabs/UI/ShopCanvas.prefab` | **15** | 0 | 0 | 0 |
| Game.unity | `&6780088997608248467` | `Assets/Prefabs/UI/DataCanvas.prefab` | **0** | 0 | 0 | 0 |
| Game.unity | `&8439140688188540330` | `Assets/Prefabs/UI/CheckCanvas.prefab` | **13** | 0 | 0 | 0 |
| Game.unity | `&8920489638242693106` | `Assets/Prefabs/UI/BlessingCanvas.prefab` | **7** | 0 | 0 | 0 |

---

## 场景 `Assets/Scenes/Game.unity`（6 个预制体实例）

### 实例 `&1701914532898964265` ← `Assets/Prefabs/UI/DialogueCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 40 |
| 与预制体一致（无需处理） | 25 |
| **场景独有差异（未同步）** | **11** |
| 僵尸条目（字段已不存在，无效） | 4 |
| 场景删除的组件 | 0 |

#### 只在场景里改过 —— 共 11 项

| 对象 | 组件/脚本 | 属性 | 场景里的值 | 预制体里的值 |
|---|---|---|---|---|
| Text (TMP) | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Text (TMP) | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| Text (TMP) | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Text (TMP) | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| NameText | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| NameText | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| DialogueCanvas | Canvas | `m_SortingOrder` | 50 | 100 |
| DialogueCanvas | DialogueUI.cs | `charsPerSecond` | 50 | 30 |
| DialoguePanel | Image.cs | `m_RaycastTarget` | 1 | 0 |
| DialogueText | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| DialogueText | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |

#### 僵尸修改条目（预制体里已找不到对应字段，可忽略）

- DialogueCanvas · DialogueUI.cs → `choice1Label`
- DialogueCanvas · DialogueUI.cs → `choice2Label`
- DialogueCanvas · DialogueUI.cs → `choice1Button`
- DialogueCanvas · DialogueUI.cs → `choice2Button`

### 实例 `&2052974500118925326` ← `Assets/Prefabs/UI/BattleCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 81 |
| 与预制体一致（无需处理） | 39 |
| **场景独有差异（未同步）** | **42** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 3 |

#### 只在场景里改过 —— 共 42 项

| 对象 | 组件/脚本 | 属性 | 场景里的值 | 预制体里的值 |
|---|---|---|---|---|
| EnemyImage | Image.cs | `m_RaycastTarget` | 0 | 1 |
| EnemyStatsText | RectTransform | `m_AnchorMax.y` | 1 | 0 |
| EnemyStatsText | RectTransform | `m_AnchorMin.y` | 1 | 0 |
| EnemyStatsText | RectTransform | `m_AnchoredPosition.x` | 175 | 0 |
| EnemyStatsText | RectTransform | `m_AnchoredPosition.y` | -350 | 0 |
| BattlePanel | Image.cs | `m_RaycastTarget` | 0 | 1 |
| Center | RectTransform | `m_AnchorMax.x` | 0.5 | 0 |
| Center | RectTransform | `m_AnchorMax.y` | 0.5 | 1 |
| Center | RectTransform | `m_AnchorMin.x` | 0.5 | 0 |
| Center | RectTransform | `m_AnchorMin.y` | 0.5 | 1 |
| Center | RectTransform | `m_AnchoredPosition.x` | 0 | 1050 |
| Center | RectTransform | `m_AnchoredPosition.y` | 0 | -300 |
| PlayerImage | Image.cs | `m_RaycastTarget` | 0 | 1 |
| RightSide | RectTransform | `m_AnchorMax.x` | 1 | 0 |
| RightSide | RectTransform | `m_AnchorMax.y` | 0.5 | 1 |
| RightSide | RectTransform | `m_AnchorMin.x` | 1 | 0 |
| RightSide | RectTransform | `m_AnchorMin.y` | 0.5 | 1 |
| RightSide | RectTransform | `m_AnchoredPosition.x` | -275 | 1875 |
| RightSide | RectTransform | `m_AnchoredPosition.y` | 0 | -325 |
| EnemyImage | RectTransform | `m_AnchorMax.x` | 0.5 | 0 |
| EnemyImage | RectTransform | `m_AnchorMax.y` | 1 | 0 |
| EnemyImage | RectTransform | `m_AnchorMin.x` | 0.5 | 0 |
| EnemyImage | RectTransform | `m_AnchorMin.y` | 1 | 0 |
| EnemyImage | RectTransform | `m_AnchoredPosition.y` | -50 | 0 |
| LeftSide | RectTransform | `m_AnchorMax.y` | 0.5 | 1 |
| LeftSide | RectTransform | `m_AnchorMin.y` | 0.5 | 1 |
| LeftSide | RectTransform | `m_AnchoredPosition.x` | 275 | 225 |
| LeftSide | RectTransform | `m_AnchoredPosition.y` | 0 | -325 |
| Round | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Round | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| Round | GameObject | `m_Name` | Turn | Round |
| BattleWindow | Image.cs | `m_RaycastTarget` | 0 | 1 |
| PlayerStatsText | RectTransform | `m_AnchorMax.y` | 0 | 1 |
| PlayerStatsText | RectTransform | `m_AnchorMin.y` | 0 | 1 |
| PlayerStatsText | RectTransform | `m_AnchoredPosition.x` | 180 | 175 |
| PlayerStatsText | RectTransform | `m_AnchoredPosition.y` | 200 | -350 |
| 'ContentArea ' | RectTransform | `m_AnchoredPosition.x` | 4 | 0 |
| 'ContentArea ' | RectTransform | `m_AnchoredPosition.y` | -72 | -50 |
| BattleWindow | BattleUI.cs | `turnText` | 预制体内「Round」(TextMeshProUGUI.cs) | None |
| PlayerImage | RectTransform | `m_AnchorMax.x` | 0.5 | 0 |
| PlayerImage | RectTransform | `m_AnchorMin.x` | 0.5 | 0 |
| PlayerImage | RectTransform | `m_AnchoredPosition.x` | 0 | 175 |

#### 场景里删除了、预制体里还留着的组件

- 'ContentArea ' · HorizontalLayoutGroup.cs
- LeftSide · VerticalLayoutGroup.cs
- RightSide · VerticalLayoutGroup.cs

### 实例 `&4743698551151404886` ← `Assets/Prefabs/UI/ShopCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 73 |
| 与预制体一致（无需处理） | 58 |
| **场景独有差异（未同步）** | **15** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

#### 只在场景里改过 —— 共 15 项

| 对象 | 组件/脚本 | 属性 | 场景里的值 | 预制体里的值 |
|---|---|---|---|---|
| Text (TMP) | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Text (TMP) | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| Text (TMP) | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Text (TMP) | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| ShopWindow | Image.cs | `m_RaycastTarget` | 0 | 1 |
| ShopPanel | Image.cs | `m_Color.a` | 0 | 0.78431374 |
| ShopPanel | Image.cs | `m_RaycastTarget` | 0 | 1 |
| Text (TMP) | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Text (TMP) | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| Text (TMP) | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Text (TMP) | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| Text (TMP) | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Text (TMP) | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| Glod | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| Glod | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |

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
| 修改条目总数 | 65 |
| 与预制体一致（无需处理） | 52 |
| **场景独有差异（未同步）** | **13** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

#### 只在场景里改过 —— 共 13 项

| 对象 | 组件/脚本 | 属性 | 场景里的值 | 预制体里的值 |
|---|---|---|---|---|
| CheckCanvas | Canvas | `m_SortingOrder` | 300 | 200 |
| CheckCanvas | MonsterManualUI.cs | `panelRoot` | 预制体内「Scroll View」(GameObject) | 预制体内「CheckCanvas」(GameObject) |
| Scrollbar Vertical | RectTransform | `m_AnchorMax.y` | 0.5 | 1 |
| Scrollbar Vertical | RectTransform | `m_AnchorMin.x` | 0 | 1 |
| Scrollbar Vertical | RectTransform | `m_AnchorMin.y` | 0.5 | 0 |
| Scrollbar Vertical | RectTransform | `m_SizeDelta.x` | -1880 | 20 |
| Scrollbar Vertical | RectTransform | `m_SizeDelta.y` | 1003 | 0 |
| Scrollbar Vertical | RectTransform | `m_AnchoredPosition.y` | 500 | 0 |
| Content | VerticalLayoutGroup.cs | `m_Padding.m_Top` | 50 | 0 |
| Content | VerticalLayoutGroup.cs | `m_Padding.m_Right` | 0 | 50 |
| Content | VerticalLayoutGroup.cs | `m_Padding.m_Bottom` | 25 | 0 |
| Scroll View | Image.cs | `m_Sprite` | title.png(fileID 21300000) | 1.jpg(fileID 21300000) |
| Scroll View | RectTransform | `m_SizeDelta.x` | 1900 | 1500 |

### 实例 `&8920489638242693106` ← `Assets/Prefabs/UI/BlessingCanvas.prefab`

| 项 | 数量 |
|---|---|
| 修改条目总数 | 49 |
| 与预制体一致（无需处理） | 42 |
| **场景独有差异（未同步）** | **7** |
| 僵尸条目（字段已不存在，无效） | 0 |
| 场景删除的组件 | 0 |

#### 只在场景里改过 —— 共 7 项

| 对象 | 组件/脚本 | 属性 | 场景里的值 | 预制体里的值 |
|---|---|---|---|---|
| BlessingDesc | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| BlessingDesc | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| BlessingWindow | Image.cs | `m_RaycastTarget` | 0 | 1 |
| BlessingName | TextMeshProUGUI.cs | `m_fontAsset` | SmileySans-Oblique SDF.asset(fileID 11400000) | culiufangxin24x SDF.asset(fileID 11400000) |
| BlessingName | TextMeshProUGUI.cs | `m_sharedMaterial` | SmileySans-Oblique SDF.asset(fileID -7471263506246653517) | culiufangxin24x SDF.asset(fileID -7803601053095721485) |
| BlessingPanel | Image.cs | `m_Color.a` | 0.39215687 | 0.78431374 |
| BlessingPanel | Image.cs | `m_RaycastTarget` | 0 | 1 |


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
