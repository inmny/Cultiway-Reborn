#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""实例级调色板：所有模块共享色阶，避免组合后各自成套。"""

from __future__ import annotations

from dataclasses import dataclass


Rgb = tuple[int, int, int]
Ramp = tuple[Rgb, Rgb, Rgb, Rgb, Rgb, Rgb]


@dataclass(frozen=True)
class PaletteTheme:
    key: str
    ramps: dict[str, Ramp]
    outline_dark: Rgb
    outline_soft: Rgb
    shadow: Rgb

    def ramp_for(self, material: str) -> Ramp:
        role = MATERIAL_ALIASES.get(material, material)
        return self.ramps.get(role, self.ramps["metal"])

    def emission_for(self, material: str) -> int:
        role = MATERIAL_ALIASES.get(material, material)
        if role == "glow":
            return 3
        return 0


MATERIAL_ALIASES = {
    "blade": "metal",
    "edge": "trim",
    "frame": "trim",
    "bronze": "metal",
    "rim": "trim",
    "ridge": "trim",
    "main": "metal",
    "left": "metal",
    "right": "metal",
    "top": "trim",
    "surface": "crystal",
    "gem": "crystal",
    "core": "glow",
    "glint": "glow",
    "fire": "glow",
    "water": "crystal",
    "wood": "grip",
    "handle": "grip",
    "pommel": "trim",
    "wrap": "cloth",
    "charm": "cloth",
    "fold": "cloth",
    "leg": "metal",
}


def _ramp(*colors: str) -> Ramp:
    if len(colors) != 6:
        raise ValueError("材质色阶必须恰好包含六级")
    return tuple(_hex(value) for value in colors)  # type: ignore[return-value]


def _hex(value: str) -> Rgb:
    value = value.removeprefix("#")
    if len(value) != 6:
        raise ValueError(f"非法颜色: {value}")
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)


THEMES: dict[str, PaletteTheme] = {
    "celestial_jade": PaletteTheme(
        key="celestial_jade",
        ramps={
            "metal": _ramp("152c38", "214b59", "34747c", "58a39d", "8bd0b8", "d9f4d1"),
            "trim": _ramp("4a3017", "76501d", "aa792d", "d4a746", "f3d578", "fff2b5"),
            "jade": _ramp("103a34", "146155", "1f8870", "3eb28b", "77d4a8", "c7f1cf"),
            "crystal": _ramp("17354b", "1d6073", "298da0", "51bdc1", "91e2d2", "e5fff0"),
            "grip": _ramp("211c1b", "3a2b28", "594137", "7c5d48", "aa8664", "d8bc8e"),
            "cloth": _ramp("381737", "61204f", "8b2e64", "bb4778", "df7798", "f7b4bf"),
            "glow": _ramp("174943", "19796a", "26a98a", "4bd3a4", "91efc4", "efffdd"),
            "dark": _ramp("11161b", "1a252b", "26383e", "385056", "557278", "83a0a2"),
        },
        outline_dark=_hex("10191a"),
        outline_soft=_hex("29433f"),
        shadow=_hex("111819"),
    ),
    "moon_silver": PaletteTheme(
        key="moon_silver",
        ramps={
            "metal": _ramp("1c2435", "303c55", "4f5e78", "7888a0", "acb9c8", "edf4f4"),
            "trim": _ramp("3b2b24", "65462f", "966b3c", "c29353", "e6c477", "fff0ad"),
            "jade": _ramp("183646", "21596a", "337f8b", "55a8aa", "88cfc4", "d8eee1"),
            "crystal": _ramp("142b52", "1d4d80", "2b76ae", "54a6d0", "91d5e8", "e4fbff"),
            "grip": _ramp("211c22", "382d35", "55424a", "795e63", "a28382", "d2b4aa"),
            "cloth": _ramp("301a48", "513068", "754788", "9d67aa", "c58bca", "e9c4ea"),
            "glow": _ramp("16325b", "1e5a92", "2b88c4", "54b8e0", "94e1f2", "edffff"),
            "dark": _ramp("11151f", "1d2533", "2b3849", "43536a", "66778c", "9aa9b8"),
        },
        outline_dark=_hex("0d1320"),
        outline_soft=_hex("2b3549"),
        shadow=_hex("10141d"),
    ),
    "copper_ember": PaletteTheme(
        key="copper_ember",
        ramps={
            "metal": _ramp("281c19", "4c2e24", "75452e", "a7613b", "d48b52", "f4bd78"),
            "trim": _ramp("3c2918", "66431d", "946329", "c18b3b", "e7b95b", "ffe49a"),
            "jade": _ramp("183d34", "22614d", "33846a", "55aa83", "83c9a0", "c8e6c3"),
            "crystal": _ramp("4a241c", "783526", "a94b2f", "d86b3d", "ef9a5b", "ffd798"),
            "grip": _ramp("211b18", "382a23", "574033", "795b45", "a17d5d", "d0aa80"),
            "cloth": _ramp("391b22", "642a31", "903b3c", "bb5450", "df7b68", "f5b19a"),
            "glow": _ramp("4d2015", "82301b", "b74720", "e46629", "f69a45", "ffd47e"),
            "dark": _ramp("171513", "28221d", "3d3127", "574536", "755e47", "9d8060"),
        },
        outline_dark=_hex("17100e"),
        outline_soft=_hex("3b2820"),
        shadow=_hex("181210"),
    ),
}


def get_theme(key: str) -> PaletteTheme:
    try:
        return THEMES[key]
    except KeyError as exc:
        raise KeyError(f"不存在的原型调色板: {key}") from exc
