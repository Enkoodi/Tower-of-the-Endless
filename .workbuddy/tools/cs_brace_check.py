# -*- coding: utf-8 -*-
"""只读：C# 源文件括号配平自检（剥离字符串 / 字符 / // 与 /* */ 注释）。

本机 csc 被安全策略拦、离线做不了真正编译，改完 C# 后先用这个做最低成本的一道筛查：
括号不配平 = 100% 编译不过；配平不代表一定没错，但至少排除最常见的手滑。

用法:
    python cs_brace_check.py <文件或目录> [更多路径...]
"""
import sys
import os

PAIRS = {')': '(', ']': '[', '}': '{'}


def strip_noise(text):
    """把字符串字面量、字符字面量、注释替换成等长空白，保留换行以便报行号。"""
    out = []
    i, n = 0, len(text)
    state = None  # None / 'str' / 'verbatim' / 'char' / 'line' / 'block'
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ''
        if state is None:
            if c == '/' and nxt == '/':
                state = 'line'
                out.append('  ')
                i += 2
                continue
            if c == '/' and nxt == '*':
                state = 'block'
                out.append('  ')
                i += 2
                continue
            if c == '@' and nxt == '"':
                state = 'verbatim'
                out.append('  ')
                i += 2
                continue
            if c == '"':
                state = 'str'
                out.append(' ')
                i += 1
                continue
            if c == "'":
                state = 'char'
                out.append(' ')
                i += 1
                continue
            out.append(c)
            i += 1
            continue

        if state == 'line':
            if c == '\n':
                state = None
                out.append('\n')
            else:
                out.append(' ')
            i += 1
            continue

        if state == 'block':
            if c == '*' and nxt == '/':
                state = None
                out.append('  ')
                i += 2
                continue
            out.append('\n' if c == '\n' else ' ')
            i += 1
            continue

        if state == 'verbatim':
            if c == '"' and nxt == '"':      # 双双引号是转义的引号
                out.append('  ')
                i += 2
                continue
            if c == '"':
                state = None
                out.append(' ')
                i += 1
                continue
            out.append('\n' if c == '\n' else ' ')
            i += 1
            continue

        # 'str' 与 'char'
        if c == '\\':                        # 转义字符吃掉下一个
            out.append('  ')
            i += 2
            continue
        if (state == 'str' and c == '"') or (state == 'char' and c == "'"):
            state = None
            out.append(' ')
            i += 1
            continue
        out.append('\n' if c == '\n' else ' ')
        i += 1

    return ''.join(out)


def check_one(path):
    with open(path, 'r', encoding='utf-8-sig') as f:
        raw = f.read()
    clean = strip_noise(raw)
    line = 1
    stack = []
    problems = []
    for ch in clean:
        if ch == '\n':
            line += 1
        elif ch in '([{':
            stack.append((ch, line))
        elif ch in ')]}':
            if not stack:
                problems.append('第 %d 行: 多余的 "%s"' % (line, ch))
            else:
                op, oline = stack.pop()
                if op != PAIRS[ch]:
                    problems.append('第 %d 行: "%s" 与第 %d 行的 "%s" 不匹配'
                                    % (line, ch, oline, op))
    for op, oline in stack:
        problems.append('第 %d 行: "%s" 未闭合' % (oline, op))

    name = os.path.basename(path)
    if problems:
        print('[FAIL] %s' % name)
        for p in problems:
            print('       %s' % p)
        return False
    print('[ OK ] %s  (%d 行)' % (name, line))
    return True


def iter_targets(paths):
    for p in paths:
        if os.path.isdir(p):
            for root, _dirs, files in os.walk(p):
                for fn in files:
                    if fn.endswith('.cs'):
                        yield os.path.join(root, fn)
        else:
            yield p


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 2
    ok = True
    files = list(iter_targets(args))
    for path in files:
        if not check_one(path):
            ok = False
    print('---- 合计 %d 个文件，%s ----' % (len(files), '全部配平' if ok else '有问题'))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
