#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""二维锚点变换与矢量基础形状展开。"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Iterable, Sequence


Point = tuple[float, float]


@dataclass(frozen=True)
class AnchorTransform:
    anchor_x: float
    anchor_y: float
    anchor_angle: float
    target_x: float
    target_y: float
    target_angle: float
    scale_x: float = 1.0
    scale_y: float = 1.0

    def apply(self, point: Sequence[float]) -> Point:
        x = (float(point[0]) - self.anchor_x) * self.scale_x
        y = (float(point[1]) - self.anchor_y) * self.scale_y
        angle = math.radians(self.target_angle - self.anchor_angle)
        cos_v = math.cos(angle)
        sin_v = math.sin(angle)
        return (
            self.target_x + x * cos_v - y * sin_v,
            self.target_y + x * sin_v + y * cos_v,
        )

    @property
    def average_scale(self) -> float:
        return (abs(self.scale_x) + abs(self.scale_y)) * 0.5


def identity_transform() -> AnchorTransform:
    return AnchorTransform(0, 0, 0, 0, 0, 0, 1, 1)


def ellipse_points(cx: float, cy: float, rx: float, ry: float, steps: int = 28) -> list[Point]:
    result: list[Point] = []
    for i in range(steps):
        angle = math.tau * i / steps
        result.append((cx + math.cos(angle) * rx, cy + math.sin(angle) * ry))
    return result


def rect_points(x: float, y: float, width: float, height: float) -> list[Point]:
    return [(x, y), (x + width, y), (x + width, y + height), (x, y + height)]


def primitive_points(primitive: dict, transform: AnchorTransform) -> list[Point]:
    kind = primitive.get("type")
    if kind == "polygon":
        raw_points = primitive.get("points") or []
    elif kind == "rect":
        raw_points = rect_points(
            float(primitive.get("x", 0)),
            float(primitive.get("y", 0)),
            float(primitive.get("width", primitive.get("w", 0))),
            float(primitive.get("height", primitive.get("h", 0))),
        )
    elif kind == "ellipse":
        raw_points = ellipse_points(
            float(primitive.get("cx", 0)),
            float(primitive.get("cy", 0)),
            float(primitive.get("rx", 0)),
            float(primitive.get("ry", 0)),
            int(primitive.get("steps", 28)),
        )
    elif kind == "line":
        raw_points = primitive.get("points") or []
    else:
        raise ValueError(f"不支持的矢量形状类型: {kind}")
    return [transform.apply(point) for point in raw_points]


def scale_points(points: Iterable[Point], scale: int) -> list[tuple[int, int]]:
    return [(round(x * scale), round(y * scale)) for x, y in points]

