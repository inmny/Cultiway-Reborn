#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""组合超采样语义图层，并量化为干净的目标像素图。"""

from __future__ import annotations

import hashlib
from collections import defaultdict
from dataclasses import dataclass
from math import cos, pi, sin, sqrt

from PIL import Image

from .palette import PaletteTheme
from .surface_styles import get_surface_style
from .types import BakeLayer


DETAIL_ROLES = {"edge", "rim", "ridge", "trim", "gem", "core", "glint", "fire", "water"}
COVERAGE_BAYER = ((0.125, 0.625), (0.875, 0.375))


@dataclass(frozen=True)
class FrameImages:
    body: Image.Image
    emission: Image.Image
    shadow: Image.Image
    mask: Image.Image
    opaque_pixels: int
    source_hash: str


def compose_layers(layers: list[BakeLayer], theme: PaletteTheme) -> FrameImages:
    if not layers:
        raise ValueError("至少需要一个模块图层")
    width = layers[0].width
    height = layers[0].height
    sample_scale = layers[0].sample_scale
    if any(
        layer.width != width or layer.height != height or layer.sample_scale != sample_scale
        for layer in layers
    ):
        raise ValueError("参与组合的模块图层尺寸或超采样倍率不一致")

    count = width * height
    depth = [-1e9] * count
    materials: list[str | None] = [None] * count
    surfaces: list[str | None] = [None] * count
    objects: list[str | None] = [None] * count
    positions = [(0.0, 0.0, 0.0)] * count
    lights = [0.0] * count
    emissions = [0] * count
    owners = [-1] * count
    orders = [-10_000] * count
    ordered_layers = sorted(layers, key=lambda item: item.order)
    slots = [layer.slot for layer in ordered_layers]
    for owner, layer in enumerate(ordered_layers):
        for index, material in enumerate(layer.materials):
            if material is None:
                continue
            candidate = layer.depth[index]
            is_nearer = candidate > depth[index] + 1e-7
            is_ordered_tie = abs(candidate - depth[index]) <= 1e-7 and layer.order >= orders[index]
            if not (is_nearer or is_ordered_tie):
                continue
            depth[index] = candidate
            materials[index] = material
            surfaces[index] = layer.surfaces[index]
            objects[index] = layer.objects[index]
            positions[index] = layer.positions[index]
            lights[index] = layer.lights[index]
            emissions[index] = layer.emissions[index]
            owners[index] = owner
            orders[index] = layer.order

    if sample_scale > 1:
        (
            width,
            height,
            depth,
            materials,
            surfaces,
            objects,
            positions,
            lights,
            emissions,
            owners,
            orders,
        ) = _downsample_semantics(
            width,
            height,
            sample_scale,
            depth,
            materials,
            surfaces,
            objects,
            positions,
            lights,
            emissions,
            owners,
            orders,
        )

    _apply_surface_patterns(surfaces, objects, positions, lights, width, height)
    shades = [
        0 if material is None else _quantize_light(lights[index], surfaces[index])
        for index, material in enumerate(materials)
    ]
    _apply_depth_accents(materials, objects, shades, depth, width, height)
    _apply_rim_highlights(materials, surfaces, shades, width, height)
    body_pixels = _colorize(materials, shades, theme)
    _add_outer_outline(body_pixels, materials, width, height, theme)

    body = Image.new("RGBA", (width, height))
    body.putdata(body_pixels)
    mask = Image.new("L", (width, height))
    mask.putdata([255 if material is not None else 0 for material in materials])
    emission = _build_emission(materials, shades, emissions, width, height, theme)
    shadow = _build_shadow(materials, width, height, theme, enabled=width != 28)
    opaque_pixels = sum(material is not None for material in materials)
    source_hash = _semantic_hash(
        materials,
        surfaces,
        objects,
        shades,
        emissions,
        owners,
        slots,
        width,
        height,
    )
    return FrameImages(body, emission, shadow, mask, opaque_pixels, source_hash)


def debug_layer_image(layer: BakeLayer, theme: PaletteTheme) -> Image.Image:
    pixels = []
    for material, shade in zip(layer.materials, layer.shades):
        if material is None:
            pixels.append((0, 0, 0, 0))
        else:
            pixels.append((*theme.ramp_for(material)[shade], 255))
    image = Image.new("RGBA", (layer.width, layer.height))
    image.putdata(pixels)
    return image


