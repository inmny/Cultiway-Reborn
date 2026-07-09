#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""轻量 3D 向量和欧拉旋转工具。"""

from __future__ import annotations

import math
from typing import Sequence


Vec3 = tuple[float, float, float]
Vec2 = tuple[float, float]


def v3(value: Sequence[float] | float, default: Vec3 = (0.0, 0.0, 0.0)) -> Vec3:
    if isinstance(value, (int, float)):
        scalar = float(value)
        return scalar, scalar, scalar
    if value is None:
        return default
    if len(value) == 0:
        return default
    if len(value) == 1:
        scalar = float(value[0])
        return scalar, scalar, scalar
    if len(value) == 2:
        return float(value[0]), float(value[1]), default[2]
    return float(value[0]), float(value[1]), float(value[2])


def add(a: Vec3, b: Vec3) -> Vec3:
    return a[0] + b[0], a[1] + b[1], a[2] + b[2]


def sub(a: Vec3, b: Vec3) -> Vec3:
    return a[0] - b[0], a[1] - b[1], a[2] - b[2]


def mul(a: Vec3, b: Vec3 | float) -> Vec3:
    if isinstance(b, (int, float)):
        return a[0] * b, a[1] * b, a[2] * b
    return a[0] * b[0], a[1] * b[1], a[2] * b[2]


def dot(a: Vec3, b: Vec3) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a: Vec3, b: Vec3) -> Vec3:
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def length(a: Vec3) -> float:
    return math.sqrt(dot(a, a))


def normalize(a: Vec3, fallback: Vec3 = (0.0, 0.0, 1.0)) -> Vec3:
    value = length(a)
    if value <= 1e-8:
        return fallback
    return a[0] / value, a[1] / value, a[2] / value


def rotate_euler(point: Vec3, rotation: Sequence[float] | None) -> Vec3:
    """按 X、Y、Z 顺序旋转，角度单位为度。"""
    if not rotation:
        return point
    rx, ry, rz = v3(rotation)
    x, y, z = point

    if rx:
        angle = math.radians(rx)
        c = math.cos(angle)
        s = math.sin(angle)
        y, z = y * c - z * s, y * s + z * c
    if ry:
        angle = math.radians(ry)
        c = math.cos(angle)
        s = math.sin(angle)
        x, z = x * c + z * s, -x * s + z * c
    if rz:
        angle = math.radians(rz)
        c = math.cos(angle)
        s = math.sin(angle)
        x, y = x * c - y * s, x * s + y * c
    return x, y, z


def face_normal(points: Sequence[Vec3]) -> Vec3:
    if len(points) < 3:
        return 0.0, 0.0, 1.0
    return normalize(cross(sub(points[1], points[0]), sub(points[2], points[0])))


def parse_rotation(data: dict) -> Vec3:
    return v3(data.get("rotation", data.get("rot", [0, 0, 0])))


def parse_offset(data: dict) -> Vec3:
    return v3(data.get("offset", data.get("position", [0, 0, 0])))


def parse_scale(data: dict) -> Vec3:
    return v3(data.get("scale", [1, 1, 1]), (1.0, 1.0, 1.0))

