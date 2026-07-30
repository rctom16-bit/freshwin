#!/usr/bin/env python3
"""Generates src/FreshWin/Assets/icon.ico from scratch.

No image libraries required: the PNG frames are encoded by hand with zlib and
packed into a multi-resolution .ico. Re-run this after changing the artwork.

    python3 tools/make_icon.py
"""

import os
import struct
import zlib

SIZES = [16, 20, 24, 32, 48, 64, 128, 256]
SS = 4  # supersampling factor per axis

TOP = (0x4C, 0x7D, 0xF0)
BOTTOM = (0x7C, 0x5C, 0xF0)


def rounded_rect(x, y, x0, y0, x1, y1, r):
    if x < x0 or x > x1 or y < y0 or y > y1:
        return False
    cx = min(max(x, x0 + r), x1 - r)
    cy = min(max(y, y0 + r), y1 - r)
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r


def in_triangle(px, py, a, b, c):
    def side(p, q):
        return (q[0] - p[0]) * (py - p[1]) - (q[1] - p[1]) * (px - p[0])

    d1, d2, d3 = side(a, b), side(b, c), side(c, a)
    has_neg = d1 < 0 or d2 < 0 or d3 < 0
    has_pos = d1 > 0 or d2 > 0 or d3 > 0
    return not (has_neg and has_pos)


def glyph(x, y):
    """White 'install' mark: an arrow pointing down into a baseline."""
    if rounded_rect(x, y, 0.452, 0.185, 0.548, 0.545, 0.048):
        return True
    if in_triangle(x, y, (0.325, 0.475), (0.675, 0.475), (0.5, 0.715)):
        return True
    if rounded_rect(x, y, 0.285, 0.775, 0.715, 0.850, 0.037):
        return True
    return False


def render(size):
    rows = []
    step = 1.0 / (size * SS)
    radius = 0.215

    for py in range(size):
        row = bytearray()
        for px in range(size):
            hits = 0
            glyph_hits = 0
            for sy in range(SS):
                for sx in range(SS):
                    x = (px * SS + sx + 0.5) * step
                    y = (py * SS + sy + 0.5) * step
                    if not rounded_rect(x, y, 0.0, 0.0, 1.0, 1.0, radius):
                        continue
                    hits += 1
                    if glyph(x, y):
                        glyph_hits += 1

            total = SS * SS
            alpha = round(255 * hits / total)
            if alpha == 0:
                row += b"\x00\x00\x00\x00"
                continue

            t = (py + 0.5) / size
            base = tuple(round(TOP[i] + (BOTTOM[i] - TOP[i]) * t) for i in range(3))

            # Blend the white mark over the gradient by its own coverage.
            w = glyph_hits / total
            colour = tuple(round(base[i] * (1 - w) + 255 * w) for i in range(3))

            row += bytes((colour[0], colour[1], colour[2], alpha))
        rows.append(bytes(row))

    return rows


def png(size, rows):
    raw = b"".join(b"\x00" + r for r in rows)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )


def main():
    frames = []
    for size in SIZES:
        frames.append((size, png(size, render(size))))
        print(f"  rendered {size}x{size}")

    out = bytearray(struct.pack("<HHH", 0, 1, len(frames)))
    offset = 6 + 16 * len(frames)

    for size, data in frames:
        dim = 0 if size >= 256 else size
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)

    for _, data in frames:
        out += data

    target = os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "src", "FreshWin", "Assets", "icon.ico",
    )
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(target, "wb") as handle:
        handle.write(out)

    print(f"wrote {target} ({len(out):,} bytes)")


if __name__ == "__main__":
    main()
