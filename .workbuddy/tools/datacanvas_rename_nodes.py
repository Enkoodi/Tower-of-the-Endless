# -*- coding: utf-8 -*-
"""
给 DataCanvas.prefab 的 HUD 节点改名（只改 GameObject 的 m_Name，不动任何其它字段）。

映射依据（全部经脚本核对，不是猜的）：
  LeftPanel  : 11 个文本的占位内容自带标签（"生命值：123456789" 等）→ 与 PlayerHUD 的下标顺序一致
  RightPanel : 图标 sprite 与道具预制体一一对应
               ITEM/0=YellowKey  ITEM/2=BlueKey  ITEM/1=RedKey  AeonKey
               ITEM/15=UP(上楼传送器)  ITEM/16=DOWN(下楼传送器)  ITEM/11=HolyWater(圣水)
               → 第 4 行 AeonKey 正好对上 PlayerHUD 的 rightTexts[3] = Aeon，整条链自洽

安全措施：
  * 先备份到 .workbuddy/backup/<改动名>/
  * 按 `--- !u!1 &<fileID>` 定位文档块，只替换块内那一行 m_Name
  * 不改行尾符、不重排、不新增/删除任何文档
  * 回读校验：文档数量、fileID 集合、m_Name 行数量、同级重名
"""
import io
import os
import re
import shutil
import sys
from datetime import datetime

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless"
PREFAB = os.path.join(ROOT, "Assets", "Prefabs", "UI", "DataCanvas.prefab")
BACKUP_DIR = os.path.join(ROOT, ".workbuddy", "backup", "DataCanvas节点改名_20260921")

# (GameObject fileID, 期望的现有名字, 新名字, 说明)
PLAN = [
    # ---- LeftPanel ----
    ("3006716417870472278", "Image", "PlayerImage", "玩家头像（MONSTER/32.png，同 BattleCanvas.PlayerImage）"),
    ("1343930783363542086", "Text (TMP)", "HPText", "生命值"),
    ("9154261909159400059", "Text (TMP) (1)", "AttackText", "攻击力"),
    ("4258549108995669440", "Text (TMP) (2)", "DefenseText", "防御力"),
    ("6229067498065903126", "Text (TMP) (3)", "AttackCountText", "攻击段数"),
    ("4037467026283773422", "Text (TMP) (4)", "LifeStealText", "吸血"),
    ("8969826290613718500", "Text (TMP) (5)", "ReflectDamageText", "反伤"),
    ("2740171748441977153", "Text (TMP) (6)", "DamageReductionText", "减伤"),
    ("3436173292503037979", "Text (TMP) (7)", "ManaChargeText", "魔力充能"),
    ("5691761528651268616", "Text (TMP) (8)", "ManaMaxText", "魔力输出（PlayerData.ManaMax）"),
    ("5380841183486878782", "Text (TMP) (9)", "SpeedText", "速度"),
    ("5298689594287648987", "Text (TMP) (10)", "GoldText", "金币"),
    # ---- RightPanel ----
    ("700130904031783224", "Floor", "FloorText", "当前楼层"),
    ("8504746254862470735", "Text (TMP) (7)", "YellowKeyText", "黄钥匙"),
    ("3792330286968327992", "Text (TMP) (8)", "BlueKeyText", "蓝钥匙"),
    ("6389425453425635401", "Text (TMP) (9)", "RedKeyText", "红钥匙"),
    ("209154887870739570", "Text (TMP) (10)", "AeonKeyText", "移涌之钥"),
    ("547339985941371237", "Text (TMP) (11)", "UpTeleporterText", "上楼传送器"),
    ("3942257819431378866", "Text (TMP) (12)", "DownTeleporterText", "下楼传送器"),
    ("1101867944985041812", "Text (TMP) (13)", "EnemyHalveItemText", "圣水（EnemyHalveItemCount）"),
    ("2272411706718523679", "Image", "YellowKeyIcon", "黄钥匙图标 ITEM/0"),
    ("5480013146755456881", "Image (1)", "BlueKeyIcon", "蓝钥匙图标 ITEM/2"),
    ("1381329424753468464", "Image (2)", "RedKeyIcon", "红钥匙图标 ITEM/1"),
    ("3475113104182501500", "Image (3)", "AeonKeyIcon", "移涌之钥图标 AeonKey.png"),
    ("8220555130126443900", "Image (4)", "UpTeleporterIcon", "上楼传送器图标 ITEM/15"),
    ("740465951088324627", "Image (5)", "DownTeleporterIcon", "下楼传送器图标 ITEM/16"),
    ("2834836339108596504", "Image (6)", "EnemyHalveItemIcon", "圣水图标 ITEM/11"),
]


