# -*- coding: utf-8 -*-
"""一次性校验（非长期工具）：把改前/改后的 TryMove 目标格分流各写一个 Python 模型，
枚举「格子内容」的所有组合 + hits 的所有排序，比对结果。

模型只保留分流骨架（照抄源码逐行转写）：
  old: hit = hits[0]，触发器只能通过专属分支生效
  new: 先遍历激活所有触发器（触发器不占用 hit），hit = 第一个非触发器
"""
import itertools

KINDS = ["door", "trigger", "pickup", "enemy", "stair", "dialogue", "npc", "wall"]


def old(order):
    if "door" in order:
        return ("door_open", False)
    hit = order[0]
    if hit == "trigger":
        return ("trigger+move", True)
    if hit == "pickup":
        return ("pickup+move", False)
    if hit == "enemy":
        return ("battle", False)
    if hit == "stair":
        return ("stair", False)
    if hit in ("dialogue", "npc"):
        return ("interact", False)
    return ("blocked_wall", False)


def new(order):
    if "door" in order:
        return ("door_open", False)
    fired = "trigger" in order
    hit = next((k for k in order if k != "trigger"), None)
    if hit is None:
        return ("move", fired)
    if hit == "pickup":
        return ("pickup+move", fired)
    if hit == "enemy":
        return ("battle", fired)
    if hit == "stair":
        return ("stair", fired)
    if hit in ("dialogue", "npc"):
        return ("interact", fired)
    return ("blocked_wall", fired)


def main():
    # 1) 组合枚举：0~3 种内容同格（含重复类型，如两个敌人）
    bad_same = []      # 无触发器却行为不同 = 真错
    bad_trig = []      # 有触发器却不生效 = 真错
    expected = []      # 主结果不同、但属于「有意修复」= 预期变化
    door_exc = []      # 带门同格：走门优先分支，触发器本来就不该生效
    n = 0
    for r in range(1, 4):
        for combo in itertools.combinations_with_replacement(KINDS, r):
            for perm in set(itertools.permutations(combo)):
                n += 1
                o, nn = old(perm), new(perm)
                if "trigger" not in perm:
                    if o != nn:
                        bad_same.append((perm, o, nn))
                elif "door" in perm:
                    # 门优先早退是既定设计（门挡着就不该隔门激活触发器）；两边必须一致
                    if o != nn:
                        bad_trig.append((perm, ("门优先分支两边不一致", o, nn)))
                    else:
                        door_exc.append(perm)
                else:
                    if not nn[1]:
                        bad_trig.append((perm, nn))
                    elif o[0] != nn[0]:
                        # 改前 hits[0] 正好是触发器 → 触发器生效但同格其它东西被跳过；
                        # 改后两者都办。这正是本次要修的行为。
                        if o == ("trigger+move", True):
                            expected.append((perm, o, nn))
                        else:
                            bad_trig.append((perm, ("主结果异常", o, nn)))

    print(f"枚举格子内容×排序 = {n} 组\n")
    print(f"【真错】无触发器却行为不一致：{len(bad_same)} 处")
    for b in bad_same[:10]:
        print("   !", b)
    print(f"【真错】有触发器却不生效：{len(bad_trig)} 处")
    for b in bad_trig[:10]:
        print("   !", b)
    print(f"【预期例外】门优先分支（触发器不生效，两边一致）：{len(door_exc)} 组")
    print(f"【预期变化】原 hits[0] 是触发器、导致同格其它东西被跳过：{len(expected)} 组，例：")
    for b in expected[:3]:
        print("   ·", b)

    print("\n关键场景：")
    print("  门 + 触发器同格：改前", old(("door", "trigger")), " 改后", new(("door", "trigger")),
          "（门优先早退，两边一致）")
    print("  触发器 + 敌人（27层）：改前 trigger 在前 →", old(("trigger", "enemy")),
          "；enemy 在前 →", old(("enemy", "trigger")), " 改后", new(("trigger", "enemy")))


if __name__ == "__main__":
    main()
