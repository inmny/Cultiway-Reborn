#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import argparse
import hashlib
import json
import math
import random
import shutil
import struct
import zlib
from pathlib import Path


PHASES = {
    "cast": (48, 48, 5, 0.5, 0.5, 8.0),
    "muzzle": (40, 32, 4, 0.18, 0.5, 8.0),
    "trail": (28, 24, 4, 0.62, 0.5, 8.0),
    "impact": (64, 64, 7, 0.5, 0.5, 8.0),
    "residual": (48, 48, 5, 0.5, 0.5, 8.0),
}

PALETTES = {
    "generic": ("#dce8ff", "#ffffff", "#8da7d8", "#69718c"),
    "fire": ("#ff4a1a", "#ffd15a", "#8e1b0c", "#ff8a26"),
    "water": ("#22bdf2", "#c7f9ff", "#075d9e", "#58e6ff"),
    "wood": ("#2fbc44", "#b6ff72", "#14652e", "#7ee15d"),
    "metal": ("#ffe26a", "#ffffff", "#9b7728", "#d0d8e8"),
    "earth": ("#b16a35", "#edc06a", "#5a3823", "#c79a62"),
    "lightning": ("#4ad8ff", "#ffffff", "#2b55d9", "#b7f6ff"),
    "wind": ("#99ffe8", "#ffffff", "#4fb7bc", "#c8fff4"),
    "neg": ("#48206e", "#b16bff", "#130818", "#7b2fc5"),
    "pos": ("#ffd94a", "#ffffff", "#b67b16", "#fff29a"),
    "entropy": ("#ff3bbb", "#67f3ff", "#521066", "#d521ff"),
}


class Canvas:
    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.data = bytearray(width * height * 4)

    def blend(self, x, y, color):
        x = int(round(x))
        y = int(round(y))
        if x < 0 or y < 0 or x >= self.width or y >= self.height:
            return

        r, g, b, a = color
        if a <= 0:
            return

        idx = (y * self.width + x) * 4
        da = self.data[idx + 3] / 255.0
        sa = a / 255.0
        oa = sa + da * (1.0 - sa)
        if oa <= 0:
            return

        self.data[idx] = int((r * sa + self.data[idx] * da * (1.0 - sa)) / oa)
        self.data[idx + 1] = int((g * sa + self.data[idx + 1] * da * (1.0 - sa)) / oa)
        self.data[idx + 2] = int((b * sa + self.data[idx + 2] * da * (1.0 - sa)) / oa)
        self.data[idx + 3] = int(oa * 255)


def color(hex_value, alpha=255):
    value = hex_value.lstrip("#")
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), alpha


def scale_alpha(c, factor):
    return c[0], c[1], c[2], max(0, min(255, int(c[3] * factor)))


def save_png(path, canvas):
    def chunk(tag, payload):
        return (
            struct.pack(">I", len(payload))
            + tag
            + payload
            + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)
        )

    raw = bytearray()
    row_bytes = canvas.width * 4
    for y in range(canvas.height):
        raw.append(0)
        start = y * row_bytes
        raw.extend(canvas.data[start:start + row_bytes])

    payload = struct.pack(">IIBBBBB", canvas.width, canvas.height, 8, 6, 0, 0, 0)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", payload)
    data += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    data += chunk(b"IEND", b"")
    path.write_bytes(data)


def disc(c, cx, cy, radius, rgba):
    radius = max(0.25, radius)
    r2 = radius * radius
    for y in range(math.floor(cy - radius), math.ceil(cy + radius) + 1):
        for x in range(math.floor(cx - radius), math.ceil(cx + radius) + 1):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r2:
                c.blend(x, y, rgba)


def rect(c, x0, y0, x1, y1, rgba):
    for y in range(math.floor(min(y0, y1)), math.ceil(max(y0, y1)) + 1):
        for x in range(math.floor(min(x0, x1)), math.ceil(max(x0, x1)) + 1):
            c.blend(x, y, rgba)


def line(c, x0, y0, x1, y1, rgba, thickness=1.0):
    steps = max(1, int(math.hypot(x1 - x0, y1 - y0) * 2))
    for i in range(steps + 1):
        t = i / steps
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        disc(c, x, y, thickness / 2.0, rgba)


