#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""颜色解析与像素化后的调色板处理。"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Mapping


@dataclass(frozen=True)
class Rgba:
    r: int
    g: int
    b: int
    a: int = 255

    def with_alpha(self, opacity: float) -> "Rgba":
        return Rgba(self.r, self.g, self.b, max(0, min(255, round(self.a * opacity))))

    def tuple(self) -> tuple[int, int, int, int]:
        return self.r, self.g, self.b, self.a


def parse_hex_color(value: str) -> Rgba:
    text = value.strip()
    if not text.startswith("#"):
        raise ValueError(f"颜色必须是 #RRGGBB 或 #RRGGBBAA: {value}")
    raw = text[1:]
    if len(raw) == 3:
        raw = "".join(ch * 2 for ch in raw)
    if len(raw) not in (6, 8):
        raise ValueError(f"颜色长度不正确: {value}")
    r = int(raw[0:2], 16)
    g = int(raw[2:4], 16)
    b = int(raw[4:6], 16)
    a = int(raw[6:8], 16) if len(raw) == 8 else 255
    return Rgba(r, g, b, a)


def resolve_color(value: str | None, palette: Mapping[str, str], opacity: float = 1.0) -> Rgba | None:
    if value is None:
        return None
    text = str(value).strip()
    if not text or text == "none":
        return None
    if text.startswith("#"):
        return parse_hex_color(text).with_alpha(opacity)
    if text not in palette:
        raise KeyError(f"调色板缺少颜色键: {text}")
    return parse_hex_color(palette[text]).with_alpha(opacity)

