#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把重制模型写为运行时 OBJ、预览 MTL 与锚点 sidecar。"""

from __future__ import annotations

import json
import re
from pathlib import Path

from .builder import StyledFace


SURFACE_SEPARATOR = "__surface__"
SURFACE_COLORS: dict[str, tuple[float, float, float]] = {
    "neutral": (0.55, 0.57, 0.60),
    "polished_metal": (0.72, 0.68, 0.52),
    "aged_metal": (0.42, 0.34, 0.24),
    "jade": (0.24, 0.66, 0.46),
    "crystal": (0.35, 0.76, 0.86),
    "emissive": (0.48, 0.96, 0.68),
    "silk": (0.63, 0.24, 0.38),
    "stone": (0.43, 0.46, 0.50),
    "wood": (0.36, 0.22, 0.14),
    "bone": (0.76, 0.72, 0.58),
}
ROLE_TINTS: dict[str, tuple[float, float, float]] = {
    "edge": (1.12, 1.05, 0.82),
    "rim": (1.06, 0.88, 0.56),
    "ridge": (0.88, 0.78, 0.60),
    "gem": (0.55, 1.12, 0.92),
    "core": (0.78, 1.15, 0.90),
    "glint": (1.12, 1.12, 1.12),
    "fire": (1.18, 0.72, 0.30),
    "water": (0.62, 0.92, 1.15),
    "fold": (0.72, 0.74, 0.80),
    "left": (0.78, 0.84, 0.92),
    "right": (0.62, 0.70, 0.82),
}


def write_variant(
    path: Path,
    module_key: str,
    variant_key: str,
    faces: list[StyledFace],
    anchors: dict[str, list[float]],
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(render_obj(module_key, variant_key, path.with_suffix(".mtl").name, faces), "utf-8")
    path.with_suffix(".mtl").write_text(render_mtl(faces), "utf-8")
    path.with_suffix(".anchors.json").write_text(
        json.dumps({"anchors": anchors}, ensure_ascii=False, indent=2) + "\n",
        "utf-8",
    )


def render_obj(module_key: str, variant_key: str, mtl_name: str, faces: list[StyledFace]) -> str:
    lines = [
        "# Cultiway handcrafted low-poly artifact model",
        f"# module: {module_key}",
        f"# variant: {variant_key}",
        f"mtllib {mtl_name}",
        "s off",
    ]
    vertex_indices: dict[tuple[float, float, float], int] = {}
    vertices: list[tuple[float, float, float]] = []
    rows: list[tuple[str, str, tuple[int, ...]]] = []
    for face in faces:
        indices: list[int] = []
        for point in face.points:
            normalized = tuple(round(float(value), 6) for value in point)
            index = vertex_indices.get(normalized)
            if index is None:
                vertices.append(normalized)
                index = len(vertices)
                vertex_indices[normalized] = index
            indices.append(index)
        rows.append((safe_name(face.object_name), material_name(face.material, face.surface), tuple(indices)))
    lines.extend(f"v {x:.6f} {y:.6f} {z:.6f}" for x, y, z in vertices)
    current_object = None
    current_material = None
    for object_name, material, indices in rows:
        if object_name != current_object:
            current_object = object_name
            lines.append(f"o {object_name}")
        if material != current_material:
            current_material = material
            lines.append(f"usemtl {material}")
        lines.append("f " + " ".join(str(index) for index in indices))
    return "\n".join(lines) + "\n"


def render_mtl(faces: list[StyledFace]) -> str:
    materials = sorted({(face.material, face.surface) for face in faces})
    lines = ["# Preview colors only; ArtifactAppearance supplies final Instance colors", ""]
    for role, surface in materials:
        base = SURFACE_COLORS[surface]
        tint = ROLE_TINTS.get(role, (1.0, 1.0, 1.0))
        color = tuple(min(1.0, base[index] * tint[index]) for index in range(3))
        name = material_name(role, surface)
        lines.extend((
            f"newmtl {name}",
            f"Ka {color[0] * 0.16:.4f} {color[1] * 0.16:.4f} {color[2] * 0.16:.4f}",
            f"Kd {color[0]:.4f} {color[1]:.4f} {color[2]:.4f}",
            "Ks 0.2200 0.2200 0.2200",
            "Ns 32.0000",
            "d 1.0000",
            "illum 2",
            "",
        ))
    return "\n".join(lines)


def material_name(role: str, surface: str) -> str:
    return safe_name(f"{role}{SURFACE_SEPARATOR}{surface}")


def safe_name(value: str) -> str:
    value = re.sub(r"[^0-9A-Za-z_.-]+", "_", value).strip("_")
    return value or "unnamed"