def ring(c, cx, cy, radius, thickness, rgba, coverage=1.0, start_angle=0.0):
    steps = max(18, int(radius * 8 * max(0.12, coverage)))
    last = None
    total_angle = math.tau * coverage
    for i in range(steps + 1):
        a = start_angle + total_angle * i / steps
        point = cx + math.cos(a) * radius, cy + math.sin(a) * radius
        if last is not None:
            line(c, last[0], last[1], point[0], point[1], rgba, thickness)
        last = point


def ray(c, cx, cy, angle, start, length, rgba, thickness=1.0):
    line(
        c,
        cx + math.cos(angle) * start,
        cy + math.sin(angle) * start,
        cx + math.cos(angle) * length,
        cy + math.sin(angle) * length,
        rgba,
        thickness,
    )


def shard(c, cx, cy, angle, length, rgba, width=2.0):
    dx = math.cos(angle)
    dy = math.sin(angle)
    sx = -dy * width
    sy = dx * width
    x0 = cx - dx * length * 0.35
    y0 = cy - dy * length * 0.35
    x1 = cx + dx * length * 0.65
    y1 = cy + dy * length * 0.65
    line(c, x0 + sx, y0 + sy, x1, y1, rgba, 1.2)
    line(c, x0 - sx, y0 - sy, x1, y1, rgba, 1.2)
    line(c, x0 - sx, y0 - sy, x0 + sx, y0 + sy, scale_alpha(rgba, 0.8), 1.0)


def leaf(c, cx, cy, angle, rgba, size=3.0):
    dx = math.cos(angle)
    dy = math.sin(angle)
    px = -dy
    py = dx
    line(c, cx - dx * size, cy - dy * size, cx + dx * size, cy + dy * size, rgba, 1.0)
    line(c, cx, cy, cx + px * size * 0.65, cy + py * size * 0.65, scale_alpha(rgba, 0.8), 1.0)
    line(c, cx, cy, cx - px * size * 0.65, cy - py * size * 0.65, scale_alpha(rgba, 0.8), 1.0)


def bolt(c, x0, y0, x1, y1, rgba, rng, thickness=1.4, jag=5.0, branches=1):
    segments = 5
    points = [(x0, y0)]
    dx = x1 - x0
    dy = y1 - y0
    length = max(1.0, math.hypot(dx, dy))
    px = -dy / length
    py = dx / length
    for i in range(1, segments):
        t = i / segments
        off = rng.uniform(-jag, jag) * (1.0 - abs(0.5 - t))
        points.append((x0 + dx * t + px * off, y0 + dy * t + py * off))
    points.append((x1, y1))

    for a, b in zip(points, points[1:]):
        line(c, a[0], a[1], b[0], b[1], rgba, thickness)
        line(c, a[0], a[1], b[0], b[1], color("#ffffff", int(rgba[3] * 0.85)), max(1.0, thickness * 0.45))

    for i in range(branches):
        origin = points[rng.randrange(1, len(points) - 1)]
        angle = math.atan2(dy, dx) + rng.choice([-1, 1]) * rng.uniform(0.65, 1.25)
        end = origin[0] + math.cos(angle) * rng.uniform(5, 12), origin[1] + math.sin(angle) * rng.uniform(5, 12)
        line(c, origin[0], origin[1], end[0], end[1], scale_alpha(rgba, 0.75), max(1.0, thickness * 0.55))


def motes(c, rng, count, center, radius, rgba, size=(0.8, 1.8)):
    cx, cy = center
    for _ in range(count):
        a = rng.random() * math.tau
        d = rng.uniform(0, radius)
        disc(c, cx + math.cos(a) * d, cy + math.sin(a) * d, rng.uniform(size[0], size[1]), rgba)


def noise_blocks(c, rng, count, rgba, center, radius, max_size=3):
    cx, cy = center
    for _ in range(count):
        a = rng.random() * math.tau
        d = rng.uniform(0, radius)
        x = cx + math.cos(a) * d
        y = cy + math.sin(a) * d
        w = rng.randint(1, max_size)
        h = rng.randint(1, max_size)
        rect(c, x, y, x + w, y + h, rgba)


def progress(frame, total):
    return frame / max(1, total - 1)