def _downsample_semantics(
    width: int,
    height: int,
    sample_scale: int,
    depth: list[float],
    materials: list[str | None],
    surfaces: list[str | None],
    objects: list[str | None],
    positions: list[tuple[float, float, float]],
    lights: list[float],
    emissions: list[int],
    owners: list[int],
    orders: list[int],
) -> tuple[
    int,
    int,
    list[float],
    list[str | None],
    list[str | None],
    list[str | None],
    list[tuple[float, float, float]],
    list[float],
    list[int],
    list[int],
    list[int],
]:
    target_width = width // sample_scale
    target_height = height // sample_scale
    count = target_width * target_height
    out_depth = [-1e9] * count
    out_materials: list[str | None] = [None] * count
    out_surfaces: list[str | None] = [None] * count
    out_objects: list[str | None] = [None] * count
    out_positions = [(0.0, 0.0, 0.0)] * count
    out_lights = [0.0] * count
    out_emissions = [0] * count
    out_owners = [-1] * count
    out_orders = [-10_000] * count
    sample_count = sample_scale * sample_scale

    for y in range(target_height):
        for x in range(target_width):
            samples = [
                (y * sample_scale + sy) * width + x * sample_scale + sx
                for sy in range(sample_scale)
                for sx in range(sample_scale)
            ]
            occupied = [index for index in samples if materials[index] is not None]
            if not occupied:
                continue
            groups: dict[tuple[str, str, str, int, int], list[int]] = defaultdict(list)
            for index in occupied:
                key = (
                    materials[index] or "",
                    surfaces[index] or "neutral",
                    objects[index] or "mesh",
                    owners[index],
                    orders[index],
                )
                groups[key].append(index)
            chosen_key, chosen_samples = max(
                groups.items(),
                key=lambda item: (
                    len(item[1]),
                    max(emissions[index] for index in item[1]),
                    max(depth[index] for index in item[1]),
                    item[0],
                ),
            )
            coverage = len(occupied) / sample_count
            role = chosen_key[0].lower()
            preserves_detail = (
                role in DETAIL_ROLES
                or get_surface_style(chosen_key[1]).preserve_detail
                or max(emissions[index] for index in chosen_samples) > 0
            )
            threshold = COVERAGE_BAYER[y % 2][x % 2]
            if coverage < threshold and not preserves_detail:
                continue
            target = y * target_width + x
            out_materials[target] = chosen_key[0]
            out_surfaces[target] = chosen_key[1]
            out_objects[target] = chosen_key[2]
            out_positions[target] = tuple(
                sum(positions[index][axis] for index in chosen_samples) / len(chosen_samples)
                for axis in range(3)
            )
            out_owners[target] = chosen_key[3]
            out_orders[target] = chosen_key[4]
            out_depth[target] = max(depth[index] for index in chosen_samples)
            out_lights[target] = sum(lights[index] for index in chosen_samples) / len(chosen_samples)
            out_emissions[target] = max(emissions[index] for index in chosen_samples)

    return (
        target_width,
        target_height,
        out_depth,
        out_materials,
        out_surfaces,
        out_objects,
        out_positions,
        out_lights,
        out_emissions,
        out_owners,
        out_orders,
    )


def _apply_surface_patterns(
    surfaces: list[str | None],
    objects: list[str | None],
    positions: list[tuple[float, float, float]],
    lights: list[float],
    width: int,
    height: int,
) -> None:
    size = min(width, height)
    for index, surface in enumerate(surfaces):
        if surface is None:
            continue
        style = get_surface_style(surface)
        if not style.pattern or style.pattern_strength <= 0 or size < style.pattern_min_size:
            continue
        phase = (_stable_hash(objects[index] or "mesh") & 0xFFFF) / 65535.0 * pi * 2.0
        signal = _surface_pattern(style.pattern, positions[index], style.pattern_scale, phase)
        lights[index] = max(0.0, min(1.0, lights[index] + signal * style.pattern_strength))


