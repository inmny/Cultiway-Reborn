#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""低模几何体生成。"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Sequence

from .math3d import (
    Vec2,
    Vec3,
    add,
    cross,
    mul,
    normalize,
    parse_offset,
    parse_rotation,
    parse_scale,
    rotate_euler,
    sub,
)


@dataclass(frozen=True)
class Face3D:
    points: tuple[Vec3, ...]
    material: str
    surface: str = "neutral"


def part_faces(part: dict) -> list[Face3D]:
    primitive = str(part.get("primitive", part.get("type", "")))
    if primitive == "radial_repeat":
        return radial_repeat_faces(part)
    material = str(part.get("material", "main"))
    if primitive == "box":
        faces = box_faces(part)
    elif primitive == "beveled_box":
        faces = beveled_box_faces(part)
    elif primitive == "poly_prism":
        faces = poly_prism_faces(part)
    elif primitive == "blade":
        faces = blade_faces(part)
    elif primitive in ("cylinder", "frustum"):
        faces = frustum_faces(part)
    elif primitive == "ellipsoid":
        faces = ellipsoid_faces(part)
    elif primitive == "torus":
        faces = torus_faces(part)
    elif primitive == "lathe":
        faces = lathe_faces(part)
    elif primitive == "capsule":
        faces = capsule_faces(part)
    elif primitive == "tube":
        faces = tube_faces(part)
    elif primitive == "cloth_panel":
        faces = cloth_panel_faces(part)
    else:
        raise ValueError(f"不支持的 3D 几何体: {primitive}")
    surface = str(part.get("surface", "neutral"))
    return [Face3D(tuple(transform_local_point(point, part) for point in face.points), material, surface) for face in faces]


def radial_repeat_faces(part: dict) -> list[Face3D]:
    child = part.get("part")
    if not isinstance(child, dict):
        return []
    count = max(1, int(part.get("count", 6)))
    radius = float(part.get("radius", 0.0))
    start_angle = float(part.get("start_angle", 0.0))
    child_faces = part_faces(child)
    faces: list[Face3D] = []
    for repeat in range(count):
        angle = start_angle + 360.0 * repeat / count
        for face in child_faces:
            points = []
            for point in face.points:
                repeated = rotate_euler(add(point, (radius, 0.0, 0.0)), (0.0, angle, 0.0))
                points.append(transform_local_point(repeated, part))
            faces.append(Face3D(tuple(points), face.material, face.surface))
    return faces


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


def beveled_box_faces(part: dict) -> list[Face3D]:
    sx, sy, sz = part_size(part, [1, 1, 1])
    bevel = min(max(float(part.get("bevel", min(sx, sy) * 0.16)), 0.0), min(sx, sy) * 0.49)
    x = sx / 2
    y = sy / 2
    outline = [
        (-x + bevel, -y), (x - bevel, -y), (x, -y + bevel), (x, y - bevel),
        (x - bevel, y), (-x + bevel, y), (-x, y - bevel), (-x, -y + bevel),
    ]
    return poly_prism_from_outline(part, outline, sz)


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


def torus_faces(part: dict) -> list[Face3D]:
    major_radius = float(part.get("major_radius", 0.5))
    minor_radius = float(part.get("minor_radius", 0.1))
    segments = max(6, int(part.get("segments", 16)))
    tube_segments = max(4, int(part.get("tube_segments", 6)))
    rings: list[list[Vec3]] = []
    for i in range(segments):
        angle = math.tau * i / segments
        ca = math.cos(angle)
        sa = math.sin(angle)
        ring: list[Vec3] = []
        for j in range(tube_segments):
            tube_angle = math.tau * j / tube_segments
            radial = major_radius + math.cos(tube_angle) * minor_radius
            ring.append((ca * radial, math.sin(tube_angle) * minor_radius, sa * radial))
        rings.append(ring)
    faces: list[list[Vec3]] = []
    for i in range(segments):
        next_i = (i + 1) % segments
        for j in range(tube_segments):
            next_j = (j + 1) % tube_segments
            faces.append([rings[i][j], rings[next_i][j], rings[next_i][next_j], rings[i][next_j]])
    return material_faces(part, faces)


def lathe_faces(part: dict) -> list[Face3D]:
    profile = parse_points2(part.get("profile", []))
    return lathe_from_profile(
        part,
        profile,
        max(6, int(part.get("segments", 14))),
        bool(part.get("cap_top", True)),
        bool(part.get("cap_bottom", True)),
    )


def capsule_faces(part: dict) -> list[Face3D]:
    radius = float(part.get("radius", 0.3))
    height = max(radius * 2, float(part.get("height", 1.2)))
    cap_rings = max(2, int(part.get("rings", 4)))
    cylinder_half = height / 2 - radius
    profile: list[Vec2] = []
    for i in range(cap_rings + 1):
        angle = -math.pi / 2 + math.pi / 2 * i / cap_rings
        profile.append((math.cos(angle) * radius, -cylinder_half + math.sin(angle) * radius))
    for i in range(1, cap_rings + 1):
        angle = math.pi / 2 * i / cap_rings
        profile.append((math.cos(angle) * radius, cylinder_half + math.sin(angle) * radius))
    return lathe_from_profile(part, profile, max(6, int(part.get("segments", 12))), False, False)


