#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""适合 28/56 像素正交渲染的低模几何构建器。"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Iterable, Sequence

from Scripts.artifact_compose.math3d import Vec2, Vec3, add, rotate_euler


@dataclass(frozen=True)
class StyledFace:
    points: tuple[Vec3, ...]
    material: str
    surface: str
    object_name: str


class ModelBuilder:
    def __init__(self) -> None:
        self.faces: list[StyledFace] = []

    def extend(
        self,
        source: "ModelBuilder",
        *,
        rotation: Vec3 = (0.0, 0.0, 0.0),
        offset: Vec3 = (0.0, 0.0, 0.0),
        scale: Vec3 = (1.0, 1.0, 1.0),
        prefix: str = "",
    ) -> None:
        for face in source.faces:
            points = tuple(
                add(
                    rotate_euler(
                        (point[0] * scale[0], point[1] * scale[1], point[2] * scale[2]),
                        rotation,
                    ),
                    offset,
                )
                for point in face.points
            )
            name = f"{prefix}{face.object_name}" if prefix else face.object_name
            self.faces.append(StyledFace(points, face.material, face.surface, name))

    def face(
        self,
        name: str,
        material: str,
        surface: str,
        points: Sequence[Vec3],
    ) -> None:
        if len(points) >= 3:
            self.faces.append(StyledFace(tuple(points), material, surface, name))

    def extrude(
        self,
        name: str,
        material: str,
        surface: str,
        outline: Sequence[Vec2],
        depth: float,
        offset: Vec3 = (0.0, 0.0, 0.0),
        *,
        side_material: str | None = None,
        side_surface: str | None = None,
    ) -> None:
        if len(outline) < 3:
            return
        front = [add((x, y, depth / 2), offset) for x, y in outline]
        back = [add((x, y, -depth / 2), offset) for x, y in outline]
        for a, b, c in _triangulate_outline(outline):
            self.face(name, material, surface, (front[a], front[b], front[c]))
            self.face(name, material, surface, (back[c], back[b], back[a]))
        side_material = side_material or material
        side_surface = side_surface or surface
        for index in range(len(outline)):
            following = (index + 1) % len(outline)
            self.face(
                f"{name}_edge",
                side_material,
                side_surface,
                (front[index], front[following], back[following], back[index]),
            )

    def box(
        self,
        name: str,
        material: str,
        surface: str,
        size: Vec3,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        x, y, z = size[0] / 2, size[1] / 2, size[2] / 2
        vertices = [
            add(point, offset)
            for point in (
                (-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z),
                (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z),
            )
        ]
        for indices in (
            (4, 5, 6, 7), (1, 0, 3, 2), (0, 4, 7, 3),
            (5, 1, 2, 6), (3, 7, 6, 2), (0, 1, 5, 4),
        ):
            self.face(name, material, surface, tuple(vertices[index] for index in indices))

    def beveled_box(
        self,
        name: str,
        material: str,
        surface: str,
        size: Vec3,
        bevel: float,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        x, y = size[0] / 2, size[1] / 2
        bevel = min(max(bevel, 0.0), min(x, y) * 0.92)
        outline = (
            (-x + bevel, -y), (x - bevel, -y), (x, -y + bevel), (x, y - bevel),
            (x - bevel, y), (-x + bevel, y), (-x, y - bevel), (-x, -y + bevel),
        )
        self.extrude(name, material, surface, outline, size[2], offset)

    def disc(
        self,
        name: str,
        material: str,
        surface: str,
        radius_x: float,
        radius_y: float,
        depth: float,
        segments: int = 12,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        outline = tuple(
            (
                math.cos(math.tau * index / segments) * radius_x,
                math.sin(math.tau * index / segments) * radius_y,
            )
            for index in range(max(6, segments))
        )
        self.extrude(name, material, surface, outline, depth, offset)

    def faceted_disc(
        self,
        name: str,
        material: str,
        surface: str,
        radius_x: float,
        radius_y: float,
        depth: float,
        relief: float,
        segments: int = 12,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        front_z = offset[2] + depth / 2
        back_z = offset[2] - depth / 2
        center = (offset[0], offset[1], front_z + relief)
        front = [
            (
                offset[0] + math.cos(math.tau * index / segments) * radius_x,
                offset[1] + math.sin(math.tau * index / segments) * radius_y,
                front_z,
            )
            for index in range(max(6, segments))
        ]
        back = [(x, y, back_z) for x, y, _ in front]
        for index in range(len(front)):
            following = (index + 1) % len(front)
            self.face(name, material, surface, (center, front[index], front[following]))
            self.face(name, material, surface, (back[following], back[index], front[index], front[following]))
        self.face(name, material, surface, tuple(reversed(back)))

    def ring(
        self,
        name: str,
        material: str,
        surface: str,
        outer: Vec2,
        inner: Vec2,
        depth: float,
        segments: int = 12,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        segments = max(6, segments)
        front_z = offset[2] + depth / 2
        back_z = offset[2] - depth / 2
        outer_front: list[Vec3] = []
        inner_front: list[Vec3] = []
        outer_back: list[Vec3] = []
        inner_back: list[Vec3] = []
        for index in range(segments):
            angle = math.tau * index / segments
            cosine, sine = math.cos(angle), math.sin(angle)
            outer_front.append((offset[0] + cosine * outer[0], offset[1] + sine * outer[1], front_z))
            inner_front.append((offset[0] + cosine * inner[0], offset[1] + sine * inner[1], front_z))
            outer_back.append((offset[0] + cosine * outer[0], offset[1] + sine * outer[1], back_z))
            inner_back.append((offset[0] + cosine * inner[0], offset[1] + sine * inner[1], back_z))
        for index in range(segments):
            following = (index + 1) % segments
            for points in (
                (outer_front[index], outer_front[following], inner_front[following], inner_front[index]),
                (outer_back[following], outer_back[index], inner_back[index], inner_back[following]),
                (outer_front[index], outer_back[index], outer_back[following], outer_front[following]),
                (inner_front[following], inner_back[following], inner_back[index], inner_front[index]),
            ):
                self.face(name, material, surface, points)

    def lathe(
        self,
        name: str,
        material: str,
        surface: str,
        profile: Sequence[Vec2],
        segments: int = 12,
        offset: Vec3 = (0.0, 0.0, 0.0),
        *,
        cap_bottom: bool = True,
        cap_top: bool = True,
    ) -> None:
        segments = max(6, segments)
        rings: list[list[Vec3]] = []
        for radius, y in profile:
            rings.append([
                (
                    offset[0] + math.cos(math.tau * index / segments) * radius,
                    offset[1] + y,
                    offset[2] + math.sin(math.tau * index / segments) * radius,
                )
                for index in range(segments)
            ])
        for row in range(len(rings) - 1):
            for index in range(segments):
                following = (index + 1) % segments
                self.face(
                    name,
                    material,
                    surface,
                    (rings[row][index], rings[row][following], rings[row + 1][following], rings[row + 1][index]),
                )
        if cap_bottom and profile and profile[0][0] > 0:
            self.face(name, material, surface, tuple(reversed(rings[0])))
        if cap_top and profile and profile[-1][0] > 0:
            self.face(name, material, surface, tuple(rings[-1]))

    def frustum(
        self,
        name: str,
        material: str,
        surface: str,
        bottom_radius: Vec2,
        top_radius: Vec2,
        height: float,
        segments: int = 8,
        offset: Vec3 = (0.0, 0.0, 0.0),
        *,
        start_angle: float = 0.0,
        cap_bottom: bool = True,
        cap_top: bool = True,
    ) -> None:
        """构建沿 Y 轴收分的四边或多边台体。"""
        segments = max(3, segments)
        bottom: list[Vec3] = []
        top: list[Vec3] = []
        for index in range(segments):
            angle = math.radians(start_angle) + math.tau * index / segments
            cosine, sine = math.cos(angle), math.sin(angle)
            bottom.append((
                offset[0] + cosine * bottom_radius[0],
                offset[1] - height * 0.5,
                offset[2] + sine * bottom_radius[1],
            ))
            top.append((
                offset[0] + cosine * top_radius[0],
                offset[1] + height * 0.5,
                offset[2] + sine * top_radius[1],
            ))
        for index in range(segments):
            following = (index + 1) % segments
            self.face(name, material, surface, (bottom[index], bottom[following], top[following], top[index]))
        if cap_bottom:
            self.face(name, material, surface, tuple(reversed(bottom)))
        if cap_top:
            self.face(name, material, surface, tuple(top))

    def open_lathe(
        self,
        name: str,
        material: str,
        surface: str,
        profile: Sequence[Vec2],
        thickness: float,
        inner_depth: float,
        segments: int = 12,
        offset: Vec3 = (0.0, 0.0, 0.0),
        *,
        rim_material: str | None = None,
        rim_surface: str | None = None,
        inner_material: str | None = None,
        inner_surface: str | None = None,
    ) -> None:
        """构建带口沿、内壁与炉膛底面的空心旋转体。"""
        if len(profile) < 2:
            return
        segments = max(6, segments)
        rim_material = rim_material or material
        rim_surface = rim_surface or surface
        inner_material = inner_material or material
        inner_surface = inner_surface or surface

        outer_rings: list[list[Vec3]] = []
        for radius, y in profile:
            outer_rings.append([
                (
                    offset[0] + math.cos(math.tau * index / segments) * radius,
                    offset[1] + y,
                    offset[2] + math.sin(math.tau * index / segments) * radius,
                )
                for index in range(segments)
            ])
        for row in range(len(outer_rings) - 1):
            for index in range(segments):
                following = (index + 1) % segments
                self.face(
                    name,
                    material,
                    surface,
                    (
                        outer_rings[row][index],
                        outer_rings[row][following],
                        outer_rings[row + 1][following],
                        outer_rings[row + 1][index],
                    ),
                )
        self.face(name, material, surface, tuple(reversed(outer_rings[0])))

        outer_radius, top_y = profile[-1]
        inner_radius = max(0.02, outer_radius - thickness)
        inner_bottom_radius = max(0.02, inner_radius * 0.72)
        inner_top = []
        inner_bottom = []
        for index in range(segments):
            angle = math.tau * index / segments
            cosine, sine = math.cos(angle), math.sin(angle)
            inner_top.append((
                offset[0] + cosine * inner_radius,
                offset[1] + top_y - thickness * 0.18,
                offset[2] + sine * inner_radius,
            ))
            inner_bottom.append((
                offset[0] + cosine * inner_bottom_radius,
                offset[1] + top_y - inner_depth,
                offset[2] + sine * inner_bottom_radius,
            ))
        outer_top = outer_rings[-1]
        for index in range(segments):
            following = (index + 1) % segments
            self.face(
                f"{name}_rim",
                rim_material,
                rim_surface,
                (outer_top[index], outer_top[following], inner_top[following], inner_top[index]),
            )
            self.face(
                f"{name}_inner",
                inner_material,
                inner_surface,
                (inner_top[index], inner_top[following], inner_bottom[following], inner_bottom[index]),
            )
        self.face(
            f"{name}_hearth",
            inner_material,
            inner_surface,
            tuple(reversed(inner_bottom)),
        )

    def ellipsoid(
        self,
        name: str,
        material: str,
        surface: str,
        radius: Vec3,
        segments: int = 12,
        rings: int = 6,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        rows: list[list[Vec3]] = []
        for row in range(1, rings):
            phi = -math.pi / 2 + math.pi * row / rings
            y = offset[1] + math.sin(phi) * radius[1]
            radial = math.cos(phi)
            rows.append([
                (
                    offset[0] + math.cos(math.tau * index / segments) * radial * radius[0],
                    y,
                    offset[2] + math.sin(math.tau * index / segments) * radial * radius[2],
                )
                for index in range(segments)
            ])
        bottom = (offset[0], offset[1] - radius[1], offset[2])
        top = (offset[0], offset[1] + radius[1], offset[2])
        for index in range(segments):
            following = (index + 1) % segments
            self.face(name, material, surface, (bottom, rows[0][following], rows[0][index]))
        for row in range(len(rows) - 1):
            for index in range(segments):
                following = (index + 1) % segments
                self.face(
                    name,
                    material,
                    surface,
                    (rows[row][index], rows[row][following], rows[row + 1][following], rows[row + 1][index]),
                )
        for index in range(segments):
            following = (index + 1) % segments
            self.face(name, material, surface, (rows[-1][index], rows[-1][following], top))

    def torus(
        self,
        name: str,
        material: str,
        surface: str,
        major_radius: float,
        minor_radius: float,
        segments: int = 12,
        tube_segments: int = 5,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        rings: list[list[Vec3]] = []
        for index in range(segments):
            angle = math.tau * index / segments
            cosine, sine = math.cos(angle), math.sin(angle)
            ring: list[Vec3] = []
            for tube_index in range(tube_segments):
                tube_angle = math.tau * tube_index / tube_segments
                radial = major_radius + math.cos(tube_angle) * minor_radius
                ring.append((
                    offset[0] + cosine * radial,
                    offset[1] + math.sin(tube_angle) * minor_radius,
                    offset[2] + sine * radial,
                ))
            rings.append(ring)
        for index in range(segments):
            following = (index + 1) % segments
            for tube_index in range(tube_segments):
                following_tube = (tube_index + 1) % tube_segments
                self.face(
                    name,
                    material,
                    surface,
                    (
                        rings[index][tube_index], rings[following][tube_index],
                        rings[following][following_tube], rings[index][following_tube],
                    ),
                )

    def ribbon(
        self,
        name: str,
        material: str,
        surface: str,
        path: Sequence[Vec2],
        width: float,
        depth: float,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        if len(path) < 2:
            return
        left: list[Vec2] = []
        right: list[Vec2] = []
        half = width / 2
        for index, point in enumerate(path):
            previous = path[max(0, index - 1)]
            following = path[min(len(path) - 1, index + 1)]
            dx, dy = following[0] - previous[0], following[1] - previous[1]
            length = math.hypot(dx, dy) or 1.0
            nx, ny = -dy / length * half, dx / length * half
            left.append((point[0] + nx, point[1] + ny))
            right.append((point[0] - nx, point[1] - ny))
        self.extrude(name, material, surface, tuple(left + list(reversed(right))), depth, offset)

    def cloth_panel(
        self,
        name: str,
        material: str,
        surface: str,
        width: float,
        height: float,
        depth: float,
        *,
        flare: float = 0.0,
        curve: float = 0.0,
        wave: float = 0.0,
        folds: int = 3,
        segments_x: int = 6,
        segments_y: int = 8,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        """构建适合低像素正交渲染的弧面布料，而不是平面挤出片。"""
        segments_x = max(2, segments_x)
        segments_y = max(2, segments_y)
        front_rows: list[list[Vec3]] = []
        back_rows: list[list[Vec3]] = []
        for row in range(segments_y + 1):
            v = row / segments_y
            y = height * (0.5 - v)
            half_width = width * 0.5 * (1.0 + flare * v)
            front_row: list[Vec3] = []
            back_row: list[Vec3] = []
            for column in range(segments_x + 1):
                u = column / segments_x
                normalized_x = u * 2.0 - 1.0
                x = normalized_x * half_width
                bulge = curve * (1.0 - normalized_x * normalized_x)
                ripple = wave * math.cos(normalized_x * math.pi * folds + v * 0.45) * (0.45 + 0.55 * v)
                hem_wave = wave * 0.22 * math.sin((normalized_x + 1.0) * math.pi * folds) * v * v
                front_row.append((
                    offset[0] + x,
                    offset[1] + y + hem_wave,
                    offset[2] + depth * 0.5 + bulge + ripple,
                ))
                back_row.append((
                    offset[0] + x,
                    offset[1] + y + hem_wave,
                    offset[2] - depth * 0.5 + bulge * 0.55 + ripple * 0.30,
                ))
            front_rows.append(front_row)
            back_rows.append(back_row)

        for row in range(segments_y):
            for column in range(segments_x):
                self.face(
                    name,
                    material,
                    surface,
                    (
                        front_rows[row][column],
                        front_rows[row + 1][column],
                        front_rows[row + 1][column + 1],
                        front_rows[row][column + 1],
                    ),
                )
                self.face(
                    f"{name}_back",
                    material,
                    surface,
                    (
                        back_rows[row][column + 1],
                        back_rows[row + 1][column + 1],
                        back_rows[row + 1][column],
                        back_rows[row][column],
                    ),
                )

        for row in range(segments_y):
            for column in (0, segments_x):
                following = row + 1
                self.face(
                    f"{name}_edge",
                    material,
                    surface,
                    (
                        front_rows[row][column],
                        front_rows[following][column],
                        back_rows[following][column],
                        back_rows[row][column],
                    ),
                )
        for row in (0, segments_y):
            for column in range(segments_x):
                self.face(
                    f"{name}_edge",
                    material,
                    surface,
                    (
                        front_rows[row][column],
                        front_rows[row][column + 1],
                        back_rows[row][column + 1],
                        back_rows[row][column],
                    ),
                )

    def radial_repeat(
        self,
        source: "ModelBuilder",
        count: int,
        radius: float,
        *,
        start_angle: float = 0.0,
        prefix: str = "repeat_",
    ) -> None:
        for index in range(count):
            angle = start_angle + 360.0 * index / count
            offset = rotate_euler((radius, 0.0, 0.0), (0.0, angle, 0.0))
            self.extend(source, rotation=(0.0, angle, 0.0), offset=offset, prefix=f"{prefix}{index:02d}_")


def mirrored_outline(right: Sequence[Vec2]) -> tuple[Vec2, ...]:
    """把从中轴底部到顶部的右侧轮廓镜像为闭合对称轮廓。"""
    return tuple((-x, y) for x, y in reversed(right)) + tuple(right)


def crescent_outline(
    outer_radius: float,
    inner_radius: float,
    *,
    samples: int = 8,
    opening: float = 0.52,
) -> tuple[Vec2, ...]:
    outer = [
        (math.cos(angle) * outer_radius, math.sin(angle) * outer_radius)
        for angle in (
            math.pi * (0.5 + opening) +
            (math.pi * (2.0 - 2.0 * opening)) * index / samples
            for index in range(samples + 1)
        )
    ]
    inner = [
        (math.cos(angle) * inner_radius + outer_radius * 0.28, math.sin(angle) * inner_radius)
        for angle in (
            math.pi * (2.5 - opening) -
            (math.pi * (2.0 - 2.0 * opening)) * index / samples
            for index in range(samples + 1)
        )
    ]
    return tuple(outer + inner)


def bounds(faces: Iterable[StyledFace]) -> tuple[Vec3, Vec3]:
    points = [point for face in faces for point in face.points]
    if not points:
        raise ValueError("模型没有几何体")
    return (
        tuple(min(point[axis] for point in points) for axis in range(3)),
        tuple(max(point[axis] for point in points) for axis in range(3)),
    )


def _triangulate_outline(outline: Sequence[Vec2]) -> list[tuple[int, int, int]]:
    """耳切法拆分凹多边形，避免运行时的扇形拆分跨过凹口。"""
    indices = list(range(len(outline)))
    counter_clockwise = _signed_area(outline) > 0
    triangles: list[tuple[int, int, int]] = []
    guard = len(indices) * len(indices)
    while len(indices) > 3 and guard > 0:
        guard -= 1
        clipped = False
        for cursor in range(len(indices)):
            previous = indices[cursor - 1]
            current = indices[cursor]
            following = indices[(cursor + 1) % len(indices)]
            if not _is_convex(outline[previous], outline[current], outline[following], counter_clockwise):
                continue
            if any(
                index not in (previous, current, following) and
                _inside_triangle(outline[index], outline[previous], outline[current], outline[following])
                for index in indices
            ):
                continue
            triangles.append((previous, current, following))
            del indices[cursor]
            clipped = True
            break
        if not clipped:
            break
    if len(indices) == 3:
        triangles.append(tuple(indices))
    if len(triangles) != len(outline) - 2:
        return [(0, index, index + 1) for index in range(1, len(outline) - 1)]
    return triangles


def _signed_area(outline: Sequence[Vec2]) -> float:
    return sum(
        outline[index][0] * outline[(index + 1) % len(outline)][1] -
        outline[(index + 1) % len(outline)][0] * outline[index][1]
        for index in range(len(outline))
    ) * 0.5


def _is_convex(a: Vec2, b: Vec2, c: Vec2, counter_clockwise: bool) -> bool:
    cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
    return cross > 1e-9 if counter_clockwise else cross < -1e-9


def _inside_triangle(point: Vec2, a: Vec2, b: Vec2, c: Vec2) -> bool:
    def side(p1: Vec2, p2: Vec2, p3: Vec2) -> float:
        return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])

    d1 = side(point, a, b)
    d2 = side(point, b, c)
    d3 = side(point, c, a)
    has_negative = d1 < -1e-9 or d2 < -1e-9 or d3 < -1e-9
    has_positive = d1 > 1e-9 or d2 > 1e-9 or d3 > 1e-9
    return not (has_negative and has_positive)