def _surface_pattern(
    pattern: str,
    position: tuple[float, float, float],
    scale: float,
    phase: float,
) -> float:
    x, y, z = position
    scale = max(0.1, scale)
    tau = pi * 2.0
    if pattern == "brushed":
        primary = sin((y * 1.7 + x * 0.35 - z * 0.25) * tau * scale + phase)
        envelope = sin((y * 0.41 + z * 0.23) * tau * scale + phase * 0.37)
        return primary * (0.72 + envelope * 0.28)
    if pattern == "patina":
        first = sin((x * 0.75 + z * 0.55) * tau * scale + phase)
        second = cos((y * 0.85 - z * 0.35) * tau * scale * 0.73 + phase * 0.61)
        third = sin((x - y + z) * tau * scale * 0.31 - phase * 0.47)
        field = (first + second + third) / 3.0
        return -max(0.0, field) + min(0.0, field) * 0.20
    if pattern == "jade_cloud":
        bend = sin(y * pi * scale + phase) * 1.35
        return sin((x + z * 0.65) * tau * scale + bend + phase * 0.43) * 0.82
    if pattern == "crystal_facet":
        first = sin((x + y * 0.72) * tau * scale + phase)
        second = sin((z - y * 0.58) * tau * scale * 0.83 - phase * 0.70)
        return max(first, second) * 0.72 + min(first, second) * 0.18
    if pattern == "silk_weave":
        first = sin((x + y) * tau * scale + phase)
        second = sin((x - y) * tau * scale - phase)
        return (first + second) * 0.34
    if pattern == "stone_mottle":
        first = sin((x * 0.82 + z * 0.61) * tau * scale + phase)
        second = cos((y * 0.77 - x * 0.29) * tau * scale * 0.79 - phase * 0.52)
        return first * second * 0.78
    if pattern == "wood_grain":
        radial = sqrt(x * x + z * z)
        bend = sin(y * tau * scale * 0.33 + phase) * 0.55
        return sin((radial * 1.8 + y * 0.36) * tau * scale + bend + phase) * 0.78
    if pattern == "bone_grain":
        first = sin((y * 1.15 + x * 0.26) * tau * scale + phase)
        second = sin((z + y * 0.19) * tau * scale * 0.37 - phase)
        return first * 0.52 + second * 0.22
    return 0.0


def _stable_hash(value: str) -> int:
    result = 2166136261
    for byte in value.encode("utf-8"):
        result ^= byte
        result = result * 16777619 & 0xFFFFFFFF
    return result


def _quantize_light(amount: float, surface: str | None) -> int:
    value = max(0.0, min(1.0, amount))
    thresholds = get_surface_style(surface).shade_thresholds
    return sum(value >= threshold for threshold in thresholds)


def _apply_depth_accents(
    materials: list[str | None],
    objects: list[str | None],
    shades: list[int],
    depth: list[float],
    width: int,
    height: int,
) -> None:
    original = list(shades)
    for y in range(height):
        for x in range(width):
            index = y * width + x
            if materials[index] is None:
                continue
            amount = original[index]
            for dx, dy in ((-1, 0), (0, -1), (1, 0), (0, 1)):
                xx, yy = x + dx, y + dy
                if not (0 <= xx < width and 0 <= yy < height):
                    continue
                other = yy * width + xx
                if materials[other] is None:
                    continue
                delta = depth[other] - depth[index]
                if delta > 0.13:
                    amount = min(amount, max(0, original[index] - 1))
                elif objects[other] != objects[index] and (dx > 0 or dy > 0) and delta > -0.02:
                    amount = min(amount, max(0, original[index] - 1))
            shades[index] = amount


def _apply_rim_highlights(
    materials: list[str | None],
    surfaces: list[str | None],
    shades: list[int],
    width: int,
    height: int,
) -> None:
    original = list(shades)
    for y in range(height):
        for x in range(width):
            index = y * width + x
            if materials[index] is None or not get_surface_style(surfaces[index]).pixel_rim:
                continue
            left_empty = x == 0 or materials[index - 1] is None
            top_empty = y == 0 or materials[index - width] is None
            if left_empty or top_empty:
                shades[index] = min(5, original[index] + 1)


def _colorize(
    materials: list[str | None],
    shades: list[int],
    theme: PaletteTheme,
) -> list[tuple[int, int, int, int]]:
    return [
        (0, 0, 0, 0) if material is None else (*theme.ramp_for(material)[shade], 255)
        for material, shade in zip(materials, shades)
    ]


