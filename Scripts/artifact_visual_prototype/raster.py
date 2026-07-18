#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""带平滑法线与超采样的确定性正交光栅器。"""

from __future__ import annotations

import math
from collections.abc import Sequence

from Scripts.artifact_compose.math3d import (
    Vec3,
    add,
    dot,
    face_normal,
    mul,
    normalize,
    rotate_euler,
    sub,
)

from .palette import PaletteTheme
from .surface_styles import get_surface_style
from .types import BakeLayer, MeshFace, ModelAsset, Placement, ResolvedView, ViewSpec


ProjectedPoint = tuple[float, float, float]
Triangle = tuple[ProjectedPoint, ProjectedPoint, ProjectedPoint]
NormalTriangle = tuple[Vec3, Vec3, Vec3]

def transform_model(model: ModelAsset, placement: Placement) -> tuple[MeshFace, ...]:
    try:
        anchor = model.anchors[placement.anchor]
    except KeyError as exc:
        raise KeyError(
            f"模型 {model.key} 缺少模板槽位 {placement.slot} 使用的锚点 {placement.anchor}"
        ) from exc

    faces: list[MeshFace] = []
    for face in model.faces:
        points = tuple(
            add(
                rotate_euler(mul(sub(point, anchor), placement.scale), placement.rotation),
                placement.position,
            )
            for point in face.points
        )
        source_normals = face.normals if len(face.normals) == len(face.points) else (
            (face_normal(face.points),) * len(face.points)
        )
        normals = tuple(
            _transform_normal(normal, placement.scale, placement.rotation)
            for normal in source_normals
        )
        faces.append(MeshFace(points, face.material, face.object_name, face.surface, normals))
    return tuple(faces)


def resolve_view(faces: Sequence[MeshFace], spec: ViewSpec) -> ResolvedView:
    points = [point for face in faces for point in face.points]
    if not points:
        raise ValueError(f"视图 {spec.key} 没有可取景的几何体")
    bounds_target = tuple(
        (min(point[axis] for point in points) + max(point[axis] for point in points)) * 0.5
        for axis in range(3)
    )
    if not spec.auto_frame:
        target = spec.target or bounds_target
        if spec.fixed_scale <= 0:
            raise ValueError(f"固定取景 {spec.key} 缺少有效 scale")
        return ResolvedView(spec, target, spec.fixed_scale)
    target = bounds_target
    camera_points = [rotate_euler(sub(point, target), spec.rotation) for point in points]
    width = max(point[0] for point in camera_points) - min(point[0] for point in camera_points)
    height = max(point[1] for point in camera_points) - min(point[1] for point in camera_points)
    available = max(1.0, spec.size - spec.margin * 2 - 1)
    scale = available / max(width, height, 1e-6)
    return ResolvedView(spec, target, scale)


def bake_layer(
    slot: str,
    order: int,
    faces: Sequence[MeshFace],
    view: ResolvedView,
    theme: PaletteTheme,
) -> BakeLayer:
    sample_scale = max(1, view.spec.supersample)
    size = view.spec.size * sample_scale
    layer = BakeLayer(size, size, slot, order, sample_scale)
    light = _light_direction(view.spec.light_yaw, view.spec.light_pitch)
    camera_light = normalize(rotate_euler(light, view.spec.rotation))
    camera_half = normalize(add(camera_light, (0.0, 0.0, 1.0)))
    for face in faces:
        if len(face.points) < 3:
            continue
        camera_points = tuple(
            rotate_euler(sub(point, view.target), view.spec.rotation)
            for point in face.points
        )
        flat_world = face_normal(face.points)
        flat_camera = face_normal(camera_points)
        world_normals = face.normals if len(face.normals) == len(face.points) else (
            (flat_world,) * len(face.points)
        )
        camera_normals = tuple(normalize(rotate_euler(normal, view.spec.rotation)) for normal in world_normals)
        if flat_camera[2] < 0:
            world_normals = tuple(mul(normal, -1.0) for normal in world_normals)
            camera_normals = tuple(mul(normal, -1.0) for normal in camera_normals)
        projected = tuple(_project(point, view, sample_scale) for point in camera_points)
        emission = max(theme.emission_for(face.material), 3 if face.surface == "emissive" else 0)
        for index in range(1, len(projected) - 1):
            triangle = (projected[0], projected[index], projected[index + 1])
            normal_world = (world_normals[0], world_normals[index], world_normals[index + 1])
            normal_camera = (camera_normals[0], camera_normals[index], camera_normals[index + 1])
            _raster_triangle(
                triangle,
                (face.points[0], face.points[index], face.points[index + 1]),
                normal_world,
                normal_camera,
                light,
                camera_half,
                face.material,
                face.surface,
                face.object_name,
                emission,
                layer,
            )
    return layer


