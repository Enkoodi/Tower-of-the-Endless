"""一次性脚本：删除战斗模块的三个死符号（P1 清理）。

删掉的东西：
  1. PlayerData.TryFight()      —— 零调用者，第二套战斗逻辑
  2. PlayerData.TakeDamage()    —— 只被 TryFight 调用（IPlayerHealth 不含它）
  3. PlayerData.FightResult()   —— private，只被 TryFight 调用
  4. EnemyController.TakeDamage() —— 零调用者（实战走 TakeRawDamage）

按行号删除 + 边界断言，改完打印结果区段供人工核对。
"""

import io
import sys

BASE = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets\Scripts"

# (文件, 起始行, 结束行(含), 起始行期望内容前缀, 结束行期望内容)
JOBS = [
    (
        BASE + r"\Player\PlayerData.cs",
        233, 341,
        "    // ============================================================",
        "",
    ),
    (
        BASE + r"\Enemies\EnemyController.cs",
        113, 121,
        "    public int TakeDamage(int rawAtk)",
        "",
    ),
]


def read_lines(path):
    with io.open(path, "r", encoding="utf-8", newline="") as f:
        return f.readlines()


def write_lines(path, lines):
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.writelines(lines)


def main():
    ok = True

    for path, start, end, expect_first, expect_last in JOBS:
        lines = read_lines(path)

        got_first = lines[start - 1]
        got_last = lines[end - 1]

        if not got_first.startswith(expect_first):
            print(f"[FAIL] {path}:{start} 期望以 {expect_first!r} 开头，实际 {got_first!r}")
            ok = False
            continue
        if got_last.rstrip("\r\n") != expect_last:
            print(f"[FAIL] {path}:{end} 期望 {expect_last!r}，实际 {got_last!r}")
            ok = False
            continue

        removed = lines[start - 1:end]
        new_lines = lines[:start - 1] + lines[end:]
        write_lines(path, new_lines)

        print(f"[OK] {path}  删除第 {start}-{end} 行（共 {len(removed)} 行）")
        print("     ---- 删除后第 %d-%d 行 ----" % (start - 4, start + 3))
        for i in range(max(0, start - 5), min(len(new_lines), start + 3)):
            print(f"     {i + 1:>4} | {new_lines[i].rstrip()}")
        print()

    print("全部完成" if ok else "有失败项，未继续")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