def _add_outer_outline(
    pixels: list[tuple[int, int, int, int]],
    materials: list[str | None],
    width: int,
    height: int,
    theme: PaletteTheme,
) -> None:
    result = list(pixels)
    cardinal = ((-1, 0), (1, 0), (0, -1), (0, 1))
    diagonal = ((-1, -1), (1, -1), (-1, 1), (1, 1))
    for y in range(height):
        for x in range(width):
            index = y * width + x
            if materials[index] is not None:
                continue
            neighbors = [
                (dx, dy)
                for dx, dy in cardinal
                if 0 <= x + dx < width and 0 <= y + dy < height
                and materials[(y + dy) * width + x + dx] is not None
            ]
            if not neighbors:
                diagonal_neighbors = [
                    (dx, dy)
                    for dx, dy in diagonal
                    if 0 <= x + dx < width and 0 <= y + dy < height
                    and materials[(y + dy) * width + x + dx] is not None
                ]
                if len(diagonal_neighbors) < 2:
                    continue
                neighbors = diagonal_neighbors
            lit_side = any(dx > 0 or dy > 0 for dx, dy in neighbors)
            result[index] = (*(theme.outline_soft if lit_side else theme.outline_dark), 255)
    pixels[:] = result


def _build_emission(
    materials: list[str | None],
    shades: list[int],
    strengths: list[int],
    width: int,
    height: int,
    theme: PaletteTheme,
) -> Image.Image:
    pixels = [(0, 0, 0, 0)] * (width * height)
    for index, strength in enumerate(strengths):
        material = materials[index]
        if material is None or strength <= 0:
            continue
        color = theme.ramp_for(material)[max(4, shades[index])]
        pixels[index] = (*color, min(220, 70 + strength * 45))
    source = list(pixels)
    for y in range(height):
        for x in range(width):
            index = y * width + x
            if source[index][3] == 0:
                continue
            r, g, b, alpha = source[index]
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    xx, yy = x + dx, y + dy
                    if not (0 <= xx < width and 0 <= yy < height):
                        continue
                    target = yy * width + xx
                    halo_alpha = int(alpha * 0.16 / max(1.0, sqrt(dx * dx + dy * dy)))
                    if halo_alpha > pixels[target][3]:
                        pixels[target] = (r, g, b, halo_alpha)
    image = Image.new("RGBA", (width, height))
    image.putdata(pixels)
    return image


def _build_shadow(
    materials: list[str | None],
    width: int,
    height: int,
    theme: PaletteTheme,
    *,
    enabled: bool,
) -> Image.Image:
    image = Image.new("RGBA", (width, height))
    if not enabled:
        return image
    occupied = [(index % width, index // width) for index, value in enumerate(materials) if value is not None]
    if not occupied:
        return image
    min_x = min(point[0] for point in occupied)
    max_x = max(point[0] for point in occupied)
    max_y = max(point[1] for point in occupied)
    center_x = (min_x + max_x) * 0.5
    radius_x = max(2.0, (max_x - min_x + 1) * 0.30)
    center_y = min(height - 2.0, max_y + 0.65)
    radius_y = max(1.0, radius_x * 0.20)
    pixels = [(0, 0, 0, 0)] * (width * height)
    for y in range(height):
        for x in range(width):
            distance = ((x - center_x) / radius_x) ** 2 + ((y - center_y) / radius_y) ** 2
            if distance > 1.0:
                continue
            alpha = int((1.0 - distance) * 58)
            pixels[y * width + x] = (*theme.shadow, alpha)
    image.putdata(pixels)
    return image


def _semantic_hash(
    materials: list[str | None],
    surfaces: list[str | None],
    objects: list[str | None],
    shades: list[int],
    emissions: list[int],
    owners: list[int],
    slots: list[str],
    width: int,
    height: int,
) -> str:
    digest = hashlib.sha256()
    digest.update(f"{width}x{height}|{'|'.join(slots)}".encode("utf-8"))
    for material, surface, object_name, shade, emission, owner in zip(
        materials,
        surfaces,
        objects,
        shades,
        emissions,
        owners,
    ):
        digest.update(
            f"{material or '-'}:{surface or '-'}:{object_name or '-'}:{shade}:{emission}:{owner};".encode("utf-8")
        )
    return digest.hexdigest()
