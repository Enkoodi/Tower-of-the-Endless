using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 开放自定义的操作。目前只有这 5 个 —— 它们都是「按一下触发一次」，
/// 不碰移动键的按下/松开成对记账，也不碰方向键（那会跟 UI 导航抢输入）。
/// </summary>
public enum KeyAction
{
    UpTeleporter = 0,
    DownTeleporter = 1,
    Ale = 2,
    ManaEssence = 3,
    HolyWater = 4,
}

/// <summary>
/// 键位表：唯一的键位定义处，设置页显示与 <see cref="PlayerMove"/> 的读取都走这里。
/// 存全局存档（改键立刻落盘，与战斗速度同路）。
/// </summary>
public static class KeyBindings
{
    public const int ActionCount = 5;

    /// <summary>设置页的一行。<c>Action</c> 为 null = 只读行（灰色、不可点）。</summary>
    public struct RowInfo
    {
        public string Label;
        public string FixedKeys;
        public KeyAction? Action;
    }

    private static readonly KeyCode[] Defaults =
    {
        KeyCode.X, KeyCode.Z, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3
    };

    /// <summary>
    /// 设置页从上到下的 12 行：左列 6 行只读（移动 / 跳层）、右列 6 行（手册只读 + 5 个可改）。
    /// 顺序即界面顺序，不要在场景里另排。
    /// </summary>
    public static readonly RowInfo[] Rows =
    {
        new RowInfo { Label = "上移",       FixedKeys = "W / ↑" },
        new RowInfo { Label = "下移",       FixedKeys = "S / ↓" },
        new RowInfo { Label = "左移",       FixedKeys = "A / ←" },
        new RowInfo { Label = "右移",       FixedKeys = "D / →" },
        new RowInfo { Label = "快速上楼",   FixedKeys = "Q" },
        new RowInfo { Label = "快速下楼",   FixedKeys = "E" },
        new RowInfo { Label = "怪物手册",   FixedKeys = "Tab" },
        new RowInfo { Label = "上楼传送器", FixedKeys = "", Action = KeyAction.UpTeleporter },
        new RowInfo { Label = "下楼传送器", FixedKeys = "", Action = KeyAction.DownTeleporter },
        new RowInfo { Label = "麦酒",       FixedKeys = "", Action = KeyAction.Ale },
        new RowInfo { Label = "魔力精华",   FixedKeys = "", Action = KeyAction.ManaEssence },
        new RowInfo { Label = "圣水",       FixedKeys = "", Action = KeyAction.HolyWater },
    };

    /// <summary>
    /// 不许绑的键：只读操作的键（绑上去会一键触发两个动作）、鼠标左右中键（点界面就触发）、
    /// 单独的修饰键。玩家点出这些键时界面提示「不可用」并还原。
    /// </summary>
    private static readonly KeyCode[] Reserved =
    {
        KeyCode.None,
        KeyCode.Escape, KeyCode.Tab,
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.Q, KeyCode.E,
        KeyCode.LeftShift, KeyCode.RightShift,
        KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.LeftAlt, KeyCode.RightAlt,
        KeyCode.LeftCommand, KeyCode.RightCommand,
        KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2,
    };

    private static KeyCode[] cache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cache = null;
    }

    private static KeyCode[] Cache
    {
        get
        {
            if (cache == null) Load();
            return cache;
        }
    }

    /// <summary>当前绑定。PlayerMove 每帧读它，改完立刻生效。</summary>
    public static KeyCode Get(KeyAction action) => Cache[(int)action];

    public static string DisplayName(KeyAction action) => DisplayName(Cache[(int)action]);

    public static bool IsReserved(KeyCode key) => Array.IndexOf(Reserved, key) >= 0;

    /// <summary>
    /// 改绑。目标键已被另一个可改操作占用时两者交换 —— 静默交换比弹提示省事，
    /// 且不会让玩家卡在「这个键就是不能用」。
    /// </summary>
    public static void Rebind(KeyAction action, KeyCode key)
    {
        int index = (int)action;
        int other = Array.IndexOf(Cache, key);
        if (other == index) return;

        KeyCode old = Cache[index];
        Cache[index] = key;
        if (other >= 0) Cache[other] = old;

        Save();
    }

    public static void ResetToDefaults()
    {
        cache = (KeyCode[])Defaults.Clone();
        Save();
    }

    /// <summary>KeyCode → 显示名。全 ASCII，避开中文字形与图集补字。</summary>
    public static string DisplayName(KeyCode key)
    {
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            return ((char)('0' + (key - KeyCode.Alpha0))).ToString();

        if (key >= KeyCode.Keypad0 && key <= KeyCode.KeypadEquals)
            return "Num" + key.ToString().Substring(6);

        switch (key)
        {
            case KeyCode.Return:    return "Enter";
            case KeyCode.Backspace: return "Back";
            case KeyCode.Delete:    return "Del";
            case KeyCode.Escape:    return "Esc";
            default:                return key.ToString();
        }
    }

    // ------------------------------------------------------------
    //  存档（全局存档，与战斗速度同路；只读-改-写，不碰 SaveGlobal）
    // ------------------------------------------------------------

    private static void Load()
    {
        cache = (KeyCode[])Defaults.Clone();

        GlobalSaveData data = SaveManager.LoadGlobalData();
        if (data == null || data.keyBindings == null) return;

        for (int i = 0; i < ActionCount && i < data.keyBindings.Count; i++)
        {
            int raw = data.keyBindings[i];
            if (raw > 0 && Enum.IsDefined(typeof(KeyCode), raw))
                cache[i] = (KeyCode)raw;
        }
    }

    private static void Save()
    {
        GlobalSaveData data = SaveManager.LoadGlobalData();
        data.keyBindings = new List<int>(ActionCount);
        for (int i = 0; i < ActionCount; i++) data.keyBindings.Add((int)cache[i]);
        SaveManager.SaveGlobalData(data);
    }
}