def draw_cast(c, element, pal, frame, total, rng):
    p = progress(frame, total)
    cx = c.width / 2
    cy = c.height / 2
    main = color(pal[0], 185)
    hi = color(pal[1], 220)
    dark = color(pal[2], 150)
    acc = color(pal[3], 190)

    if element == "fire":
        ring(c, cx, cy, 8 + p * 10, 2.0, scale_alpha(main, 0.7), 0.78, p * 2.2)
        for i in range(8):
            a = i * math.tau / 8 + p * 1.8
            ray(c, cx, cy, a, 3, 9 + rng.uniform(4, 12) * (1 - p * 0.35), i % 2 and main or hi, 2.0)
        motes(c, rng, 9, (cx, cy), 18, acc, (0.8, 1.5))
    elif element == "water":
        for i in range(3):
            ring(c, cx, cy, 6 + p * 10 + i * 5, 1.2, scale_alpha(main, 0.62 - i * 0.12), 0.92, p + i)
        for i in range(7):
            a = i * math.tau / 7 - p * 1.3
            disc(c, cx + math.cos(a) * (12 + p * 4), cy + math.sin(a) * (8 + p * 5), 1.3, hi)
    elif element == "wood":
        ring(c, cx, cy, 11 + p * 5, 1.5, dark, 0.68, -p * 2.0)
        for i in range(9):
            a = i * math.tau / 9 + p * 1.2
            ray(c, cx, cy, a, 5, 12 + p * 8, main, 1.3)
            leaf(c, cx + math.cos(a) * 15, cy + math.sin(a) * 15, a + 0.7, hi, 2.5)
    elif element == "metal":
        for i in range(6):
            a = i * math.tau / 6 + p * 1.7
            shard(c, cx + math.cos(a) * 9, cy + math.sin(a) * 9, a, 13 + p * 3, hi if i % 2 else main, 1.8)
        ring(c, cx, cy, 15 + p * 2, 1.0, scale_alpha(acc, 0.55), 0.72, p)
    elif element == "earth":
        ring(c, cx, cy, 9 + p * 8, 2.2, dark, 0.76, p)
        for i in range(8):
            a = i * math.tau / 8 + rng.uniform(-0.2, 0.2)
            shard(c, cx + math.cos(a) * (9 + p * 7), cy + math.sin(a) * (9 + p * 6), a, rng.uniform(4, 8), main, 2.2)
        motes(c, rng, 16, (cx, cy), 18, scale_alpha(acc, 0.48), (0.8, 2.2))
    elif element == "lightning":
        ring(c, cx, cy, 11 + p * 6, 1.1, main, 0.58, p * 3.0)
        for i in range(5):
            a = i * math.tau / 5 + p * 1.9
            bolt(c, cx, cy, cx + math.cos(a) * (13 + p * 8), cy + math.sin(a) * (13 + p * 8), hi if i % 2 else main, rng, 1.4, 3.5, 0)
    elif element == "wind":
        for i in range(4):
            ring(c, cx, cy, 7 + i * 4 + p * 5, 1.0, scale_alpha(main, 0.65 - i * 0.1), 0.42, p * 3 + i)
        motes(c, rng, 7, (cx, cy), 16, scale_alpha(hi, 0.55), (0.6, 1.1))
    elif element == "neg":
        disc(c, cx, cy, 6 + p * 4, scale_alpha(dark, 0.65))
        for i in range(5):
            ring(c, cx, cy, 10 + i * 3 + p * 3, 2.0, scale_alpha(main if i % 2 else acc, 0.62), 0.25, i + p)
        motes(c, rng, 12, (cx, cy), 19, scale_alpha(dark, 0.7), (1.2, 2.8))
    elif element == "pos":
        ring(c, cx, cy, 9 + p * 9, 1.4, main, 1.0, 0)
        for i in range(12):
            a = i * math.tau / 12 + p * 0.5
            ray(c, cx, cy, a, 5, 12 + p * 12 * (1 if i % 3 == 0 else 0.65), hi if i % 3 == 0 else main, 1.1)
        disc(c, cx, cy, 3 + p * 3, hi)
    elif element == "entropy":
        ring(c, cx, cy, 12 + p * 6, 1.3, main, 0.18, p * 4)
        ring(c, cx, cy, 17 - p * 3, 1.0, acc, 0.22, -p * 5)
        noise_blocks(c, rng, 20, scale_alpha(main, 0.7), (cx, cy), 18, 3)
        bolt(c, cx - 11, cy + 5, cx + 12, cy - 6, acc, rng, 1.0, 7, 0)
    else:
        ring(c, cx, cy, 8 + p * 11, 1.4, main, 0.9, p)
        ring(c, cx, cy, 14 + p * 5, 1.0, hi, 0.55, -p * 2)
        motes(c, rng, 8, (cx, cy), 15, acc, (0.8, 1.4))


