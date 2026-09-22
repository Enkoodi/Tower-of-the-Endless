# 楼层地图数据（floor_XX.json）— ID 表与解析坑位

## 数据在哪
- 地图：`Assets/Resources/floor_{编号}.json`，编号支持负数（`floor_-01` … `floor_-07`）与 `floor_00`。
  实际存在 -7 ~ 31 共 39 个文件；`.` 后没有 `_floor_doc` 之类字段参与解析，都是注释性字段。
- 每个文件 5 层：`terrain` / `objects` / `enemies` / `items` / `npcs`，均为 `height × width` 的二维 int。
- **id → 实际物品的映射不在 JSON 里**，在 `Assets/Scenes/Game.unity` 里 `MapGenerator` 组件的
  `terrainPrefabs` / `objectPrefabs` / `enemyPrefabs` / `itemPrefabs` / `npcPrefabs` 数组
  （元素 = `{id, prefab(guid), displayName}`）。改物品先改这里。

## objects 层 ID
| id | 含义 | | id | 含义 |
| --- | --- | --- | --- | --- |
| 1 | 黄之门 | | 10-20 | 各层战斗门 / 战斗触发器（8F/3F/4F/19F/20F/24F/25F/28F/29F/30F）|
| 2 | 蓝之门 | | 21-27 | 移涌之门（AeonDoor 1-8，实际落在 -1 ~ -7 层）|
| 3 | 红之门 | | 8 | 上楼梯 |
| 4/5/6 | 魂之门（PsycheDoor，需求 1000/500/2000 魂）| | 9 | 下楼梯 |
| 7 | 移涌之门（通用）| | | |

## items 层 ID（关键部分）
| id | 物品 | 效果 |
| --- | --- | --- |
| 1/2/3 | 黄之钥 / 蓝之钥 / 红之钥 | 开对应门 |
| 4 | 移涌之钥 | **所有楼层都没放**，只有移涌之门 |
| 5/6/7 | 攻击力碎片 | 攻击 +1 / +2 / +4 |
| 8/9/10 | 防御力碎片 | 防御 +2 / +4 / +8 |
| 11/12/13 | 魔力充能碎片 | 魔力充能 +10 / +20 / +40 |
| 14/15/16 | 魔力输出碎片 | 魔力输出 +10 / +20 / +40 |
| 17/18/19 | 速度碎片 | 速度 +1 / +2 / +4 |
| 20/40 | 祝福（普通 / BOSS 祝福）| |
| 21-28 | 生命恢复 | HP +100/200/300/400/200/400/600/800 |
| 29-32 | 生息/魂流/悲焰/灵知剑 | 攻击 +10/25/50/75 |
| 33-36 | 生息/魂流/悲焰/灵知盾 | 防御 +20/50/100/150 |
| 37/38 | 上/下传送器 | |
| 39 | 幸运金币 | |
| 41/42/43 | 神圣剑 / 神圣盾 / 神圣火花 | |
| 51/71/72/91-95 | 公主 / 盗贼 / 各 BOSS 类 NPC 道具 | |

数值来源：Prefab 上的 `StatBoostPickup.data` → `Assets/Data/Emanation/*.asset` 的
`boostType`（0=攻击 1=防御 2=魔力输出 3=魔力充能 4=速度 5=HP …）与 `value`。

## 金币与掉落（统计时别漏）
- **金币不是道具**：来自战斗胜利 `BattleManager` 调 `playerData.AddGold(enemy.GoldReward)`，
  数值写在 `EnemyStats.goldReward`。所以某层金币 = 该层 `enemies` + `npcs` 里**所有可战斗单位**的
  goldReward 之和。幸运金币（items 39）是 `GoldMultiplier` 倍率，不是金币。
- **掉落配置在敌人 Prefab 的 `ItemDrop` 组件**（`drops[]` 是 prefab 数组，无掉落概率，必掉）。
  真正带掉落物的只有这几个 prefab：
  `SlimeLord`(敌4) / `Slime`(敌5、NPC92) / `VampireLord`(敌9、NPC91) /
  `UndeadTotem`(敌15) / `GreatMagicMaster`(敌20、NPC93) / `Zeno`(敌32、NPC95)。
- **BOSS 有两种存在形式**：`enemies` 层的普通敌人（prefab 直接带 EnemyController），
  和 `npcs` 层的对话战斗敌人（prefab 带 `NpcBattler`，运行时 `AddComponent<EnemyController>()`）。
  同一种怪在两张表里都有登记（如吸血鬼领主 = 敌9 / NPC91），**统计时要同时扫 enemies 和 npcs**，
  只扫 enemies 会漏掉 6F/12F/20F/30F 的 BOSS 掉落与金币。
- 对话战斗 NPC 也要 `ItemDrop` 挂在**根节点**才会掉（DropManager 用 `GetComponent<ItemDrop>()`）。
- 装备类道具的数值不在 `StatBoostData` 里：
  神圣剑 = `MagicAmplifierPickup`(boostType 0, boostValue 100 + 魔力伤害×2)；
  神圣盾 = `AegisAmuletPickup`(boostType 1, boostValue 200)。
  另外 `Data/Equipment/Sword1-4 / Shield1-4` 是从不属于 `itemPrefabs` 的素材资产，别混用。

## 地图数据现存问题（普查发现，未改）
- `npcPrefabs` id 94「初觉者」(`NPC/TheHalfAwake.prefab`) 没有 `NpcBattler`，
  在 `floor_-01` ~ `floor_-07` 里被摆了 2/4/6/8/10/12/14 个，只能当装饰物看。
