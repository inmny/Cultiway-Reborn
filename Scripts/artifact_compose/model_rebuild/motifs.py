#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""各器形共享的中式器物纹饰。"""

from __future__ import annotations

import math

from Scripts.artifact_compose.math3d import Vec3

from .builder import ModelBuilder, crescent_outline


def jewel(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    radius: float,
    *,
    material: str = "gem",
    surface: str = "jade",
    squash: float = 1.0,
) -> None:
    target.faceted_disc(
        name,
        material,
        surface,
        radius,
        radius * squash,
        radius * 0.42,
        radius * 0.22,
        8,
        center,
    )


def diamond_rune(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    size: float,
    *,
    material: str = "core",
    surface: str = "emissive",
) -> None:
    x, y, z = center
    target.extrude(
        name,
        material,
        surface,
        ((0.0, size), (size * 0.68, 0.0), (0.0, -size), (-size * 0.68, 0.0)),
        size * 0.24,
        (x, y, z),
    )


def star_rune(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    radius: float,
    *,
    points: int = 6,
    material: str = "glint",
    surface: str = "emissive",
) -> None:
    outline = []
    for index in range(points * 2):
        angle = math.pi / 2 + math.pi * index / points
        current = radius if index % 2 == 0 else radius * 0.42
        outline.append((math.cos(angle) * current, math.sin(angle) * current))
    target.extrude(name, material, surface, outline, radius * 0.18, center)


def crescent(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    radius: float,
    *,
    material: str = "rim",
    surface: str = "polished_metal",
) -> None:
    target.extrude(
        name,
        material,
        surface,
        crescent_outline(radius, radius * 0.76, samples=9),
        radius * 0.28,
        center,
    )


def lotus(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    radius: float,
    *,
    petals: int = 6,
    material: str = "main",
    surface: str = "jade",
    core_material: str = "gem",
) -> None:
    petal = ModelBuilder()
    petal.extrude(
        "petal",
        material,
        surface,
        ((-radius * 0.24, 0.0), (0.0, radius), (radius * 0.24, 0.0), (0.0, radius * 0.28)),
        radius * 0.22,
    )
    for index in range(petals):
        target.extend(
            petal,
            rotation=(0.0, 0.0, 360.0 * index / petals),
            offset=center,
            prefix=f"{name}_{index:02d}_",
        )
    jewel(target, f"{name}_core", (center[0], center[1], center[2] + radius * 0.08), radius * 0.28,
          material=core_material, surface="emissive")


def cloud_pair(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    width: float,
    *,
    material: str = "edge",
    surface: str = "polished_metal",
    depth: float = 0.12,
) -> None:
    x, y, z = center
    left = (
        (0.0, 0.0), (-width * 0.24, width * 0.12), (-width * 0.47, width * 0.08),
        (-width * 0.34, -width * 0.08), (-width * 0.58, -width * 0.16),
        (-width * 0.86, -width * 0.04), (-width, width * 0.14),
    )
    right = tuple((-px, py) for px, py in left)
    target.ribbon(f"{name}_left", material, surface, left, width * 0.13, depth, (x, y, z))
    target.ribbon(f"{name}_right", material, surface, right, width * 0.13, depth, (x, y, z))


def tassel(
    target: ModelBuilder,
    name: str,
    knot: Vec3,
    length: float,
    *,
    strands: int = 3,
    material: str = "charm",
    surface: str = "silk",
) -> None:
    x, y, z = knot
    jewel(target, f"{name}_knot", knot, length * 0.10, material="gem", surface="jade")
    for index in range(strands):
        spread = (index - (strands - 1) / 2) * length * 0.10
        path = (
            (spread * 0.25, -length * 0.08),
            (spread * 0.45, -length * 0.38),
            (spread, -length),
        )
        target.ribbon(
            f"{name}_strand_{index:02d}",
            material,
            surface,
            path,
            length * 0.08,
            length * 0.07,
            (x, y, z),
        )


def small_bell(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    size: float,
    *,
    material: str = "rim",
    surface: str = "polished_metal",
) -> None:
    x, y, z = center
    target.lathe(
        name,
        material,
        surface,
        ((size * 0.22, size * 0.35), (size * 0.38, size * 0.12),
         (size * 0.46, -size * 0.28), (size * 0.56, -size * 0.38)),
        8,
        (x, y, z),
    )
    jewel(target, f"{name}_clapper", (x, y - size * 0.48, z), size * 0.12,
          material="glint", surface="emissive")


def flame(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    size: float,
    *,
    material: str = "fire",
    surface: str = "emissive",
) -> None:
    x, y, z = center
    outline = (
        (0.0, size), (size * 0.22, size * 0.55), (size * 0.18, size * 0.18),
        (size * 0.42, -size * 0.18), (size * 0.16, -size * 0.48),
        (0.0, -size * 0.60), (-size * 0.22, -size * 0.42),
        (-size * 0.38, -size * 0.08), (-size * 0.14, size * 0.24),
    )
    target.extrude(name, material, surface, outline, size * 0.24, (x, y, z))
    diamond_rune(target, f"{name}_core", (x, y - size * 0.12, z + size * 0.18), size * 0.22)


def trigram(
    target: ModelBuilder,
    name: str,
    center: Vec3,
    radius: float,
    *,
    material: str = "ridge",
    surface: str = "polished_metal",
) -> None:
    x, y, z = center
    for index in range(8):
        angle = math.tau * index / 8
        local = ModelBuilder()
        local.box("bar", material, surface, (radius * 0.42, radius * 0.11, radius * 0.10), (0.0, radius, 0.0))
        target.extend(local, rotation=(0.0, 0.0, -math.degrees(angle)), offset=(x, y, z), prefix=f"{name}_{index:02d}_")
    diamond_rune(target, f"{name}_center", center, radius * 0.28, material="core", surface="emissive")


def bead_chain(
    target: ModelBuilder,
    name: str,
    points: tuple[Vec3, ...],
    radius: float,
    *,
    material: str = "gem",
    surface: str = "jade",
) -> None:
    for index, point in enumerate(points):
        target.ellipsoid(f"{name}_{index:02d}", material, surface, (radius, radius, radius), 8, 4, point)
