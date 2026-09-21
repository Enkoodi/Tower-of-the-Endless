# 字体 / 文本（缺字方块问题的定论，2026-09-20）

`Assets/Font/smiley-sans-v2.0.1/` 下 4 个 TMP 字体资产。**根因是动态图集容量，不是设置写错**。

## 事实
- `pointSize=90 / padding=9` → 每字形约 92×92px，单张 2048² **上限约 450 字形**。
  项目实需 **865 字符（680 汉字）**。已烘焙 SmileySans 450 / culiufangxin 390 → 早已到顶。
- 挂载量：**SmileySans（45 组件）+ culiufangxin24x（16 组件）在跑**。
  `xiangcuilingduhei` / `零ゴシック` 零引用；**零ゴシック 是日文字形集，缺 348 个常用简体字，永远别用**。
- 历史隐患（部分已修）：TMP Settings 默认字体曾 = LiberationSans（**无中文**），两处 fallback 表都是空的；
  `renderMode: 4165 = SMOOTH_HINTED`，**不是 SDF**。
- **4 个资产原先都 `m_IsMultiAtlasTexturesEnabled: 0`** → 图集满了不再开新页。
  这是"没动设置突然缺字"的机制：每渲染一个没见过的字就刻一个，刻满后新字全变 □（U+25A1）。

## 已落地（2026-09-20 23:30）
- SmileySans 与 culiufangxin24x 已开 `m_IsMultiAtlasTexturesEnabled: 1`（多页由
  `TMP_MaterialManager.GetFallbackMaterial` 处理，开箱可用）。
- TMP Settings 默认字体已从 LiberationSans 改为 **SmileySans-Oblique SDF**
  → 顺带给所有文本加了一层隐式兜底（`TMPro_Private.cs:1176` 缺字链会查 defaultFontAsset）。
- 备份：`.workbuddy/backup/font_atlas_20260920_2330/`（3 个文件，改前原始状态）。
- 预期副作用：编辑器里补字会往 `.asset` 追加图集页（长到 ~17MB/个、显存 +4MB/页）。想收敛就走方案 B。

## 铁律
- **动态字形会写回 `.asset`（8.6MB，git 里常年是 `M`）。git 回滚 / Unity 强退 = 已刻字形消失，
  且图集已满补不回来。绝对不要把字体 `.asset` 当普通文件回滚。**
- Font Asset Creator 的 **`Save` 会覆盖选中的同一个 asset**（GUID 不变，61 个引用不动），
  别点 `Save as...`（那会生成新文件、引用全断）。见 `TMPro_FontAssetCreatorWindow.cs:1099`。
- 方案 B（推荐，未做）**预烘焙全套字符** → SDFAA + pointSize 48/padding 5（单张容约 1225 字形，一张够）。
  **B 之后 populationMode 保持 Dynamic，不要切 Static**：预烘焙把常见字固化（运行期不再动态加字、无脏写），
  Dynamic 只当保险丝——以后冒出新字会自动补、不方块；切 Static 则每次新字都要重跑。
  代价仅是构建里多保留源字体（+约 7MB）。

## 排查工具（可复用，只读）
`.workbuddy/tools/` 下：`font_report.py` 是**体检入口**（图集占用率/渲染模式/TMP Settings 一次看完）；
`font_charset.py` 导出字符集 + 与 `字符集_清单.json` 比对（直接输出「一致，不用重做」或列出新增字）；
`font_coverage.py` 用字 vs 烘焙 vs 缺口；`font_risk.py` 容量核算；`font_audit.py` 共用的解析库（import 无副作用）。
字符集正本：`.workbuddy/font/字符集_单行.txt`（865 字）。
统一用托管 Python 跑：`C:/Users/admin/.workbuddy/binaries/python/versions/3.13.12/python.exe`。
缺字现场直接看 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 里的 `was not found in the [...] font asset`。
