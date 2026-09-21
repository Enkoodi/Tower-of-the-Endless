"""
一次性工具：为「divineSpark 暴露到 Inspector」这次改动补做改动前备份。

背景：PlayerData.cs / SaveManager.cs 在工作区里本来就带着很多之前会话的未提交改动，
所以 `git show HEAD:<path>` 拿到的不是「本次改动前」的状态。做法是复制当前文件，
再把本次新增的块逐一反向替换回去（每处都断言必须命中且只命中一次），
最后用 git diff --no-index 校验 .bak → 现在的差异 == 本次改动本身。

用法（在项目根目录）：
  python .workbuddy/tools/make_backup_divinespark.py
"""

import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BACKUP_DIR = os.path.join(ROOT, ".workbuddy", "backup", "divineSpark暴露_20260921")

# (文件, [(现在的文本, 改动前的文本), ...])
REVERSE_EDITS = [
    ("Assets/Scripts/Player/PlayerData.cs", [
        (
            "            SaveManager.Instance.ApplyGlobalAeonKeys();\n"
            "            // 神圣火花同为全局道具，一起从全局存档覆盖\n"
            "            SaveManager.Instance.ApplyGlobalDivineSpark();\n",
            "            SaveManager.Instance.ApplyGlobalAeonKeys();\n",
        ),
        (
            '    [Header("全局道具（跨存档保留）")]\n'
            '    [Tooltip("神圣火花：数量 > 0 时解锁开场菜单的「无尽模式」。\\n" +\n'
            '             "运行中可直接在 Inspector 里改；与 save/global.json 的同步规则和 Aeon 钥匙完全一致——\\n" +\n'
            '             "进场景时由全局存档覆盖，存档（P）时再由这里写回全局存档。")]\n'
            "    [SerializeField] private int divineSpark = 0;\n"
            "\n"
            '    [Header("传送器数量")]\n',
            '    [Header("传送器数量")]\n',
        ),
        (
            "    public int PendingEnemyHalveBattles => pendingEnemyHalveBattles;\n"
            "\n"
            "    /// <summary>神圣火花数量（全局道具，跨存档保留；&gt;0 时解锁无尽模式）</summary>\n"
            "    public int DivineSpark => divineSpark;\n",
            "    public int PendingEnemyHalveBattles => pendingEnemyHalveBattles;\n",
        ),
        (
            "    /// <summary>直接设置 Aeon 钥匙数量（供全局存档覆盖）</summary>\n"
            "    public void SetAeonKeys(int v) => aeonKeys = v;\n"
            "\n"
            "    /// <summary>直接设置神圣火花数量（供全局存档覆盖 / Inspector 调试）</summary>\n"
            "    public void SetDivineSpark(int v) => divineSpark = v;\n"
            "\n"
            "    /// <summary>神圣火花 +amount（拾取时调用；落盘由 SaveManager 负责）</summary>\n"
            "    public void AddDivineSpark(int amount = 1)\n"
            "    {\n"
            "        divineSpark += amount;\n"
            '        Debug.Log($"[PlayerData] 获得 {amount} 个神圣火花（总计 {divineSpark}）");\n'
            "    }\n",
            "    /// <summary>直接设置 Aeon 钥匙数量（供全局存档覆盖）</summary>\n"
            "    public void SetAeonKeys(int v) => aeonKeys = v;\n",
        ),
    ]),
    ("Assets/Scripts/Core/SaveManager.cs", [
        (
            "        // 神圣火花同样是「运行时以 PlayerData 为准」（那里可以直接在 Inspector 里改），\n"
            "        // 存档时写回全局存档。没有玩家时保留文件里的旧值，绝不清零。\n"
            "        if (player != null) globalData.divineSpark = player.DivineSpark;\n"
            "\n"
            "        WriteJson(globalSavePath, globalData);\n"
            '        Debug.Log($"[SaveManager] 全局存档已保存 → {globalSavePath}" +\n'
            '                  $"（aeonKeys = {globalData.aeonKeys}，divineSpark = {globalData.divineSpark}）");\n',
            "        WriteJson(globalSavePath, globalData);\n"
            '        Debug.Log($"[SaveManager] 全局存档已保存 → {globalSavePath}");\n',
        ),
        (
            "    /// <summary>将全局存档中的 divineSpark 应用到玩家（进场景 / 读档时调用）</summary>\n"
            "    public void ApplyGlobalDivineSpark()\n"
            "    {\n"
            "        PlayerData player = FindAnyObjectByType<PlayerData>();\n"
            "        if (player == null) return;\n"
            "\n"
            "        GlobalSaveData globalData = LoadGlobal();\n"
            "        player.SetDivineSpark(globalData.divineSpark);\n"
            "    }\n"
            "\n"
            "    /// <summary>\n"
            "    /// 神圣火花 +amount，并立即写入全局存档。\n"
            "    /// 运行时的事实来源是 PlayerData（可在 Inspector 里直接改），所以这里同步改玩家再落盘 ——\n"
            "    /// 否则之后 SaveGlobal() 会用玩家那边的旧值把这一颗覆盖掉。\n"
            "    /// </summary>\n"
            "    public void AddDivineSpark(int amount = 1)\n"
            "    {\n"
            "        GlobalSaveData globalData = LoadGlobal();\n"
            "        PlayerData player = FindAnyObjectByType<PlayerData>();\n"
            "\n"
            "        if (player != null)\n"
            "        {\n"
            "            player.AddDivineSpark(amount);\n"
            "            globalData.divineSpark = player.DivineSpark;\n"
            "        }\n"
            "        else\n"
            "        {\n"
            "            // 理论上不会走到：没有玩家时退化为纯全局累加\n"
            "            globalData.divineSpark += amount;\n"
            "        }\n"
            "\n"
            "        WriteJson(globalSavePath, globalData);\n"
            '        Debug.Log($"[SaveManager] 神圣火花 +{amount}（总计 {globalData.divineSpark}），已写入全局存档");\n'
            "    }\n",
            "    /// <summary>神圣火花数量 +amount，并立即写入全局存档。</summary>\n"
            "    public void AddDivineSpark(int amount = 1)\n"
            "    {\n"
            "        GlobalSaveData globalData = LoadGlobal();\n"
            "        globalData.divineSpark += amount;\n"
            "        WriteJson(globalSavePath, globalData);\n"
            '        Debug.Log($"[SaveManager] 神圣火花 +{amount}（总计 {globalData.divineSpark}），已写入全局存档");\n'
            "    }\n",
        ),
        (
            "        // 2. 从全局存档覆盖全局道具（Aeon 钥匙 / 神圣火花）\n"
            "        ApplyGlobalAeonKeys();\n"
            "        ApplyGlobalDivineSpark();\n",
            "        // 2. 从全局存档覆盖 aeonKeys\n"
            "        ApplyGlobalAeonKeys();\n",
        ),
    ]),
]


def main():
    os.makedirs(BACKUP_DIR, exist_ok=True)
    failures = []

    for rel, edits in REVERSE_EDITS:
        src = os.path.join(ROOT, rel)
        with open(src, "r", encoding="utf-8", newline="") as f:
            text = f.read()

        for i, (new, old) in enumerate(edits, 1):
            cnt = text.count(new)
            if cnt != 1:
                failures.append(f"{rel} 第 {i} 处：匹配 {cnt} 次（应为 1 次）")
                continue
            text = text.replace(new, old)

        if failures:
            continue

        name = os.path.basename(rel)
        dst = os.path.join(BACKUP_DIR, name + ".bak")
        with open(dst, "w", encoding="utf-8", newline="") as f:
            f.write(text)
        print(f"[OK] {rel} -> {os.path.relpath(dst, ROOT)}"
              f"  ({len(text.splitlines())} 行)")

    if failures:
        print("\n失败：")
        for s in failures:
            print("  - " + s)
        sys.exit(1)


if __name__ == "__main__":
    main()
