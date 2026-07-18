#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从运行时 Content 读取表面着色参数，保持 Python 与 C# 预览一致。"""

from __future__ import annotations

import json
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path


DEFAULT_THRESHOLDS = (0.10, 0.28, 0.47, 0.67, 0.87)


@dataclass(frozen=True)
class SurfaceStyle:
    key: str
    diffuse: float = 0.56
    side_shadow: float = 1.0
    brightness: float = 0.0
    emission: float = 0.0
    specular: float = 0.0
    specular_power: float = 14.0
    rim_light: float = 0.0
    pixel_rim: bool = False
    emission_layer: float = 0.0
    shade_thresholds: tuple[float, ...] = DEFAULT_THRESHOLDS
    preserve_detail: bool = False
    pattern: str = ""
    pattern_scale: float = 1.0
    pattern_strength: float = 0.0
    pattern_min_size: int = 28


@lru_cache(maxsize=1)
def load_surface_styles() -> dict[str, SurfaceStyle]:
    root = Path(__file__).resolve().parents[2]
    path = root / "Content" / "Artifacts" / "AppearanceCatalog" / "surfaces.json"
    payload = json.loads(path.read_text(encoding="utf-8"))
    styles: dict[str, SurfaceStyle] = {}
    for raw in payload.get("styles", []):
        thresholds = tuple(float(value) for value in raw.get("shade_thresholds", DEFAULT_THRESHOLDS))
        if len(thresholds) != 5:
            thresholds = DEFAULT_THRESHOLDS
        style = SurfaceStyle(
            key=str(raw["key"]),
            diffuse=float(raw.get("diffuse", 0.56)),
            side_shadow=float(raw.get("side_shadow", 1.0)),
            brightness=float(raw.get("brightness", 0.0)),
            emission=float(raw.get("emission", 0.0)),
            specular=float(raw.get("specular", 0.0)),
            specular_power=float(raw.get("specular_power", 14.0)),
            rim_light=float(raw.get("rim_light", 0.0)),
            pixel_rim=bool(raw.get("pixel_rim", False)),
            emission_layer=float(raw.get("emission_layer", 0.0)),
            shade_thresholds=thresholds,
            preserve_detail=bool(raw.get("preserve_detail", False)),
            pattern=str(raw.get("pattern", "")),
            pattern_scale=float(raw.get("pattern_scale", 1.0)),
            pattern_strength=float(raw.get("pattern_strength", 0.0)),
            pattern_min_size=int(raw.get("pattern_min_size", 28)),
        )
        styles[style.key] = style
    if "neutral" not in styles:
        styles["neutral"] = SurfaceStyle("neutral")
    return styles


def get_surface_style(key: str | None) -> SurfaceStyle:
    styles = load_surface_styles()
    return styles.get(key or "neutral", styles["neutral"])
