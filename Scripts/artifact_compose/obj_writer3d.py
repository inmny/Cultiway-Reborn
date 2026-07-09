#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""导出 3D 组合结果为 OBJ/MTL。"""

from __future__ import annotations

import re
from pathlib import Path

from .colors import parse_hex_color
from .compose3d import ArtifactInstance3D, transform_module_point
from .math3d import face_normal
from .mesh3d import part_faces


def write_obj3d(instance: ArtifactInstance3D, obj_path: Path) -> tuple[Path, Path]:
    obj_path.parent.mkdir(parents=True, exist_ok=True)
    mtl_path = obj_path.with_suffix(".mtl")
    mtl_name = mtl_path.name

    obj_lines: list[str] = [
        f"# Cultiway artifact compose 3D OBJ",
        f"# template: {instance.template.key}",
        f"# seed: {instance.seed}",
        f"# sample: {instance.sample_index}",
        f"mtllib {mtl_name}",
        "",
    ]
    material_colors: dict[str, tuple[float, float, float]] = {}
    vertex_index = 1
    normal_index = 1

    for module in instance.modules:
        obj_lines.append(f"o {sanitize_obj_name(module.placement.slot)}")
        obj_lines.append(f"g {sanitize_obj_name(module.id)}")
        for part_index, part in enumerate(module.variant.parts):
            material = str(part.get("material", "main"))
            material_name = sanitize_obj_name(f"{module.placement.slot}_{material}")
            material_colors.setdefault(material_name, resolve_material_rgb(module.colors, material))
            for face in part_faces(part):
                world_points = [transform_module_point(point, module) for point in face.points]
                if len(world_points) < 3:
                    continue
                normal = face_normal(world_points)
                obj_lines.append(f"usemtl {material_name}")
                obj_lines.append(f"s off")
                for point in world_points:
                    obj_lines.append(f"v {point[0]:.6f} {point[1]:.6f} {point[2]:.6f}")
                obj_lines.append(f"vn {normal[0]:.6f} {normal[1]:.6f} {normal[2]:.6f}")
                indices = [f"{vertex_index + i}//{normal_index}" for i in range(len(world_points))]
                obj_lines.append("f " + " ".join(indices))
                vertex_index += len(world_points)
                normal_index += 1
        obj_lines.append("")

    obj_path.write_text("\n".join(obj_lines) + "\n", encoding="utf-8")
    write_mtl(mtl_path, material_colors)
    return obj_path, mtl_path


def write_mtl(mtl_path: Path, material_colors: dict[str, tuple[float, float, float]]) -> None:
    lines: list[str] = ["# Cultiway artifact compose 3D MTL", ""]
    for name, color in sorted(material_colors.items()):
        r, g, b = color
        lines.extend([
            f"newmtl {name}",
            f"Ka {r * 0.25:.6f} {g * 0.25:.6f} {b * 0.25:.6f}",
            f"Kd {r:.6f} {g:.6f} {b:.6f}",
            "Ks 0.180000 0.180000 0.180000",
            "Ns 18.000000",
            "d 1.000000",
            "illum 2",
            "",
        ])
    mtl_path.write_text("\n".join(lines), encoding="utf-8")


def resolve_material_rgb(palette: dict[str, str], material: str) -> tuple[float, float, float]:
    color = parse_hex_color(palette.get(material, "#9aa0a8"))
    return color.r / 255.0, color.g / 255.0, color.b / 255.0


def sanitize_obj_name(value: str) -> str:
    text = re.sub(r"[^0-9A-Za-z_]+", "_", value)
    text = text.strip("_")
    return text or "unnamed"