def snapshot(text):
    """结构性指纹：文档 id 列表 + m_Name 行总数"""
    ids = re.findall(r"(?m)^--- !u!(\d+) &(-?\d+)$", text)
    return ids, len(re.findall(r"(?m)^  m_Name: ", text))


def main():
    raw = io.open(PREFAB, "rb").read()
    text = raw.decode("utf-8")

    before_ids, before_names = snapshot(text)
    print("改前：文档 %d 个，m_Name 行 %d 行" % (len(before_ids), before_names))

    os.makedirs(BACKUP_DIR, exist_ok=True)
    stamp = datetime.now().strftime("%H%M%S")
    bak = os.path.join(BACKUP_DIR, "DataCanvas.prefab.%s.bak" % stamp)
    shutil.copy2(PREFAB, bak)
    print("备份：", os.path.relpath(bak, ROOT))

    changed = 0
    problems = []
    for go_id, expect_old, new_name, memo in PLAN:
        key = "--- !u!1 &%s\n" % go_id
        i = text.find(key)
        if i < 0:
            problems.append("找不到 GameObject 块：%s" % go_id)
            continue
        j = text.find("\n--- !u!", i + 1)
        if j < 0:
            j = len(text)
        block = text[i:j]
        m = re.search(r"(?m)^  m_Name: (.*)$", block)
        if not m:
            problems.append("块内没有 m_Name：%s" % go_id)
            continue
        cur = m.group(1)
        if cur != expect_old:
            problems.append("名字与预期不符 %s：实际=%r 预期=%r" % (go_id, cur, expect_old))
            continue
        new_block = block[:m.start(1)] + new_name + block[m.end(1):]
        text = text[:i] + new_block + text[j:]
        changed += 1
        print("  %-14s -> %-20s  (%s)" % (cur, new_name, memo))

    if problems:
        print("\n!! 有 %d 处未按预期处理，**未写盘**：" % len(problems))
        for p in problems:
            print("   -", p)
        return 1

    io.open(PREFAB, "wb").write(text.encode("utf-8"))

    # ---------- 回读校验 ----------
    after_raw = io.open(PREFAB, "rb").read()
    after = after_raw.decode("utf-8")
    after_ids, after_names = snapshot(after)

    print("\n--- 回读校验 ---")
    ok = True
    if len(after_ids) != len(before_ids):
        print("✗ 文档数量变了：%d -> %d" % (len(before_ids), len(after_ids)))
        ok = False
    if sorted(after_ids) != sorted(before_ids):
        print("✗ fileID 集合发生变化")
        ok = False
    if after_names != before_names:
        print("✗ m_Name 行数变了：%d -> %d" % (before_names, after_names))
        ok = False
    if changed != len(PLAN):
        print("✗ 只改了 %d / %d 处" % (changed, len(PLAN)))
        ok = False

    # 新名字在（同一父级内）不能重名；这里直接检查全文件新名字唯一
    new_names = [p[2] for p in PLAN]
    if len(set(new_names)) != len(new_names):
        print("✗ 计划里存在重复的新名字")
        ok = False
    for n in new_names:
        cnt = len(re.findall(r"(?m)^  m_Name: %s$" % re.escape(n), after))
        if cnt != 1:
            print("✗ 新名字 %s 在文件里出现 %d 次（应为 1）" % (n, cnt))
            ok = False

    # 旧名字不该再以原样出现在这些块里（只报“剩余文本/图片”这类通用名，供人工确认）
    left_generic = re.findall(r"(?m)^  m_Name: (Text \(TMP\)[^$]*|Image[^$]*)$", after)
    generic = [g for g in left_generic if g in ("Text (TMP)", "Image")
               or re.match(r"^Text \(TMP\) \(\d+\)$", g) or re.match(r"^Image \(\d+\)$", g)]
    print("仍是通用名的节点：%s" % ("无" if not generic else generic))

    print("结果：%s" % ("全部通过 ✓" if ok else "有问题 ✗"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