def draw_muzzle(c, element, pal, frame, total, rng):
    p = progress(frame, total)
    cy = c.height / 2
    main = color(pal[0], 205)
    hi = color(pal[1], 235)
    dark = color(pal[2], 170)
    acc = color(pal[3], 195)

    if element == "fire":
        for i in range(6):
            a = rng.uniform(-0.45, 0.45)
            ray(c, 5, cy, a, 0, 17 + p * 15 + rng.uniform(-3, 3), hi if i % 3 == 0 else main, 2.4 - i * 0.14)
        motes(c, rng, 8, (20 + p * 8, cy), 12, acc, (0.8, 1.5))
    elif element == "water":
        for i in range(4):
            y = cy + (i - 1.5) * 3
            line(c, 4, y, 28 + p * 8, y + math.sin(i + p * 4) * 4, main if i % 2 else hi, 1.8)
        ring(c, 24 + p * 5, cy, 5 + p * 3, 1.0, acc, 0.45, p)
    elif element == "wood":
        line(c, 4, cy, 34, cy + math.sin(p * math.pi) * 3, dark, 2.2)
        for i in range(5):
            x = 9 + i * 6 + p * 2
            shard(c, x, cy + rng.uniform(-4, 4), rng.uniform(-0.5, 0.5), 7 + p * 5, main if i % 2 else hi, 1.4)
            leaf(c, x - 1, cy + rng.uniform(-6, 6), rng.uniform(-0.6, 0.6), acc, 2.2)
    elif element == "metal":
        shard(c, 21, cy, 0.0, 30 + p * 5, hi, 2.6)
        line(c, 8, cy - 5, 34, cy + 3, main, 1.1)
        line(c, 12, cy + 6, 30, cy - 4, color("#ffffff", 190), 1.0)
    elif element == "earth":
        line(c, 4, cy + 7, 35, cy + 7, dark, 2.0)
        for i in range(5):
            x = 10 + i * 5 + p * 2
            shard(c, x, cy + rng.uniform(-3, 6), -0.2 + rng.uniform(-0.2, 0.2), 5 + p * 4, main, 2.0)
        motes(c, rng, 12, (24, cy + 3), 12, scale_alpha(acc, 0.55), (0.7, 1.8))
    elif element == "lightning":
        bolt(c, 4, cy, 36, cy + rng.uniform(-3, 3), hi, rng, 1.8, 7, 3)
    elif element == "wind":
        for i in range(3):
            ring(c, 18 + i * 5 + p * 3, cy, 7 + i * 2, 1.3, main if i % 2 else hi, 0.32, -0.8 + i * 0.7 + p)
        line(c, 4, cy, 34, cy + 1, scale_alpha(acc, 0.55), 1.0)
    elif element == "neg":
        ring(c, 18, cy, 10 + p * 4, 3.0, main, 0.28, -0.8 + p)
        disc(c, 14 + p * 6, cy, 6 + p * 2, scale_alpha(dark, 0.7))
        line(c, 9, cy + 5, 32, cy - 5, acc, 1.2)
    elif element == "pos":
        ray(c, 5, cy, 0, 0, 34 + p * 4, hi, 2.4)
        for i in range(5):
            ray(c, 8, cy, (i - 2) * 0.18, 2, 26 + p * 6, main, 1.1)
        ring(c, 10, cy, 6 + p * 2, 1.1, hi, 1.0, 0)
    elif element == "entropy":
        bolt(c, 5, cy, 34, cy + rng.uniform(-7, 7), main, rng, 1.4, 9, 1)
        noise_blocks(c, rng, 12, acc, (22, cy), 13, 3)
    else:
        ray(c, 5, cy, 0, 0, 28 + p * 8, main, 1.8)
        ring(c, 16 + p * 5, cy, 6 + p * 2, 1.1, hi, 0.72, p)