- 从未被任何楼层使用的条目（写入参考，不一定是 bug）：
  敌人 id **5 / 9 / 20 / 32**（史莱姆、吸血鬼领主、大魔导师、Zeno 的"敌人形态"——
  它们在正层里一律以 NPC 形态出现）；道具 id 4 / 37 / 38 / 40 / 41 / 42
  （移涌之钥、上下传送器、Boss 祝福、神圣剑、神圣盾 —— 全部只能靠掉落拿）；
  物体 id 10（8F 战斗门，由 8F 触发器运行时动态生成）。

### 已由用户修复（2026-09-22 16:08~16:10，勿再当问题报）
- ~~`enemyPrefabs` id 32 的 prefab guid 是死引用~~ → 已指向 `Prefabs/Enemies/Knight/Zeno.prefab`，
  名称也从「魔王Zeno」改为「魔导师Zeno」。
- ~~`npcPrefabs` 没有 id 4、但 `floor_26.json` 放了 NPC 4~~ → 该 NPC 已从第 26 层移除。

## 用户自己的两份「权威文档」（要更新就往这两个里改）
- `.workbuddy/reports/Tower of the Endless.txt` —— floor_XX.json 的 **ID 图例**
  （地形层 / 物体层 / 敌人层 / NPC层 / 道具层）。用户手写的速查表，改动要**保持原有极简风格**。
- `.workbuddy/reports/Tower of the Endless.xlsx` —— 只有一张表 **`角色属性表`**，
  有效区 `G1:S{n}`（G=ID、H=角色名称、I=头像占位空格、J-S=生命值/攻击力/防御力/攻击段数/
  生命偷取/反伤系数/魔力充能/魔力最大输出/速度/金币）。**无公式、无自定义格式**，
  列结构不要动。用户明确要求：**以游戏内实际数据为准**，不要另建新文件、不要换形式。
- 备份在 `.workbuddy/backup/xlsx-txt更新_2026-09-22/`。

### 名称有两套，别混淆
`enemyPrefabs` / `npcPrefabs` 的 `displayName`（编辑器里的标签）与 `EnemyStats.enemyName`
（**战斗界面和怪物图鉴实际显示的名字**，见 `BattleUI` / `MonsterManualEntryUI`）并不总是一致：
| ID | 表名 | 游戏内显示 |
| --- | --- | --- |
| 敌 4 | 史莱姆领主 | 史莱姆王 |
| 敌 18 / 19 | 魔术师（右）/（左）| 魔术师右 / 魔术师左 |
| 敌 21 / 22 / 23 | 初级 / 中级 / 高级卫兵 | 黄色 / 蓝色 / 红色卫兵 |
| NPC 95 | 魔王 | **魔导师Zeno**（和未使用的敌 32 共用 Zeno 的 EnemyStats）|

## 解析坑位（写分析脚本时）
1. **JSON 带 `//` 行注释和尾逗号**，标准 `json.loads` 会炸。要先按行剥掉注释（注意跳过字符串内的 `//`），
   再用 `,(\s*[}\]])` 去尾逗号。
2. 从 `Game.unity` 抽 `xxxPrefabs:` 数组时，**结束条件不能只看「缩进回到 0」**——
   `npcPrefabs:` 之类的下一个字段也是缩进 2，会把后续条目吃进来并按 id 覆盖前一个数组。
   判据：缩进 2 且首字符是 `\w`（即字段行）就停。
3. 统计「属性碎片」时不要只按 `StatBoost` 组件过滤——剑/盾类装备也是 `StatBoost`，
   会把它们的数值算进碎片总属性里（表现为该层碎片数量 0 却涨属性）。
4. 地图上还有系统类物体：楼梯(8/9)、战斗门与触发器(10-20)、魂门(4/5/6)、移涌门(7/21-27)，
   计数时按需排除或单列。

## 现成工具
- `.workbuddy/tools/map_cell_conflicts.py` —— **同格叠加普查**（全 39 层五层数据，哪格被放了几样东西）。
  当前结果只有 2 处：27F (5,11) 触发器+敌人 18、`floor_-07` (8,0) 门+神圣火花（后者是门优先的正常流程）。
- `.workbuddy/tools/battle_trigger_overlap.py` —— 六个战斗触发器的位置、`spawnPositions`、
  生成门的 `requiredEnemyPositions`，以及触发器与敌人/NPC 的同格冲突。
- `.workbuddy/tools/floor_resource_tally.py` —— 还原 id 语义 + 逐层累计，落盘 `floor_tally_raw.json`。
- `.workbuddy/tools/enemy_drop_probe.py` —— 抽敌人/NPC 的 `EnemyStats`(金币) 与 `ItemDrop` 掉落，落盘 `enemy_drop_raw.json`。
- `.workbuddy/tools/floor_resource_report2.py` —— **当前在用**的报表：钥匙/门/金币/属性数值/生命值，
  含 BOSS 掉落与装备数值，输出 `.workbuddy/reports/逐层资源统计.{html,csv}`。
  （`floor_resource_report.py` 是只统计数量、不含掉落的旧版，保留仅作对照。）
- `.workbuddy/tools/unity_tables_dump.py` —— **五张映射表 + 敌人/NPC 全量导出**（含数值、掉落、
  各 ID 的逐层出现次数），落盘 `.workbuddy/tools/unity_tables.json`。
  下游 `.workbuddy/reports/.Tower_of_the_Endless_Prefab映射表与敌人数据.ref/build.py`
  用它生成 `.workbuddy/reports/Tower_of_the_Endless_Prefab映射表与敌人数据.xlsx`（6 个 sheet）。
  **XLSX 重跑方式**：先跑 `unity_tables_dump.py`，再跑 `build.py`。
