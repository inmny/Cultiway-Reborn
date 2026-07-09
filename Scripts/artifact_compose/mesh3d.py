#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""低模几何体生成。"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Sequence

from .math3d import Vec2, Vec3, add, mul, parse_offset, parse_rotation, parse_scale, rotate_euler


@dataclass(frozen=True)
class Face3D:
    points: tuple[Vec3, ...]
    material: str


def part_faces(part: dict) -> list[Face3D]:
    primitive = str(part.get("primitive", part.get("type", "")))
    material = str(part.get("material", "main"))
    if primitive == "box":
        faces = box_faces(part)
    elif primitive == "poly_prism":
        faces = poly_prism_faces(part)
    elif primitive == "blade":
        faces = blade_faces(part)
    elif primitive in ("cylinder", "frustum"):
        faces = frustum_faces(part)
    elif primitive == "ellipsoid":
        faces = ellipsoid_faces(part)
    else:
        raise ValueError(f"不支持的 3D 几何体: {primitive}")
    return [Face3D(tuple(transform_local_point(point, part) for point in face.points), material) for face in faces]


def transform_local_point(point: Vec3, part: dict) -> Vec3:
    value = mul(point, parse_scale(part))
    value = rotate_euler(value, parse_rotation(part))
    return add(value, parse_offset(part))


def box_faces(part: dict) -> list[Face3D]:
    sx, sy, sz = part_size(part, [1, 1, 1])
    x = sx / 2
    y = sy / 2
    z = sz / 2
    v = [
        (-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z),
        (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z),
    ]
    return material_faces(part, [
        [v[4], v[5], v[6], v[7]],
        [v[1], v[0], v[3], v[2]],
        [v[0], v[4], v[7], v[3]],
        [v[5], v[1], v[2], v[6]],
        [v[3], v[7], v[6], v[2]],
        [v[0], v[1], v[5], v[4]],
    ])


def poly_prism_faces(part: dict) -> list[Face3D]:
    points = parse_points2(part.get("points", []))
    depth = float(part.get("depth", 0.12))
    if len(points) < 3:
        return []
    front = [(x, y, depth / 2) for x, y in points]
    back = [(x, y, -depth / 2) for x, y in points]
    faces: list[list[Vec3]] = [front, list(reversed(back))]
    for i in range(len(points)):
        j = (i + 1) % len(points)
        faces.append([front[i], front[j], back[j], back[i]])
    return material_faces(part, faces)


def blade_faces(part: dict) -> list[Face3D]:
    length = float(part.get("length", 2.4))
    width = float(part.get("width", 0.32))
    depth = float(part.get("depth", 0.08))
    shoulder = float(part.get("shoulder", 0.13))
    base = float(part.get("base", width * 0.55))
    outline = [
        (0.0, length),
        (width / 2, shoulder),
        (base / 2, 0.0),
        (-base / 2, 0.0),
        (-width / 2, shoulder),
    ]
    front = [(x, y, depth / 2) for x, y in outline]
    back = [(x, y, -depth / 2) for x, y in outline]
    faces: list[list[Vec3]] = [front, list(reversed(back))]
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        faces.append([front[i], front[j], back[j], back[i]])
    ridge_depth = depth * 0.58
    ridge = [
        (0, length * 0.94, ridge_depth),
        (width * 0.16, shoulder, depth / 2),
        (0, 0.02, ridge_depth),
        (-width * 0.16, shoulder, depth / 2),
    ]
    faces.append(ridge)
    return material_faces(part, faces)


def frustum_faces(part: dict) -> list[Face3D]:
    height = float(part.get("height", 1.0))
    segments = max(5, int(part.get("segments", 12)))
    top_rx, top_rz = radius_pair(part.get("top_radius", part.get("radius", [0.5, 0.5])))
    bottom_rx, bottom_rz = radius_pair(part.get("bottom_radius", part.get("radius", [0.5, 0.5])))
    top_y = height / 2
    bottom_y = -height / 2
    top: list[Vec3] = []
    bottom: list[Vec3] = []
    for i in range(segments):
        angle = math.tau * i / segments
        ca = math.cos(angle)
        sa = math.sin(angle)
        top.append((ca * top_rx, top_y, sa * top_rz))
        bottom.append((ca * bottom_rx, bottom_y, sa * bottom_rz))
    faces: list[list[Vec3]] = []
    for i in range(segments):
        j = (i + 1) % segments
        faces.append([bottom[i], bottom[j], top[j], top[i]])
    if bool(part.get("cap_top", True)):
        faces.append(list(reversed(top)))
    if bool(part.get("cap_bottom", True)):
        faces.append(bottom)
    return material_faces(part, faces)


def ellipsoid_faces(part: dict) -> list[Face3D]:
    rx, ry, rz = radius3(part.get("radius", [0.5, 0.5, 0.5]))
    segments = max(6, int(part.get("segments", 10)))
    rings = max(3, int(part.get("rings", 5)))
    rows: list[list[Vec3]] = []
    for ring in range(rings + 1):
        phi = -math.pi / 2 + math.pi * ring / rings
        y = math.sin(phi) * ry
        r = math.cos(phi)
        row: list[Vec3] = []
        for i in range(segments):
            angle = math.tau * i / segments
            row.append((math.cos(angle) * r * rx, y, math.sin(angle) * r * rz))
        rows.append(row)
    faces: list[list[Vec3]] = []
    for ring in range(rings):
        for i in range(segments):
            j = (i + 1) % segments
            faces.append([rows[ring][i], rows[ring][j], rows[ring + 1][j], rows[ring + 1][i]])
    return material_faces(part, faces)


def material_faces(part: dict, faces: Sequence[Sequence[Vec3]]) -> list[Face3D]:
    material = str(part.get("material", "main"))
    return [Face3D(tuple(face), material) for face in faces if len(face) >= 3]


def part_size(part: dict, default: Sequence[float]) -> Vec3:
    value = part.get("size", default)
    if isinstance(value, (int, float)):
        scalar = float(value)
        return scalar, scalar, scalar
    return float(value[0]), float(value[1]), float(value[2])


def parse_points2(points: Sequence[Sequence[float]]) -> list[Vec2]:
    return [(float(point[0]), float(point[1])) for point in points]


def radius_pair(value) -> tuple[float, float]:
    if isinstance(value, (int, float)):
        radius = float(value)
        return radius, radius
    if len(value) == 1:
        radius = float(value[0])
        return radius, radius
    return float(value[0]), float(value[1])


def radius3(value) -> Vec3:
    if isinstance(value, (int, float)):
        radius = float(value)
        return radius, radius, radius
    if len(value) == 1:
        radius = float(value[0])
        return radius, radius, radius
    if len(value) == 2:
        return float(value[0]), float(value[1]), float(value[0])
    return float(value[0]), float(value[1]), float(value[2])