def draw_trail(c, element, pal, frame, total, rng):
    p = progress(frame, total)
    cy = c.height / 2
    main = color(pal[0], int(175 * (1 - p * 0.28)))
    hi = color(pal[1], int(205 * (1 - p * 0.35)))
    dark = color(pal[2], 135)
    acc = color(pal[3], 160)

    if element == "fire":
        for i in range(5):
            disc(c, 19 - i * 4, cy + rng.uniform(-4, 4), max(1.0, 4 - i * 0.6 - p), main if i else hi)
        motes(c, rng, 6, (9, cy), 9, acc, (0.6, 1.3))
    elif element == "water":
        for i in range(3):
            line(c, 4, cy + (i - 1) * 3, 24, cy + math.sin(i + p * 5) * 4, hi if i == 1 else main, 1.2)
        motes(c, rng, 5, (12, cy), 8, acc, (0.6, 1.2))
    elif element == "wood":
        line(c, 5, cy, 23, cy + rng.uniform(-2, 2), dark, 1.4)
        for i in range(4):
            leaf(c, 7 + i * 4, cy + rng.uniform(-5, 5), rng.uniform(-0.8, 0.8), main if i % 2 else hi, 1.8)
    elif element == "metal":
        for i in range(4):
            shard(c, 8 + i * 4, cy + rng.uniform(-5, 5), rng.uniform(-0.2, 0.2), 6 + i, hi if i == 0 else main, 1.0)
    elif element == "earth":
        motes(c, rng, 13, (13, cy + 2), 10, main, (0.8, 2.0))
        line(c, 5, cy + 5, 22, cy + 5, dark, 1.0)
    elif element == "lightning":
        bolt(c, 4, cy, 24, cy + rng.uniform(-4, 4), hi, rng, 1.2, 5, 1)
    elif element == "wind":
        for i in range(3):
            ring(c, 13 + i * 3, cy, 5 + i * 2, 1.0, main, 0.28, p + i * 0.8)
    elif element == "neg":
        for i in range(5):
            disc(c, 6 + i * 4, cy + rng.uniform(-5, 5), rng.uniform(1.5, 3.5), main if i % 2 else dark)
    elif element == "pos":
        line(c, 4, cy, 24, cy, hi, 1.0)
        motes(c, rng, 7, (13, cy), 10, main, (0.7, 1.4))
    elif element == "entropy":
        noise_blocks(c, rng, 12, main, (13, cy), 10, 2)
        noise_blocks(c, rng, 5, acc, (13, cy), 9, 2)
    else:
        ring(c, 14, cy, 6 + p * 3, 1.0, main, 0.42, p)
        motes(c, rng, 4, (13, cy), 8, hi, (0.6, 1.2))


