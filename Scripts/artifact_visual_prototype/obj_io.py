#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""读取 Blockbench 可导出的 OBJ，并用 sidecar 保存法宝锚点。"""

from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path
from typing import Iterable

from Scripts.artifact_compose.math3d import Vec3, add, dot, face_normal, normalize

from .types import MeshFace, ModelAsset


def load_model(key: str, obj_path: Path) -> ModelAsset:
    vertices: list[Vec3] = []
    raw_faces: list[tuple[tuple[int, ...], str, str, str]] = []
    object_name = key
    material = "metal"
    surface = "polished_metal"
    with obj_path.open("r", encoding="utf-8") as file:
        for line_number, raw in enumerate(file, 1):
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            command, _, payload = line.partition(" ")
            values = payload.split()
            if command == "v":
                if len(values) < 3:
                    raise ValueError(f"{obj_path}:{line_number} 顶点坐标不完整")
                vertices.append((float(values[0]), float(values[1]), float(values[2])))
            elif command in {"o", "g"} and payload:
                object_name = payload.strip()
            elif command == "usemtl" and payload:
                material, surface = _parse_material(payload.strip())
            elif command == "f":
                if len(values) < 3:
                    raise ValueError(f"{obj_path}:{line_number} 面至少需要三个顶点")
                indices = [_resolve_index(token, len(vertices), obj_path, line_number) for token in values]
                raw_faces.append((tuple(indices), material, surface, object_name))
    if not vertices or not raw_faces:
        raise ValueError(f"OBJ 没有有效几何体: {obj_path}")

    face_normals = [face_normal(tuple(vertices[index] for index in indices)) for indices, _, _, _ in raw_faces]
    # 生成器为便于导出会按面重复写顶点，因此按对象、表面和量化坐标连接平滑组。
    incidents: dict[tuple[str, str, Vec3], list[int]] = defaultdict(list)
    for face_index, (indices, _, face_surface, face_object) in enumerate(raw_faces):
        for point in {vertices[vertex_index] for vertex_index in indices}:
            incidents[(face_object, face_surface, point)].append(face_index)
    faces = []
    for face_index, (indices, face_material, face_surface, face_object) in enumerate(raw_faces):
        base = face_normals[face_index]
        normals = []
        for vertex_index in indices:
            smoothed = (0.0, 0.0, 0.0)
            for incident in incidents[(face_object, face_surface, vertices[vertex_index])]:
                candidate = face_normals[incident]
                if dot(base, candidate) >= 0.64:
                    smoothed = add(smoothed, candidate)
            normals.append(normalize(smoothed, base))
        faces.append(MeshFace(
            tuple(vertices[index] for index in indices),
            face_material,
            face_object,
            face_surface,
            tuple(normals),
        ))

    anchor_path = obj_path.with_suffix(".anchors.json")
    if not anchor_path.exists():
        raise FileNotFoundError(f"OBJ 缺少锚点 sidecar: {anchor_path}")
    with anchor_path.open("r", encoding="utf-8") as file:
        data = json.load(file)
    anchors = {
        str(name): _vec3(value, anchor_path, name)
        for name, value in data.get("anchors", {}).items()
    }
    if not anchors:
        raise ValueError(f"模型没有任何锚点: {anchor_path}")
    return ModelAsset(key, obj_path, tuple(faces), anchors)


