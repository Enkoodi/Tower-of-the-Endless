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