def _transform_normal(normal: Vec3, scale: Vec3, rotation: Vec3) -> Vec3:
    adjusted = tuple(
        normal[axis] / scale[axis] if abs(scale[axis]) > 1e-8 else normal[axis]
        for axis in range(3)
    )
    return normalize(rotate_euler(adjusted, rotation))


def _project(point: Vec3, view: ResolvedView, sample_scale: int) -> ProjectedPoint:
    size = view.spec.size * sample_scale
    center = (size - 1) * 0.5
    scale = view.scale * sample_scale
    return center + point[0] * scale, center - point[1] * scale, point[2]


def _light_amount(
    surface: str,
    normal: Vec3,
    camera_normal: Vec3,
    light: Vec3,
    camera_half: Vec3,
) -> float:
    style = get_surface_style(surface)
    normal = normalize(normal)
    camera_normal = normalize(camera_normal)
    diffuse = max(0.0, dot(normal, light))
    side_shadow = max(0.0, -normal[0] * 0.18 - normal[2] * 0.10)
    facing = max(0.0, min(1.0, camera_normal[2]))
    specular = max(0.0, dot(camera_normal, camera_half)) ** style.specular_power * style.specular
    rim = (1.0 - facing) ** 2 * style.rim_light
    amount = 0.24 + diffuse * style.diffuse - side_shadow * style.side_shadow * 0.18
    amount += style.brightness + specular + rim
    if style.emission_layer > 0:
        amount = max(amount, 0.74)
    return max(0.0, min(1.0, amount))


def _light_direction(yaw_degrees: float, pitch_degrees: float) -> Vec3:
    yaw = math.radians(yaw_degrees)
    pitch = math.radians(pitch_degrees)
    return normalize((
        math.cos(pitch) * math.sin(yaw),
        math.sin(pitch),
        math.cos(pitch) * math.cos(yaw),
    ))


def _raster_triangle(
    triangle: Triangle,
    positions: tuple[Vec3, Vec3, Vec3],
    normal_world: NormalTriangle,
    normal_camera: NormalTriangle,
    light: Vec3,
    camera_half: Vec3,
    material: str,
    surface: str,
    object_name: str,
    emission: int,
    layer: BakeLayer,
) -> None:
    xs = [point[0] for point in triangle]
    ys = [point[1] for point in triangle]
    min_x = max(0, math.floor(min(xs)))
    max_x = min(layer.width - 1, math.ceil(max(xs)))
    min_y = max(0, math.floor(min(ys)))
    max_y = min(layer.height - 1, math.ceil(max(ys)))
    area = _edge(triangle[0], triangle[1], triangle[2])
    if abs(area) < 1e-7:
        return
    for y in range(min_y, max_y + 1):
        for x in range(min_x, max_x + 1):
            sample = (x + 0.5, y + 0.5, 0.0)
            w0 = _edge(triangle[1], triangle[2], sample) / area
            w1 = _edge(triangle[2], triangle[0], sample) / area
            w2 = _edge(triangle[0], triangle[1], sample) / area
            if w0 < -1e-6 or w1 < -1e-6 or w2 < -1e-6:
                continue
            depth = triangle[0][2] * w0 + triangle[1][2] * w1 + triangle[2][2] * w2
            target = y * layer.width + x
            if depth <= layer.depth[target]:
                continue
            world = normalize(tuple(
                normal_world[0][axis] * w0 + normal_world[1][axis] * w1 + normal_world[2][axis] * w2
                for axis in range(3)
            ))
            camera = normalize(tuple(
                normal_camera[0][axis] * w0 + normal_camera[1][axis] * w1 + normal_camera[2][axis] * w2
                for axis in range(3)
            ))
            amount = _light_amount(surface, world, camera, light, camera_half)
            position = tuple(
                positions[0][axis] * w0 + positions[1][axis] * w1 + positions[2][axis] * w2
                for axis in range(3)
            )
            layer.depth[target] = depth
            layer.materials[target] = material
            layer.surfaces[target] = surface
            layer.objects[target] = object_name
            layer.positions[target] = position
            layer.lights[target] = amount
            layer.shades[target] = max(0, min(5, int(amount * 5.0 + 0.5)))
            layer.emissions[target] = emission


def _edge(a: ProjectedPoint, b: ProjectedPoint, c: ProjectedPoint) -> float:
    return (c[0] - a[0]) * (b[1] - a[1]) - (c[1] - a[1]) * (b[0] - a[0])