def draw_impact(c, element, pal, frame, total, rng):
    p = progress(frame, total)
    cx = c.width / 2
    cy = c.height / 2
    main = color(pal[0], int(220 * (1 - p * 0.25)))
    hi = color(pal[1], int(245 * (1 - p * 0.32)))
    dark = color(pal[2], 175)
    acc = color(pal[3], 190)

    if element == "fire":
        disc(c, cx, cy, 5 + p * 3, hi)
        ring(c, cx, cy, 9 + p * 18, 3.0, main, 0.88, p)
        for i in range(12):
            a = i * math.tau / 12 + rng.uniform(-0.1, 0.1)
            ray(c, cx, cy, a, 4, 13 + p * rng.uniform(13, 25), main if i % 2 else acc, 2.0)
        motes(c, rng, 18, (cx, cy), 24 + p * 5, acc, (0.7, 1.8))
    elif element == "water":
        ring(c, cx, cy, 8 + p * 20, 1.8, main, 1.0, 0)
        for i in range(12):
            a = i * math.tau / 12
            ray(c, cx, cy, a, 6, 12 + p * rng.uniform(12, 22), hi if i % 3 == 0 else main, 1.3)
            disc(c, cx + math.cos(a) * (16 + p * 12), cy + math.sin(a) * (10 + p * 13), 1.5, acc)
    elif element == "wood":
        for i in range(10):
            a = i * math.tau / 10 + rng.uniform(-0.12, 0.12)
            line(c, cx, cy, cx + math.cos(a) * (12 + p * 18), cy + math.sin(a) * (12 + p * 18), dark, 2.0)
            shard(c, cx + math.cos(a) * (14 + p * 12), cy + math.sin(a) * (14 + p * 12), a, 7 + p * 5, main, 1.3)
            leaf(c, cx + math.cos(a) * (18 + p * 10), cy + math.sin(a) * (18 + p * 10), a + 0.4, hi, 2.3)
    elif element == "metal":
        for i in range(8):
            a = i * math.tau / 8 + p * 0.2
            shard(c, cx + math.cos(a) * (5 + p * 8), cy + math.sin(a) * (5 + p * 8), a, 17 + p * 8, hi if i % 2 else main, 2.0)
        line(c, cx - 18, cy - 12, cx + 18, cy + 12, color("#ffffff", 220), 1.5)
        line(c, cx - 14, cy + 15, cx + 16, cy - 14, acc, 1.2)
    elif element == "earth":
        disc(c, cx, cy, 4 + p * 3, dark)
        for i in range(11):
            a = i * math.tau / 11 + rng.uniform(-0.2, 0.2)
            ray(c, cx, cy, a, 4, 13 + p * rng.uniform(12, 24), dark, 1.8)
            shard(c, cx + math.cos(a) * (9 + p * 17), cy + math.sin(a) * (9 + p * 15), a, rng.uniform(5, 9), main, 2.4)
        motes(c, rng, 22, (cx, cy), 28, scale_alpha(acc, 0.55), (0.8, 2.4))
    elif element == "lightning":
        disc(c, cx, cy, 4 + p * 2, hi)
        for i in range(9):
            a = i * math.tau / 9
            bolt(c, cx, cy, cx + math.cos(a) * (17 + p * 18), cy + math.sin(a) * (17 + p * 18), hi if i % 2 else main, rng, 1.5, 8, 1)
        ring(c, cx, cy, 11 + p * 17, 1.0, acc, 0.48, p)
    elif element == "wind":
        for i in range(5):
            ring(c, cx, cy, 7 + i * 4 + p * 8, 1.2, scale_alpha(main, 0.7 - i * 0.08), 0.38, p * 2 + i)
        for i in range(6):
            a = i * math.tau / 6 + p
            ray(c, cx, cy, a, 7, 18 + p * 13, hi, 1.0)
    elif element == "neg":
        disc(c, cx, cy, 8 + p * 4, scale_alpha(dark, 0.8))
        ring(c, cx, cy, 10 + p * 18, 3.0, main, 0.58, p)
        for i in range(7):
            a = i * math.tau / 7 + 0.3
            ray(c, cx, cy, a, 6, 15 + p * 19, acc, 1.6)
    elif element == "pos":
        disc(c, cx, cy, 6 + p * 4, hi)
        ring(c, cx, cy, 10 + p * 20, 1.6, main, 1.0, 0)
        for i in range(16):
            a = i * math.tau / 16
            ray(c, cx, cy, a, 4, 14 + p * (22 if i % 4 == 0 else 14), hi if i % 4 == 0 else main, 1.1)
    elif element == "entropy":
        ring(c, cx, cy, 12 + p * 18, 2.0, main, 0.2, p * 5)
        ring(c, cx, cy, 18 + p * 8, 1.5, acc, 0.18, -p * 7)
        bolt(c, cx - 20, cy - 3, cx + 19, cy + 4, main, rng, 1.2, 12, 2)
        noise_blocks(c, rng, 32, scale_alpha(hi, 0.75), (cx, cy), 28, 4)
        noise_blocks(c, rng, 18, acc, (cx, cy), 25, 3)
    else:
        ring(c, cx, cy, 8 + p * 18, 2.0, main, 0.95, p)
        for i in range(10):
            a = i * math.tau / 10
            ray(c, cx, cy, a, 5, 12 + p * 16, hi if i % 2 else acc, 1.0)


