# -*- coding: utf-8 -*-
"""PNG 逐像素对比（纯 stdlib，支持 4bit 调色板）：确认门静止图 == 开门动画首帧。"""
import os
import struct
import zlib

ROOT = r"D:\zzzMyWork\Game\unity\Tower of the Endless\Assets"
LCG = os.path.join(ROOT, "Sprites", "魔塔", "LCG")
GROUND = os.path.join(ROOT, "Sprites", "魔塔", "GROUND")

CHANNELS = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}


def decode(path):
    """返回 (w, h, 每行字节数, 原始索引/像素字节)。只做结构解压，不展开调色板。"""
    data = open(path, "rb").read()
    pos, idat = 8, b""
    w = h = bd = ct = 0
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
        elif typ == b"IDAT":
            idat += body
        elif typ == b"IEND":
            break
        pos += 12 + ln

    raw = zlib.decompress(idat)
    bits = bd * CHANNELS[ct]
    stride = (w * bits + 7) // 8
    bpp = max(1, bits // 8)
    out, prev, p = bytearray(), bytearray(stride), 0
    for _ in range(h):
        f = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1:
                line[i] = (line[i] + a) & 0xFF
            elif f == 2:
                line[i] = (line[i] + b) & 0xFF
            elif f == 3:
                line[i] = (line[i] + ((a + b) >> 1)) & 0xFF
            elif f == 4:
                pp = a + b - c
                pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        out += line
        prev = line
    return w, h, stride, bytes(out)


def tile(px, stride, x, y, tw, th):
    return b"".join(px[(y + r) * stride + x:(y + r) * stride + x + tw] for r in range(th))


def ratio(a, b):
    n = sum(1 for i in range(min(len(a), len(b))) if a[i] != b[i])
    return n / len(a) if a else 1.0


print("=== 图像尺寸 ===")
info = {}
for d, tag in ((LCG, "LCG"), (GROUND, "GROUND")):
    for f in sorted(os.listdir(d)):
        if not f.lower().endswith(".png"):
            continue
        w, h, s, px = decode(os.path.join(d, f))
        info[tag + "/" + f] = (w, h, s, px)
        print("  %-13s %4dx%-4d 每行%d字节" % (tag + "/" + f, w, h, s))

print("\n=== 表格帧切分 + 与静止图对比 ===")
for n in ("2", "3", "4", "5", "6"):
    lkey, gkey = "LCG/%s.png" % n, "GROUND/%s.png" % n
    if lkey not in info:
        print("  LCG/%s.png 不存在，跳过" % n)
        continue
    lw, lh, ls, lpx = info[lkey]
    if gkey in info:
        gw, gh, gs, gpx = info[gkey]
    else:
        gw = gh = gs = gpx = None

    # 两个图块边长可能不等于 32（以 GROUND 为准，否则默认 32）
    tw = gw or 32
    th = gh or 32
    if lh % th == 0:
        cnt = lh // th
        fmt = "竖向 %d 帧" % cnt
        get = lambda i: tile(lpx, ls, 0, i * th, min(tw, lw), th)
    elif lw % tw == 0:
        cnt = lw // tw
        fmt = "横向 %d 帧" % cnt
        get = lambda i: tile(lpx, ls, i * tw, 0, tw, min(th, lh))
    else:
        print("  LCG/%s.png (%dx%d) 无法按 %dx%d 切分" % (n, lw, lh, tw, th))
        continue

    print("  LCG/%s.png = %s" % (n, fmt))
    if gpx is not None:
        g = tile(gpx, gs, 0, 0, min(tw, lw), min(th, lh))
        for i in range(cnt):
            r = ratio(g, get(i))
            mark = "<== 与静止图一致" if r < 0.001 else ""
            print("      第%d帧 vs GROUND/%s.png 差异 %.1f%% %s" % (i, n, r * 100, mark))
    # 相邻帧差异
    ds = ["%.0f%%" % (ratio(get(i), get(i + 1)) * 100) for i in range(cnt - 1)]
    print("      相邻帧差异: %s" % " ".join(ds))

print("\n=== 动画实际引用的图块（按 fileID 顺序）是否单调铺开 ===")
print("  （见 door_anim_detail.py：每段动画 8 个关键帧，0.6s，60fps -> 0.0833s/帧）")
