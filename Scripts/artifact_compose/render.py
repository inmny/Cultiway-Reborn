#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""将组合后的矢量模型栅格化为 28x28 PNG。"""

from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image, ImageDraw

from .colors import resolve_color
from .compose import ComposedArtifact
from .geometry import identity_transform, primitive_points, scale_points


SUPPORTED_PRIMITIVES = {"polygon", "rect", "ellipse", "line"}


def render_png(
    composed: ComposedArtifact,
    output_path: Path,
    size: int = 28,
    supersample: int = 1,
    palette_colors: int = 96,
    alpha_threshold: int = 24,
    finish: str = "artifact",
) -> Image.Image:
    image = render_high_res(composed, size, supersample)
    image = apply_template_shading(image, composed, supersample)
    pixel = downsample_pixel_art(image, size, 0, alpha_threshold)
    if finish == "artifact":
        pixel = apply_artifact_finish(pixel, f"{composed.seed}|{composed.template.key}|{composed.sample_index}")
    elif finish != "flat":
        raise ValueError(f"未知 PNG finish: {finish}")
    pixel = limit_palette(pixel, palette_colors, alpha_threshold)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    pixel.save(output_path)
    return pixel


def save_preview(pixel_image: Image.Image, output_path: Path, scale: int = 6) -> None:
    if scale <= 1:
        return
    output_path.parent.mkdir(parents=True, exist_ok=True)
    preview = pixel_image.resize((pixel_image.width * scale, pixel_image.height * scale), Image.Resampling.NEAREST)
    preview.save(output_path)


