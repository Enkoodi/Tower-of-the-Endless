# 工程环境 / 安全改动

## 工程环境
- Unity 项目 `D:\zzzMyWork\Game\unity\Tower of the Endless`。5 个场景全 enabled：
  Opening / Game / Credits / Ending / Setting。
- 改 `.unity` / `.prefab` 前先查 `Unity.exe` 进程（本机常只有 Unity Hub 在跑，编辑器没开就能安全改 YAML）。
- **编译校验限制**：`csc` 被安全策略拦（等价 Add-Type），离线只能做「括号配对 + grep 核 API 签名」。
  CS1612 这类语言错误只有编译器能查 → **改完必须让用户回 Unity 编译一次**。

## Unity YAML 场景/预制体改动（踩过的坑）
1. 定位 MonoBehaviour **必须用 script guid 匹配**（从 `.cs.meta` 读 guid + 匹配 `m_GameObject`），
   不能猜组件 fileID（Canvas 上多个同类组件 fileID 连号）。
2. 追加字段前先读块内容，避免 YAML 重复键。
3. 改完必做：结构计数对比（GameObject/MonoBehaviour/RectTransform/m_Name）、
   引用完整性（所有 `{fileID: N}` 都要存在）、重复键检查。
4. 改前备份到 `.workbuddy/backup/`，补丁脚本写成幂等的。
5. 组件换宿主 GO = 改 `m_GameObject` + 源 GO 删条目 + 目标 GO 末尾追加（净零改动）。
6. **用户可能在编辑器里打开并保存过** → 动手前重新读当前文件，别沿用上次假设。

## 工具脚本的踩坑（写 Python 时注意）
- **Bash 工具的 heredoc 会吃掉反斜杠转义**（`re.search(r"...\n...")` 里的 `\n` 会变成真换行、
  `\r` 变真回车、`\d` 半残），导致正则莫名失配。**要写正则/含转义的脚本，一律用 Write 写到
  `.workbuddy/tools/*.py` 再用 python 跑**，不要用 `python - <<EOF`。
  （`python -c "..."` 里没有反斜杠时是安全的。）
