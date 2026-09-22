# NPC 对话系统（`Assets/Scripts/Dialogue/`）

## 一套数据，两个用途

`DialogueTrigger`（挂在 NPC 上，带 `BoxCollider2D`）持有 `dialogueLines[]`；
`PlayerMove.TryMove` 撞到它 → `DialogueUI.OpenDialogue(trigger)`。
`DialogueUI` 是场景里的**全局单例面板**（不是 NPC 的子物体），和 `BattleManager` 一样是"公共设施"。

- `DialogueLine`：`speakerName` / `content` / `showChoices` / `nextLineIndex`
  - `nextLineIndex`：`-1` 顺序取下句；`>=0` 跳转；**`-2` 显式结束**。
  - `showChoices` 只在"本句没有下一句"时生效 → 选项句要么是数组最后一句，要么 `nextLineIndex: -2`。
- `entryBranches`：按特殊敌人击败信号决定**从第几句开始**（`SpecialEnemyManager.IsDefeated`）。
  这是现有"同一 NPC 第二次对话说不同话"的做法（例：`3F.prefab` 分 Zeno 是否已击败）。

## 选项的三种后果（2026-09-22 起）

选项文本 + `onChoice1/onChoice2`（UnityEvent，绑目标脚本的无参方法）+ **`choice1NextLineIndex`/`choice2NextLineIndex`**：

| 配置 | 行为 |
| --- | --- |
| 续接索引 `-1` | 执行事件后关闭对话（原有行为） |
| 续接索引 `>=0`，事件不开战 | 面板不关，**从该句接着播** |
| 续接索引 `>=0`，事件开了一场战斗 | 面板先收起，**战斗胜利结束后自动弹回来**续接（战败不弹） |

续接句写在数组后面即可，线性流程走不到它们。`-1` 是新增序列化字段的默认值（Inspector 里核对一下）。

### 实现要点（改这块前必读）

1. **打开时复制** `dialogueLines` + 选项文本（`currentLines` / `choice1TextCache` …）。
   选项里常见的 `NpcRemover.RemoveSelf` / `NpcBattler` 胜利销毁都会**当场销毁 NPC 自身**，
   面板不能持有 `currentTrigger` 去读数据（销毁后是 fake-null，读属性会抛 MissingReferenceException）。
2. **`ExecuteChoice` 的顺序：先收起面板 → 再 `Invoke()` → 最后决定去哪**。
   顺序反了会出现"跳过档不弹回来"：战斗速度选「跳过」时，`StartBattle` 的协程**同一帧同步跑完**，
   `OnBattleClose` 会在 `Invoke()` 内部就触发，所以 `awaitingPostBattle` / `postBattleLineIndex`
   必须在 Invoke **之前**写好。
3. 战后续接靠订阅静态事件 `BattleManager.OnBattleClose`，胜负从 `BattleManager.LastBattleWon` 读
   （`BattleManager` 里就为这个加了该属性）。面板若已销毁要 `OnDestroy` 里退订。
4. `panelShown` 保证 `OnPanelOpen/OnPanelClose` 成对：`PlayerMove` 靠这对事件锁/解锁移动，
   "隐藏面板保留数据"（`HidePanel`）后不要再发一次关闭事件。
5. 战后续接时若原 trigger 已销毁，`ShowChoiceButtons` 会直接不显示选项（只剩文本，点一下关闭）——
   即**战后那段话里不能再用选项**。要在战后继续给选项，就得改成"延后销毁 NPC"而不是复制数据。

### 尚未处理 / 已知边界

- 选项事件若打开**祝福选择面板**（`MerchantPurchase` 发祝福类道具 → `BlessingManager.ShowWithPool`），
  续接句会和祝福面板同时出现。要挡住就在 `IsBattleRunning` 旁边再加一条"祝福面板开着"的判断。
- 选项事件是 UnityEvent，**拿不到返回值**：`MerchantPurchase.TryPurchase` 买不起时也照样会走
  同一段后续对话（而且现有 NPC 往往还同时绑了 `NpcRemover.RemoveSelf`，买不起也把 NPC 移除了）。
  要按成功/失败分叉，得换成脚本驱动的入口，不是配字段能解决的。
- 新增台词会引入新字形 → 先看 `topics/fonts.md`，缺字会显示方块（动态字形会写回字体 `.asset`）。
