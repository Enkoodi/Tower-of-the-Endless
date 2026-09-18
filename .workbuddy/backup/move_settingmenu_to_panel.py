"""把 SettingMenu 组件从 Canvas 搬到 Panel。

改动内容：
1. SettingMenu 块的 m_GameObject: 1542338964 (Canvas) -> 1107696162 (Panel)
2. Canvas 的 m_Component 列表移除 1542338969
3. Panel 的 m_Component 列表追加 1542338969（保持在末尾，Transform 仍在第一位）

用法：python move_settingmenu_to_panel.py
"""
import io
import re
import sys

SCENE = r"D:/zzzMyWork/Game/unity/Tower of the Endless/Assets/Scenes/Setting.unity"

SETTING_MENU_COMP = "1542338969"   # SettingMenu 组件 fileID
CANVAS_GO = "1542338964"
PANEL_GO = "1107696162"


def main():
    with io.open(SCENE, encoding="utf-8", errors="strict") as f:
        text = f.read()
    original = text

    # ---------- 1) 改 m_GameObject ----------
    i = text.index("--- !u!114 &%s\n" % SETTING_MENU_COMP)
    j = text.find("\n--- ", i)
    blk = text[i:j]

    if ("m_GameObject: {fileID: %s}" % PANEL_GO) in blk:
        print("  [skip] SettingMenu already on Panel")
        return 0
    if ("m_GameObject: {fileID: %s}" % CANVAS_GO) not in blk:
        print("  [ERROR] SettingMenu 的 m_GameObject 既不是 Canvas 也不是 Panel")
        return 1

    # 只替换该块内第一次出现的 m_GameObject
    new_blk = blk.replace("m_GameObject: {fileID: %s}" % CANVAS_GO,
                          "m_GameObject: {fileID: %s}" % PANEL_GO, 1)
    text = text[:i] + new_blk + text[j:]
    print("  [ok] SettingMenu.m_GameObject -> Panel(%s)" % PANEL_GO)

    # ---------- 2) Canvas 移除该组件 ----------
    text = remove_component(text, CANVAS_GO, SETTING_MENU_COMP)

    # ---------- 3) Panel 追加该组件 ----------
    text = append_component(text, PANEL_GO, SETTING_MENU_COMP)

    if text == original:
        print("  [ERROR] 没有任何改动")
        return 1

    with io.open(SCENE, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("  Written: %d -> %d bytes" % (len(original), len(text)))
    return 0


def go_block_span(text, go_id):
    i = text.index("--- !u!1 &%s\n" % go_id)
    j = text.find("\n--- ", i)
    if j == -1:
        j = len(text)
    return i, j


def remove_component(text, go_id, comp_id):
    i, j = go_block_span(text, go_id)
    blk = text[i:j]
    line = "  - component: {fileID: %s}\n" % comp_id
    if line not in blk:
        print("  [WARN] Canvas 列表里没有该组件，跳过移除")
        return text
    text = text[:i] + blk.replace(line, "", 1) + text[j:]
    print("  [ok] Canvas.m_Component 移除 %s" % comp_id)
    return text


def append_component(text, go_id, comp_id):
    i, j = go_block_span(text, go_id)
    blk = text[i:j]
    line = "  - component: {fileID: %s}\n" % comp_id
    if line in blk:
        print("  [skip] Panel 已有该组件")
        return text

    # 在 m_Component 列表的最后一条 component 之后插入
    m = list(re.finditer(r"  - component: \{fileID: \d+\}\n", blk))
    if not m:
        print("  [ERROR] Panel 没有 m_Component 列表")
        raise SystemExit(1)
    last = m[-1]
    ins = last.end()
    new_blk = blk[:ins] + line + blk[ins:]
    text = text[:i] + new_blk + text[j:]
    print("  [ok] Panel.m_Component 追加 %s" % comp_id)
    return text


if __name__ == "__main__":
    sys.exit(main())
