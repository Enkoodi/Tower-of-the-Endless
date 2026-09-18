"""一次性补丁：给 Setting.unity 的 SettingMenu 补引用字段、给速度按钮补 speedDelay。

用法：python patch_setting_scene.py
原地修改 Setting.unity（已备份到 .workbuddy/backup/Setting.unity.20260918-2348.bak）。

块切分约定：get_block 返回 [起始, 下一个 '--- ' 之前)，
下一块的换行符不包含在范围内，因此可以安全地在末尾追加新行。
"""
import io
import sys

SCENE = r"D:/zzzMyWork/Game/unity/Tower of the Endless/Assets/Scenes/Setting.unity"

# GameObject 名 -> (Button 组件 fileID, OpeningButton 组件 fileID)
BTN = {
    "NewGame":    ("167860",     "167863"),
    "Return":     ("97274945",   "97274948"),
    "Title":      ("374495495",  "374495498"),
    "Exit":       ("1372815455", "1372815458"),
    "Button":     ("1243487075", "1243487078"),
    "Button (1)": ("968713654",  "968713657"),
    "Button (2)": ("301168210",  "301168213"),
    "Button (3)": ("2121272235", "2121272238"),
}

SPEED = {"Button": 1, "Button (1)": 0.5, "Button (2)": 0.25, "Button (3)": 0.01}
SPEED_ORDER = ["Button", "Button (1)", "Button (2)", "Button (3)"]

SETTING_MENU_GUID = "e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0"
BATTLE_SPEED_RT = "798531793"


def find_component_on(text, guid, go_id):
    """按 script guid + GameObject id 定位 MonoBehaviour 块，返回 (i, j, blk)。"""
    for m in __import__("re").finditer(r"--- !u!114 &\d+\nMonoBehaviour:", text):
        i = m.start()
        j = text.find("\n--- ", i)
        if j == -1:
            j = len(text)
        blk = text[i:j]
        if ("guid: %s" % guid) in blk and ("m_GameObject: {fileID: %s}" % go_id) in blk:
            return i, j, blk
    raise SystemExit("ERROR: component guid=%s on GO=%s not found" % (guid, go_id))


def get_block(text, header):
    i = text.index(header)
    j = text.find("\n--- ", i)
    if j == -1:
        j = len(text)
    return i, j, text[i:j]


def main():
    with io.open(SCENE, encoding="utf-8", errors="strict") as f:
        text = f.read()

    original_len = len(text)

    for name in SPEED_ORDER:
        ob_id = BTN[name][1]
        header = "--- !u!114 &%s\n" % ob_id
        i, j, blk = get_block(text, header)

        if "speedDelay:" in blk:
            print("  [skip] %s already has speedDelay" % name)
            continue

        if "transitionSpeed:" not in blk:
            print("  [ERROR] %s: transitionSpeed not found" % name)
            return 1

        new_blk = blk.rstrip("\n") + "\n  speedDelay: %s\n" % SPEED[name]
        text = text[:i] + new_blk + text[j:]
        print("  [ok] %s.speedDelay = %s" % (name, SPEED[name]))

    # SettingMenu 挂在 Canvas(GO=1542338964) 上，按 script guid 精确定位
    i, j, blk = find_component_on(text, SETTING_MENU_GUID, "1542338964")

    if "newGameButton:" in blk:
        print("  [skip] SettingMenu already has reference fields")
    else:
        print("  --- SettingMenu block BEFORE ---")
        print("  " + "\n  ".join(blk.splitlines()))
        print("  --- appending ---")
        # 注意：titleSceneName / gameSceneName 场景里已存在，不要重复写
        refs = [
            "  newGameButton: {fileID: %s}" % BTN["NewGame"][0],
            "  returnButton: {fileID: %s}" % BTN["Return"][0],
            "  titleButton: {fileID: %s}" % BTN["Title"][0],
            "  exitButton: {fileID: %s}" % BTN["Exit"][0],
            "  battleSpeedRoot: {fileID: %s}" % BATTLE_SPEED_RT,
            "  speedButtons:",
        ]
        for nm in SPEED_ORDER:
            refs.append("  - {fileID: %s}" % BTN[nm][1])

        new_blk = blk.rstrip("\n") + "\n" + "\n".join(refs) + "\n"
        text = text[:i] + new_blk + text[j:]
        print("  [ok] SettingMenu references written")

    if len(text) == original_len:
        print("No changes made.")
        return 1

    with io.open(SCENE, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)

    print("Written: %s (%d -> %d bytes)" % (SCENE, original_len, len(text)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
