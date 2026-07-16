#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""3D 法器软件渲染器：正交相机、z-buffer、像素描边。"""

from __future__ import annotations

import math
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

from PIL import Image

from .colors import parse_hex_color
from .compose3d import ArtifactInstance3D, build_world_faces, split_instance_material_key
from .math3d import Vec3, dot, face_normal, normalize, rotate_euler, sub, v3
from .mesh3d import Face3D
from .models3d import SurfaceStyle3D
from .render import clear_transparent_pixels, darken_color, lighten_color, save_preview, stable_seed


@dataclass(frozen=True)
class ProjectedFace:
    points: tuple[tuple[float, float, float], ...]
    world_points: tuple[Vec3, ...]
    normal_world: Vec3
    material: str
    surface: SurfaceStyle3D
    color: tuple[int, int, int]


def render_png3d(
    instance: ArtifactInstance3D,
    output_path: Path,
    size: int = 28,
    palette_colors: int = 0,
) -> Image.Image:
    faces = build_world_faces(instance)
    image = render_faces(faces, instance, size)
    if palette_colors > 0:
        image = limit_palette3d(image, palette_colors)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(output_path)
    return image


def render_faces(faces: Sequence[Face3D], instance: ArtifactInstance3D, size: int) -> Image.Image:
    camera = instance.template.camera
    scale = float(camera.get("scale", 8.0))
    target = v3(camera.get("target", [0, 0, 0]))
    rotation = (
        -float(camera.get("pitch", 0)),
        -float(camera.get("yaw", 0)),
        -float(camera.get("roll", 0)),
    )
    light = light_direction(instance.template.light)
    seed = stable_seed(f"{instance.seed}|{instance.template.key}|{instance.sample_index}|3d")

    projected = [
        project_face(face, instance, target, rotation, scale, size, light, seed)
        for face in faces
    ]
    projected = [face for face in projected if len(face.points) >= 3]

    pixels = [(0, 0, 0, 0) for _ in range(size * size)]
    depth = [-1e9 for _ in range(size * size)]
    material_marks = ["" for _ in range(size * size)]

    for face in projected:
        triangles = triangulate(face.points)
        for tri in triangles:
            raster_triangle(tri, face, pixels, depth, material_marks, size, seed)

    image = Image.new("RGBA", (size, size))
    image.putdata(pixels)
    image = add_depth_edges(image, depth, size)
    image = add_outer_outline(image)
    return clear_transparent_pixels(image)


def project_face(
    face: Face3D,
    instance: ArtifactInstance3D,
    target: Vec3,
    camera_rotation: Vec3,
    scale: float,
    size: int,
    light: Vec3,
    seed: int,
) -> ProjectedFace:
    camera_points: list[Vec3] = []
    for point in face.points:
        camera_points.append(rotate_euler(sub(point, target), camera_rotation))
    normal_world = face_normal(face.points)
    normal_camera = face_normal(camera_points)
    if normal_camera[2] < 0:
        normal_world = (-normal_world[0], -normal_world[1], -normal_world[2])

    _, material = split_instance_material_key(face.material)
    color = resolve_material_color(face.material, instance)
    surface = resolve_surface_style(face.surface, instance)
    shaded = shade_color(color, normal_world, light, surface)
    projected = tuple(
        (
            size * 0.5 + point[0] * scale,
            size * 0.5 - point[1] * scale,
            point[2],
        )
        for point in camera_points
    )
    return ProjectedFace(projected, tuple(face.points), normal_world, material, surface, shaded)


def resolve_material_color(material_key: str, instance: ArtifactInstance3D) -> tuple[int, int, int]:
    slot, material = split_instance_material_key(material_key)
    for module in instance.modules:
        if slot is not None and module.placement.slot != slot:
            continue
        if material in module.colors:
            color = parse_hex_color(module.colors[material])
            return color.r, color.g, color.b
    fallback = parse_hex_color("#9aa0a8")
    return fallback.r, fallback.g, fallback.b


def shade_color(
    color: tuple[int, int, int],
    normal: Vec3,
    light: Vec3,
    surface: SurfaceStyle3D,
) -> tuple[int, int, int]:
    normal = normalize(normal)
    diffuse = max(0.0, dot(normal, light))
    side_dark = max(0.0, -normal[0] * 0.18 + -normal[2] * 0.10)
    amount = (
        0.52 + diffuse * surface.diffuse - side_dark * surface.side_shadow
        + surface.brightness + surface.emission
    )
    amount = max(0.22, min(1.08, amount))
    r, g, b = color
    if amount >= 1.0:
        r, g, b = lighten_color(r, g, b, (amount - 1.0) * 0.75)
    else:
        r, g, b = darken_color(r, g, b, 1.0 - amount)
    if surface.emission > 0 and diffuse > 0.25:
        r, g, b = lighten_color(r, g, b, min(0.3, surface.emission * 0.7))
    return r, g, b


def resolve_surface_style(surface: str, instance: ArtifactInstance3D) -> SurfaceStyle3D:
    return instance.surface_styles.get(
        surface,
        instance.surface_styles.get("neutral", SurfaceStyle3D("neutral")),
    )