def write_model(
    obj_path: Path,
    faces: Iterable[MeshFace],
    anchors: dict[str, Vec3],
) -> tuple[Path, Path]:
    obj_path.parent.mkdir(parents=True, exist_ok=True)
    faces = tuple(faces)
    material_path = obj_path.with_suffix(".mtl")
    lines = [
        "# Artifact visual prototype - Blockbench compatible OBJ",
        f"mtllib {material_path.name}",
        "s off",
    ]
    vertex_index = 1
    current_object = None
    current_material = None
    for face in faces:
        if face.object_name != current_object:
            current_object = face.object_name
            lines.append(f"o {_safe_name(current_object)}")
        material_name = _material_name(face)
        if material_name != current_material:
            current_material = material_name
            lines.append(f"usemtl {_safe_name(material_name)}")
        for x, y, z in face.points:
            lines.append(f"v {x:.6f} {y:.6f} {z:.6f}")
        indices = range(vertex_index, vertex_index + len(face.points))
        lines.append("f " + " ".join(str(index) for index in indices))
        vertex_index += len(face.points)
    obj_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    _write_material_library(material_path, {face.material for face in faces})

    anchor_path = obj_path.with_suffix(".anchors.json")
    anchor_path.write_text(
        json.dumps({"anchors": anchors}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return obj_path, anchor_path


def write_combined_obj(path: Path, groups: Iterable[tuple[str, Iterable[MeshFace]]]) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    groups = tuple((slot, tuple(faces)) for slot, faces in groups)
    material_path = path.with_suffix(".mtl")
    lines = ["# Composed artifact preview", f"mtllib {material_path.name}", "s off"]
    vertex_index = 1
    for slot, faces in groups:
        lines.append(f"o {_safe_name(slot)}")
        current_material = None
        for face in faces:
            material_name = _material_name(face)
            if material_name != current_material:
                current_material = material_name
                lines.append(f"usemtl {_safe_name(material_name)}")
            for x, y, z in face.points:
                lines.append(f"v {x:.6f} {y:.6f} {z:.6f}")
            indices = range(vertex_index, vertex_index + len(face.points))
            lines.append("f " + " ".join(str(index) for index in indices))
            vertex_index += len(face.points)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    _write_material_library(
        material_path,
        {face.material for _, faces in groups for face in faces},
    )
    return path


def _resolve_index(token: str, count: int, path: Path, line_number: int) -> int:
    raw = token.split("/", 1)[0]
    try:
        value = int(raw)
    except ValueError as exc:
        raise ValueError(f"{path}:{line_number} 非法 OBJ 索引 {token}") from exc
    index = value - 1 if value > 0 else count + value
    if index < 0 or index >= count:
        raise IndexError(f"{path}:{line_number} OBJ 索引越界 {token}")
    return index


def _vec3(value, path: Path, name: str) -> Vec3:
    if not isinstance(value, list) or len(value) != 3:
        raise ValueError(f"{path} 锚点 {name} 必须是三个数字")
    return float(value[0]), float(value[1]), float(value[2])


def _safe_name(value: str) -> str:
    return "".join(character if character.isalnum() or character in "_.-" else "_" for character in value)


def _parse_material(value: str) -> tuple[str, str]:
    if "__surface__" in value:
        material, surface = value.split("__surface__", 1)
        return material, surface
    return value, _default_surface(value)


def _default_surface(material: str) -> str:
    return {
        "jade": "jade",
        "crystal": "crystal",
        "surface": "crystal",
        "gem": "crystal",
        "glow": "emissive",
        "core": "emissive",
        "glint": "emissive",
        "cloth": "silk",
        "fold": "silk",
        "grip": "wood",
        "wood": "wood",
    }.get(material, "polished_metal")


def _material_name(face: MeshFace) -> str:
    return f"{face.material}__surface__{face.surface}"


def _write_material_library(path: Path, materials: set[str]) -> None:
    preview_colors = {
        "metal": (0.42, 0.52, 0.58),
        "trim": (0.78, 0.58, 0.25),
        "jade": (0.20, 0.62, 0.48),
        "crystal": (0.25, 0.68, 0.78),
        "grip": (0.30, 0.22, 0.19),
        "cloth": (0.58, 0.24, 0.38),
        "glow": (0.35, 0.92, 0.68),
        "dark": (0.10, 0.13, 0.16),
    }
    lines = ["# Preview-only materials; game colors are assigned by Instance palette"]
    for material in sorted(materials):
        color = preview_colors.get(material, (0.55, 0.55, 0.55))
        lines.extend((
            f"newmtl {_safe_name(material)}",
            f"Kd {color[0]:.3f} {color[1]:.3f} {color[2]:.3f}",
            "Ka 0.080 0.080 0.080",
            "Ks 0.150 0.150 0.150",
            "Ns 24.000",
            "d 1.000",
            "illum 2",
            "",
        ))
    path.write_text("\n".join(lines), encoding="utf-8")
