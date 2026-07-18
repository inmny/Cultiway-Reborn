#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""只用于生成可在 Blockbench 中继续加工的三件原型 OBJ。"""

from __future__ import annotations

import math
from collections.abc import Sequence

from Scripts.artifact_compose.math3d import Vec2, Vec3, add

from .types import MeshFace


class MeshBuilder:
    def __init__(self) -> None:
        self.faces: list[MeshFace] = []

    def extrude_polygon(
        self,
        name: str,
        material: str,
        outline: Sequence[Vec2],
        depth: float,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        if len(outline) < 3:
            return
        front = [add((x, y, depth / 2), offset) for x, y in outline]
        back = [add((x, y, -depth / 2), offset) for x, y in outline]
        self.faces.append(MeshFace(tuple(front), material, name))
        self.faces.append(MeshFace(tuple(reversed(back)), material, name))
        for index in range(len(outline)):
            following = (index + 1) % len(outline)
            self.faces.append(MeshFace(
                (front[index], front[following], back[following], back[index]),
                material,
                name,
            ))

    def box(
        self,
        name: str,
        material: str,
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
        indices = (
            (4, 5, 6, 7), (1, 0, 3, 2), (0, 4, 7, 3),
            (5, 1, 2, 6), (3, 7, 6, 2), (0, 1, 5, 4),
        )
        for face in indices:
            self.faces.append(MeshFace(tuple(vertices[index] for index in face), material, name))

    def disc(
        self,
        name: str,
        material: str,
        radius_x: float,
        radius_y: float,
        depth: float,
        segments: int = 16,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        points = [
            (math.cos(math.tau * index / segments) * radius_x,
             math.sin(math.tau * index / segments) * radius_y)
            for index in range(segments)
        ]
        self.extrude_polygon(name, material, points, depth, offset)

    def ring(
        self,
        name: str,
        material: str,
        outer: Vec2,
        inner: Vec2,
        depth: float,
        segments: int = 16,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        front_z = depth / 2 + offset[2]
        back_z = -depth / 2 + offset[2]
        outer_front: list[Vec3] = []
        inner_front: list[Vec3] = []
        outer_back: list[Vec3] = []
        inner_back: list[Vec3] = []
        for index in range(segments):
            angle = math.tau * index / segments
            cosine = math.cos(angle)
            sine = math.sin(angle)
            outer_front.append((offset[0] + cosine * outer[0], offset[1] + sine * outer[1], front_z))
            inner_front.append((offset[0] + cosine * inner[0], offset[1] + sine * inner[1], front_z))
            outer_back.append((offset[0] + cosine * outer[0], offset[1] + sine * outer[1], back_z))
            inner_back.append((offset[0] + cosine * inner[0], offset[1] + sine * inner[1], back_z))
        for index in range(segments):
            following = (index + 1) % segments
            quads = (
                (outer_front[index], outer_front[following], inner_front[following], inner_front[index]),
                (outer_back[following], outer_back[index], inner_back[index], inner_back[following]),
                (outer_front[index], outer_back[index], outer_back[following], outer_front[following]),
                (inner_front[following], inner_back[following], inner_back[index], inner_front[index]),
            )
            for quad in quads:
                self.faces.append(MeshFace(quad, material, name))

    def lathe(
        self,
        name: str,
        material: str,
        profile: Sequence[Vec2],
        segments: int = 16,
        offset: Vec3 = (0.0, 0.0, 0.0),
        cap_bottom: bool = True,
        cap_top: bool = True,
    ) -> None:
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
                self.faces.append(MeshFace(
                    (rings[row][index], rings[row][following],
                     rings[row + 1][following], rings[row + 1][index]),
                    material,
                    name,
                ))
        if cap_bottom and profile[0][0] > 0:
            self.faces.append(MeshFace(tuple(reversed(rings[0])), material, name))
        if cap_top and profile[-1][0] > 0:
            self.faces.append(MeshFace(tuple(rings[-1]), material, name))

    def torus(
        self,
        name: str,
        material: str,
        major_radius: float,
        minor_radius: float,
        segments: int = 16,
        tube_segments: int = 6,
        offset: Vec3 = (0.0, 0.0, 0.0),
    ) -> None:
        rings: list[list[Vec3]] = []
        for index in range(segments):
            angle = math.tau * index / segments
            cosine = math.cos(angle)
            sine = math.sin(angle)
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
                self.faces.append(MeshFace((
                    rings[index][tube_index], rings[following][tube_index],
                    rings[following][following_tube], rings[index][following_tube],
                ), material, name))