def light_direction(light: dict) -> Vec3:
    yaw = math.radians(float(light.get("yaw", -35)))
    pitch = math.radians(float(light.get("pitch", 55)))
    x = math.cos(pitch) * math.sin(yaw)
    y = math.sin(pitch)
    z = math.cos(pitch) * math.cos(yaw)
    return normalize((x, y, z))


def triangulate(points: Sequence[tuple[float, float, float]]) -> list[tuple[tuple[float, float, float], tuple[float, float, float], tuple[float, float, float]]]:
    if len(points) < 3:
        return []
    return [(points[0], points[i], points[i + 1]) for i in range(1, len(points) - 1)]


def raster_triangle(triangle, face: ProjectedFace, pixels, depth, material_marks, size: int, seed: int) -> None:
    xs = [p[0] for p in triangle]
    ys = [p[1] for p in triangle]
    min_x = max(0, math.floor(min(xs)))
    max_x = min(size - 1, math.ceil(max(xs)))
    min_y = max(0, math.floor(min(ys)))
    max_y = min(size - 1, math.ceil(max(ys)))
    area = edge(triangle[0], triangle[1], triangle[2])
    if abs(area) < 1e-6:
        return
    for y in range(min_y, max_y + 1):
        for x in range(min_x, max_x + 1):
            sample = (x + 0.5, y + 0.5, 0.0)
            w0 = edge(triangle[1], triangle[2], sample) / area
            w1 = edge(triangle[2], triangle[0], sample) / area
            w2 = edge(triangle[0], triangle[1], sample) / area
            if w0 < -1e-5 or w1 < -1e-5 or w2 < -1e-5:
                continue
            z = triangle[0][2] * w0 + triangle[1][2] * w1 + triangle[2][2] * w2
            index = y * size + x
            if z <= depth[index]:
                continue
            r, g, b = add_face_texture(
                face.color,
                face.surface,
                x,
                y,
                seed,
            )
            pixels[index] = (r, g, b, 255)
            depth[index] = z
            material_marks[index] = face.material


def edge(a, b, c) -> float:
    return (c[0] - a[0]) * (b[1] - a[1]) - (c[1] - a[1]) * (b[0] - a[0])


def add_face_texture(
    color: tuple[int, int, int],
    surface: SurfaceStyle3D,
    x: int,
    y: int,
    seed: int,
) -> tuple[int, int, int]:
    r, g, b = color
    value = ((x * 73) ^ (y * 151) ^ seed) & 0xFF
    if surface.texture_frequency > 0 and (x + y + seed) % surface.texture_frequency == 0:
        amount = surface.texture_dark * (0.7 + (value % 4) * 0.1)
        r, g, b = darken_color(r, g, b, amount)
    elif surface.texture_light > 0 and (
        (x * 2 - y + seed) % max(3, surface.texture_frequency + 3) == 0
    ):
        r, g, b = lighten_color(r, g, b, surface.texture_light)
    if surface.sparkle_frequency > 0 and (x - y + seed) % surface.sparkle_frequency == 0:
        r, g, b = lighten_color(r, g, b, max(surface.texture_light, 0.08))
    return r, g, b


def add_depth_edges(image: Image.Image, depth: list[float], size: int) -> Image.Image:
    pixels = image.load()
    result = image.copy()
    out = result.load()
    for y in range(size):
        for x in range(size):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            index = y * size + x
            current = depth[index]
            edge_amount = 0.0
            for dx, dy in ((1, 0), (0, 1)):
                xx = x + dx
                yy = y + dy
                if xx >= size or yy >= size:
                    continue
                other = depth[yy * size + xx]
                if other <= -1e8:
                    continue
                diff = abs(current - other)
                if diff > 0.12:
                    edge_amount = max(edge_amount, min(0.22, diff * 0.13))
            if edge_amount > 0:
                out[x, y] = (*darken_color(r, g, b, edge_amount), 255)
    return result


def add_outer_outline(image: Image.Image) -> Image.Image:
    pixels = image.load()
    result = image.copy()
    out = result.load()
    width, height = image.size
    for y in range(height):
        for x in range(width):
            if pixels[x, y][3] != 0:
                continue
            colors: list[tuple[int, int, int]] = []
            for yy in range(y - 1, y + 2):
                for xx in range(x - 1, x + 2):
                    if xx < 0 or yy < 0 or xx >= width or yy >= height:
                        continue
                    r, g, b, a = pixels[xx, yy]
                    if a:
                        colors.append((r, g, b))
            if not colors:
                continue
            base = min(colors, key=lambda item: item[0] * 0.299 + item[1] * 0.587 + item[2] * 0.114)
            out[x, y] = (*darken_color(base[0], base[1], base[2], 0.50), 255)
    return result


def limit_palette3d(image: Image.Image, palette_colors: int) -> Image.Image:
    alpha = image.getchannel("A")
    rgb = Image.new("RGB", image.size, (0, 0, 0))
    rgb.paste(image.convert("RGB"), mask=alpha)
    rgb = rgb.quantize(colors=palette_colors, method=Image.Quantize.MEDIANCUT).convert("RGB")
    result = Image.merge("RGBA", (*rgb.split(), alpha))
    return clear_transparent_pixels(result)


__all__ = ["render_png3d", "save_preview"]