def draw_residual(c, element, pal, frame, total, rng):
    p = progress(frame, total)
    cx = c.width / 2
    cy = c.height / 2
    main = color(pal[0], int(145 * (1 - p * 0.45)))
    hi = color(pal[1], int(165 * (1 - p * 0.5)))
    dark = color(pal[2], int(135 * (1 - p * 0.35)))
    acc = color(pal[3], int(145 * (1 - p * 0.42)))

    if element == "fire":
        motes(c, rng, 18, (cx, cy), 19 + p * 5, main, (0.7, 1.7))
        for i in range(4):
            ray(c, cx, cy + 5, -math.pi / 2 + rng.uniform(-0.4, 0.4), 1, 6 + rng.random() * 9, acc, 1.1)
    elif element == "water":
        for i in range(3):
            ring(c, cx, cy, 7 + i * 6 + p * 4, 1.0, main if i % 2 else hi, 0.82, p + i)
        motes(c, rng, 7, (cx, cy), 19, acc, (0.6, 1.1))
    elif element == "wood":
        for i in range(7):
            a = i * math.tau / 7 + rng.uniform(-0.25, 0.25)
            line(c, cx, cy, cx + math.cos(a) * (8 + p * 6), cy + math.sin(a) * (8 + p * 6), dark, 1.0)
            leaf(c, cx + math.cos(a) * (11 + p * 5), cy + math.sin(a) * (11 + p * 5), a, main, 2.0)
    elif element == "metal":
        for i in range(8):
            a = rng.uniform(0, math.tau)
            shard(c, cx + math.cos(a) * rng.uniform(4, 16), cy + math.sin(a) * rng.uniform(4, 16), a, rng.uniform(4, 8), main if i % 2 else hi, 1.0)
    elif element == "earth":
        for i in range(7):
            a = i * math.tau / 7 + rng.uniform(-0.25, 0.25)
            ray(c, cx, cy, a, 3, 11 + p * 5, dark, 1.0)
        motes(c, rng, 20, (cx, cy + 3), 19, main, (0.7, 2.0))
    elif element == "lightning":
        for i in range(4):
            a = rng.uniform(0, math.tau)
            bolt(c, cx + math.cos(a) * 5, cy + math.sin(a) * 5, cx + math.cos(a) * (14 + p * 5), cy + math.sin(a) * (14 + p * 5), hi if i % 2 else main, rng, 1.0, 5, 0)
    elif element == "wind":
        for i in range(4):
            ring(c, cx, cy, 8 + i * 4 + p * 4, 1.0, main if i % 2 else hi, 0.32, i + p * 2)
    elif element == "neg":
        for i in range(10):
            disc(c, cx + rng.uniform(-17, 17), cy + rng.uniform(-12, 12), rng.uniform(1.2, 3.5), dark if i % 2 else main)
        ring(c, cx, cy, 11 + p * 7, 2.0, acc, 0.36, p)
    elif element == "pos":
        ring(c, cx, cy, 7 + p * 8, 1.0, main, 1.0, 0)
        motes(c, rng, 16, (cx, cy), 20, hi if frame == 0 else main, (0.7, 1.4))
    elif element == "entropy":
        noise_blocks(c, rng, 22, main, (cx, cy), 19, 3)
        noise_blocks(c, rng, 12, acc, (cx, cy), 16, 2)
        ring(c, cx, cy, 10 + p * 8, 1.0, hi, 0.16, p * 5)
    else:
        ring(c, cx, cy, 8 + p * 7, 1.0, main, 0.75, p)
        motes(c, rng, 9, (cx, cy), 17, hi, (0.6, 1.2))


DRAWERS = {
    "cast": draw_cast,
    "muzzle": draw_muzzle,
    "trail": draw_trail,
    "impact": draw_impact,
    "residual": draw_residual,
}


def make_rng(element, phase, frame):
    key = f"{element}:{phase}:{frame}".encode("utf-8")
    seed = int(hashlib.sha256(key).hexdigest()[:16], 16)
    return random.Random(seed)


def generate(root):
    if root.exists():
        shutil.rmtree(root)
    root.mkdir(parents=True, exist_ok=True)

    for element, palette in PALETTES.items():
        for phase, (width, height, count, pivot_x, pivot_y, pixels_per_unit) in PHASES.items():
            folder = root / element / phase
            folder.mkdir(parents=True, exist_ok=True)
            for frame in range(count):
                canvas = Canvas(width, height)
                rng = make_rng(element, phase, frame)
                DRAWERS[phase](canvas, element, palette, frame, count, rng)
                save_png(folder / f"{frame:03}.png", canvas)

            metadata = {
                "Default": {
                    "PixelsPerUnit": pixels_per_unit,
                    "PivotX": pivot_x,
                    "PivotY": pivot_y,
                }
            }
            (folder / "sprites.json").write_text(json.dumps(metadata, indent=4), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        default="GameResources/cultiway/effect_v2",
        help="Output root for generated skill VFX frames.",
    )
    args = parser.parse_args()
    generate(Path(args.root))


if __name__ == "__main__":
    main()
