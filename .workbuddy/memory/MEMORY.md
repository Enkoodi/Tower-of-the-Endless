# Tower of the Endless — 项目索引

> **这里是索引 + 铁律，不要往里塞细节。** 每个专题的完整内容在同目录 `topics/*.md`，
> 只在真要用到那一块时去读对应文件（保持本文件足够小，才能每次会话完整注入）。

Unity 项目：`D:\zzzMyWork\Game\unity\Tower of the Endless`（5 个场景：
Opening / Game / Credits / Ending / Setting）。

## 专题文件
| 文件 | 讲什么 |
| --- | --- |
| `topics/env-and-yaml.md` | 工程环境、改 `.unity`/`.prefab` 的踩坑、离线编译校验限制、工具脚本的 heredoc 坑 |
| `topics/save-blessing-stats.md` | SaveManager、特殊祝福（Conditional 型）等级、**战斗数值口径**、取整规则、商店购买 |
| `topics/battle-resolver.md` | 结算内核 `BattleResolver`、图鉴共用、反伤/提前收场、技术债 |
| `topics/ui-scene-door.md` | UI 约定、战斗速度档位、门与开门动画 |
| `topics/fonts.md` | TMP 字体缺字方块的决定性结论与诊断工具 |

## 跨专题铁律（改代码前先扫一眼）
1. **改完必须让用户回 Unity 编译一次** —— 本机 `csc` 被安全策略拦，离线查不了语言错误（如 CS1612）。
2. **结算逻辑只有一份**（`BattleResolver`），实战与图鉴共用；**祝福回调必须留在内核里**，不能挪到表现层。
3. **取整一律偏向玩家**：加成用 `Ceil`，损失量用 `Floor`；**绝不用整数除法 `/100`**。
   两个例外：`* (100-减伤)/100` 保持向下截断；夹击用 `Floor`（不致死优先）。
4. **战前 / 战时两族写入口各有守卫，不要新增第三条直写路径**；存档一律读 `Base*`。
5. **反伤一律不写进战斗日志**（用户明确要求）。
6. 绑 UI 一律 `[SerializeField]` + Awake 里 `AddListener`，**不要 `switch(button.name)`**。
7. 动态字形会写回字体 `.asset` —— **不要把字体 `.asset` 当普通文件回滚**。
8. 写含正则/转义的 Python 脚本，**用 Write 落盘再跑**，别用 heredoc（会被吃掉反斜杠）。
9. **注释/说明文本只给作者看，越短越好**：Inspector 里不塞解释性 `[Tooltip]`（字段名 + `[Header]` 够用），
   也不写面向玩家的说明文本；长解释写进 `topics/*.md`，不要留在代码里（2026-09-21 用户要求）。

## 目录约定
- `.workbuddy/memory/YYYY-MM-DD.md` —— 当日工作流水（append-only）。
- `.workbuddy/memory/topics/*.md` —— 专题长期结论。
- `.workbuddy/tools/*.py` —— 可复用的**只读**分析脚本（字体体检、门/动画接线、结算对拍等），不侵入 `Assets/`。
- `.workbuddy/backup/` —— 改动前的备份（按 `改动名_日期/` 分目录）。