def lathe_from_profile(
    part: dict,
    profile: Sequence[Vec2],
    segments: int,
    cap_top: bool,
    cap_bottom: bool,
) -> list[Face3D]:
    if len(profile) < 2:
        return []
    rings: list[list[Vec3]] = []
    for radius, y in profile:
        rings.append([
            (math.cos(math.tau * i / segments) * radius, y, math.sin(math.tau * i / segments) * radius)
            for i in range(segments)
        ])
    faces: list[list[Vec3]] = []
    for row in range(len(profile) - 1):
        for i in range(segments):
            next_i = (i + 1) % segments
            faces.append([rings[row][i], rings[row][next_i], rings[row + 1][next_i], rings[row + 1][i]])
    if cap_bottom and profile[0][0] > 0:
        faces.append(rings[0])
    if cap_top and profile[-1][0] > 0:
        faces.append(list(reversed(rings[-1])))
    return material_faces(part, faces)


def tube_faces(part: dict) -> list[Face3D]:
    path = parse_points3(part.get("points", []))
    if len(path) < 2:
        return []
    radius = float(part.get("radius", 0.08))
    segments = max(4, int(part.get("segments", 7)))
    rings: list[list[Vec3]] = []
    for row, point in enumerate(path):
        if row == 0:
            tangent = sub(path[1], path[0])
        elif row == len(path) - 1:
            tangent = sub(path[row], path[row - 1])
        else:
            tangent = sub(path[row + 1], path[row - 1])
        tangent = normalize(tangent, (0.0, 1.0, 0.0))
        normal = cross(tangent, (0.0, 0.0, 1.0))
        if sum(value * value for value in normal) < 0.0001:
            normal = cross(tangent, (1.0, 0.0, 0.0))
        normal = normalize(normal)
        binormal = normalize(cross(tangent, normal))
        ring: list[Vec3] = []
        for i in range(segments):
            angle = math.tau * i / segments
            radial = add(mul(normal, math.cos(angle)), mul(binormal, math.sin(angle)))
            ring.append(add(point, mul(radial, radius)))
        rings.append(ring)
    faces: list[list[Vec3]] = []
    for row in range(len(path) - 1):
        for i in range(segments):
            next_i = (i + 1) % segments
            faces.append([rings[row][i], rings[row][next_i], rings[row + 1][next_i], rings[row + 1][i]])
    if bool(part.get("cap_start", True)):
        faces.append(list(reversed(rings[0])))
    if bool(part.get("cap_end", True)):
        faces.append(rings[-1])
    return material_faces(part, faces)


def cloth_panel_faces(part: dict) -> list[Face3D]:
    width = float(part.get("width", 1.0))
    height = float(part.get("height", 1.4))
    depth = float(part.get("depth", 0.035))
    flare = float(part.get("flare", 0.15))
    curve = float(part.get("curve", 0.08))
    wave = float(part.get("wave", 0.04))
    columns = max(2, int(part.get("segments_x", 5)))
    rows = max(2, int(part.get("segments_y", 7)))
    front: list[list[Vec3]] = []
    back: list[list[Vec3]] = []
    for y_index in range(rows + 1):
        v = y_index / rows
        half_width = width / 2 * (1 + flare * v)
        front_row: list[Vec3] = []
        back_row: list[Vec3] = []
        for x_index in range(columns + 1):
            u = x_index / columns * 2 - 1
            x = u * half_width
            y = height * (0.5 - v)
            z = curve * u * u + math.sin((u + v) * math.tau) * wave
            front_row.append((x, y, z + depth / 2))
            back_row.append((x, y, z - depth / 2))
        front.append(front_row)
        back.append(back_row)
    faces: list[list[Vec3]] = []
    for y_index in range(rows):
        for x_index in range(columns):
            faces.append([
                front[y_index][x_index], front[y_index][x_index + 1],
                front[y_index + 1][x_index + 1], front[y_index + 1][x_index],
            ])
            faces.append([
                back[y_index][x_index], back[y_index + 1][x_index],
                back[y_index + 1][x_index + 1], back[y_index][x_index + 1],
            ])
    for x_index in range(columns):
        faces.append([front[0][x_index], back[0][x_index], back[0][x_index + 1], front[0][x_index + 1]])
        faces.append([front[rows][x_index], front[rows][x_index + 1], back[rows][x_index + 1], back[rows][x_index]])
    for y_index in range(rows):
        faces.append([front[y_index][0], front[y_index + 1][0], back[y_index + 1][0], back[y_index][0]])
        faces.append([
            front[y_index][columns], back[y_index][columns],
            back[y_index + 1][columns], front[y_index + 1][columns],
        ])
    return material_faces(part, faces)


def poly_prism_from_outline(part: dict, outline: Sequence[Vec2], depth: float) -> list[Face3D]:
    front = [(x, y, depth / 2) for x, y in outline]
    back = [(x, y, -depth / 2) for x, y in outline]
    faces: list[list[Vec3]] = [front, list(reversed(back))]
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        faces.append([front[i], front[j], back[j], back[i]])
    return material_faces(part, faces)


def material_faces(part: dict, faces: Sequence[Sequence[Vec3]]) -> list[Face3D]:
    material = str(part.get("material", "main"))
    surface = str(part.get("surface", "neutral"))
    return [Face3D(tuple(face), material, surface) for face in faces if len(face) >= 3]


def part_size(part: dict, default: Sequence[float]) -> Vec3:
    value = part.get("size", default)
    if isinstance(value, (int, float)):
        scalar = float(value)
        return scalar, scalar, scalar
    return float(value[0]), float(value[1]), float(value[2])


def parse_points2(points: Sequence[Sequence[float]]) -> list[Vec2]:
    return [(float(point[0]), float(point[1])) for point in points]


def parse_points3(points: Sequence[Sequence[float]]) -> list[Vec3]:
    return [(float(point[0]), float(point[1]), float(point[2])) for point in points if len(point) >= 3]


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