def render_high_res(composed: ComposedArtifact, size: int, supersample: int) -> Image.Image:
    image = Image.new("RGBA", (size * supersample, size * supersample), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    for placed in sorted(composed.modules, key=lambda item: item.placement.z):
        shapes = sorted(placed.variant.shapes, key=lambda item: int(item.get("z", 0)))
        for primitive in shapes:
            draw_primitive(draw, primitive, placed.transform, placed.palette, supersample)
    return image


def draw_primitive(draw: ImageDraw.ImageDraw, primitive: dict, transform, palette: dict[str, str], scale: int) -> None:
    kind = primitive.get("type")
    if kind not in SUPPORTED_PRIMITIVES:
        raise ValueError(f"不支持的矢量形状类型: {kind}")
    opacity = float(primitive.get("opacity", 1.0))
    points = scale_points(primitive_points(primitive, transform), scale)
    fill = resolve_color(primitive.get("fill"), palette, opacity)
    stroke = resolve_color(primitive.get("stroke"), palette, opacity)
    stroke_width = max(1, round(float(primitive.get("stroke_width", 0)) * transform.average_scale * scale))

    if kind == "line":
        if stroke is None or len(points) < 2:
            return
        draw.line(points, fill=stroke.tuple(), width=stroke_width, joint="curve")
        return

    if len(points) < 3:
        return
    if fill is not None:
        draw.polygon(points, fill=fill.tuple())
    if stroke is not None and stroke_width > 0:
        draw.line(points + [points[0]], fill=stroke.tuple(), width=stroke_width, joint="curve")


def apply_template_shading(image: Image.Image, composed: ComposedArtifact, supersample: int) -> Image.Image:
    if not composed.template.shading:
        return image
    result = image.copy()
    alpha = result.getchannel("A")
    for shade in composed.template.shading:
        opacity = float(shade.get("opacity", 0.18))
        opacity = max(0.0, min(1.0, opacity))
        mask = Image.new("L", result.size, 0)
        mask_draw = ImageDraw.Draw(mask)
        points = scale_points(primitive_points(shade, identity_transform()), supersample)
        if shade.get("type") == "line":
            width = max(1, round(float(shade.get("stroke_width", 1)) * supersample))
            mask_draw.line(points, fill=round(255 * opacity), width=width, joint="curve")
        elif len(points) >= 3:
            mask_draw.polygon(points, fill=round(255 * opacity))
        mask = Image.composite(mask, Image.new("L", result.size, 0), alpha)
        result = adjust_gray(result, mask, str(shade.get("mode", "darken")))
    return result


def adjust_gray(image: Image.Image, mask: Image.Image, mode: str) -> Image.Image:
    pixels = image.load()
    mask_pixels = mask.load()
    width, height = image.size
    for y in range(height):
        for x in range(width):
            amount = mask_pixels[x, y] / 255.0
            if amount <= 0:
                continue
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            if mode == "lighten":
                r = round(r + (255 - r) * amount)
                g = round(g + (255 - g) * amount)
                b = round(b + (255 - b) * amount)
            else:
                r = round(r * (1.0 - amount))
                g = round(g * (1.0 - amount))
                b = round(b * (1.0 - amount))
            pixels[x, y] = (r, g, b, a)
    return image


def downsample_pixel_art(
    image: Image.Image,
    size: int,
    palette_colors: int,
    alpha_threshold: int,
) -> Image.Image:
    resized = image.resize((size, size), Image.Resampling.BOX).convert("RGBA")
    output = harden_transparency(resized, alpha_threshold)
    return limit_palette(output, palette_colors, alpha_threshold)


def apply_artifact_finish(image: Image.Image, seed_text: str) -> Image.Image:
    """让矢量平涂结果更接近参考法宝图的手绘像素质感。"""
    seed = stable_seed(seed_text)
    source = harden_transparency(image, 1)
    width, height = source.size
    styled = source.copy()
    source_pixels = source.load()
    styled_pixels = styled.load()

    for y in range(height):
        for x in range(width):
            r, g, b, a = source_pixels[x, y]
            if a == 0:
                continue

            r, g, b = boost_material_color(r, g, b)
            gradient = (x + y) / max(1, width + height - 2)
            if gradient < 0.34:
                r, g, b = lighten_color(r, g, b, (0.34 - gradient) * 0.38)
            elif gradient > 0.54:
                r, g, b = darken_color(r, g, b, (gradient - 0.54) * 0.34)

            transparent_neighbors = count_transparent_neighbors(source_pixels, width, height, x, y)
            if transparent_neighbors:
                r, g, b = darken_color(r, g, b, min(0.24, 0.045 * transparent_neighbors))
                if is_transparent(source_pixels, width, height, x - 1, y) or is_transparent(source_pixels, width, height, x, y - 1):
                    r, g, b = lighten_color(r, g, b, 0.12)
                if is_transparent(source_pixels, width, height, x + 1, y) or is_transparent(source_pixels, width, height, x, y + 1):
                    r, g, b = darken_color(r, g, b, 0.14)

            noise = pixel_noise(seed, x, y)
            if (x + y + seed) % 5 == 0:
                r, g, b = darken_color(r, g, b, 0.055 + (noise & 3) * 0.016)
            if (x * 2 - y + seed) % 9 == 0 and luma(r, g, b) > 62:
                r, g, b = lighten_color(r, g, b, 0.070 + ((noise >> 3) & 3) * 0.014)
            if (x - y + seed) % 7 == 0 and 48 < luma(r, g, b) < 210:
                r, g, b = darken_color(r, g, b, 0.065)
            if (x * 5 + y * 3 + seed) % 37 == 0 and luma(r, g, b) > 132:
                r, g, b = lighten_color(r, g, b, 0.28)

            styled_pixels[x, y] = (r, g, b, 255)

    return add_pixel_outline(styled, source)


def add_pixel_outline(styled: Image.Image, mask_source: Image.Image) -> Image.Image:
    width, height = styled.size
    result = styled.copy()
    styled_pixels = styled.load()
    mask_pixels = mask_source.load()
    result_pixels = result.load()
    for y in range(height):
        for x in range(width):
            if mask_pixels[x, y][3] != 0:
                continue
            neighbors = opaque_neighbor_colors(styled_pixels, mask_pixels, width, height, x, y)
            if not neighbors:
                continue
            base = min(neighbors, key=lambda color: luma(color[0], color[1], color[2]))
            amount = 0.62 if len(neighbors) >= 3 else 0.52
            result_pixels[x, y] = (*darken_color(base[0], base[1], base[2], amount), 255)
    return harden_transparency(result, 1)


def limit_palette(image: Image.Image, palette_colors: int, alpha_threshold: int) -> Image.Image:
    output = harden_transparency(image, alpha_threshold)
    if palette_colors <= 0:
        return output
    alpha = output.getchannel("A")
    rgb = Image.new("RGB", output.size, (0, 0, 0))
    rgb.paste(output.convert("RGB"), mask=alpha)
    rgb = rgb.quantize(colors=palette_colors, method=Image.Quantize.MEDIANCUT).convert("RGB")
    result = Image.merge("RGBA", (*rgb.split(), alpha))
    return clear_transparent_pixels(result)


def harden_transparency(image: Image.Image, alpha_threshold: int) -> Image.Image:
    resized = image.convert("RGBA")
    alpha = resized.getchannel("A").point(lambda value: 255 if value >= alpha_threshold else 0)
    output = Image.merge("RGBA", (*resized.convert("RGB").split(), alpha))
    return clear_transparent_pixels(output)


def clear_transparent_pixels(image: Image.Image) -> Image.Image:
    output = image.convert("RGBA")
    pixels = output.load()
    for y in range(output.height):
        for x in range(output.width):
            if pixels[x, y][3] == 0:
                pixels[x, y] = (0, 0, 0, 0)
    return output


def stable_seed(text: str) -> int:
    digest = hashlib.sha256(text.encode("utf-8")).digest()
    return int.from_bytes(digest[:4], "big", signed=False)


def pixel_noise(seed: int, x: int, y: int) -> int:
    value = seed ^ (x * 0x45D9F3B) ^ (y * 0x119DE1F3)
    value ^= value >> 16
    value *= 0x7FEB352D
    value ^= value >> 15
    return value & 0xFFFFFFFF


def boost_material_color(r: int, g: int, b: int) -> tuple[int, int, int]:
    value = luma(r, g, b)
    contrast = 1.20
    saturation = 1.30
    r = clamp(128 + (r - 128) * contrast)
    g = clamp(128 + (g - 128) * contrast)
    b = clamp(128 + (b - 128) * contrast)
    r = clamp(value + (r - value) * saturation)
    g = clamp(value + (g - value) * saturation)
    b = clamp(value + (b - value) * saturation)
    return r, g, b


def lighten_color(r: int, g: int, b: int, amount: float) -> tuple[int, int, int]:
    amount = max(0.0, min(1.0, amount))
    return (
        clamp(r + (255 - r) * amount),
        clamp(g + (255 - g) * amount),
        clamp(b + (255 - b) * amount),
    )


def darken_color(r: int, g: int, b: int, amount: float) -> tuple[int, int, int]:
    amount = max(0.0, min(1.0, amount))
    return (
        clamp(r * (1.0 - amount)),
        clamp(g * (1.0 - amount)),
        clamp(b * (1.0 - amount)),
    )


def luma(r: int, g: int, b: int) -> float:
    return r * 0.299 + g * 0.587 + b * 0.114


def clamp(value: float) -> int:
    return max(0, min(255, round(value)))


def is_transparent(pixels, width: int, height: int, x: int, y: int) -> bool:
    if x < 0 or y < 0 or x >= width or y >= height:
        return True
    return pixels[x, y][3] == 0


def count_transparent_neighbors(pixels, width: int, height: int, x: int, y: int) -> int:
    count = 0
    for yy in range(y - 1, y + 2):
        for xx in range(x - 1, x + 2):
            if xx == x and yy == y:
                continue
            if is_transparent(pixels, width, height, xx, yy):
                count += 1
    return count


def opaque_neighbor_colors(styled_pixels, mask_pixels, width: int, height: int, x: int, y: int) -> list[tuple[int, int, int]]:
    colors: list[tuple[int, int, int]] = []
    for yy in range(y - 1, y + 2):
        for xx in range(x - 1, x + 2):
            if xx == x and yy == y:
                continue
            if xx < 0 or yy < 0 or xx >= width or yy >= height:
                continue
            if mask_pixels[xx, yy][3] == 0:
                continue
            r, g, b, _ = styled_pixels[xx, yy]
            colors.append((r, g, b))
    return colors
