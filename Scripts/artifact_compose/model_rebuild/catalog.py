#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""遍历 AppearanceCatalog 并重建全部 variant 模型。"""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from pathlib import Path

from .builder import ModelBuilder, bounds
from .exporter import render_mtl, render_obj, write_variant
from .recipes_arcane import build_arcane
from .recipes_relics import build_relic
from .recipes_robe import build_robe
from .recipes_sword_seal import build_sword_seal
from .recipes_vessels import build_vessel


@dataclass(frozen=True)
class RebuildStats:
    modules: int
    variants: int
    faces: int
    changed_files: int


def rebuild_catalog(catalog: Path, *, check: bool = False) -> RebuildStats:
    catalog = catalog.resolve()
    surface_keys = load_surface_keys(catalog)
    module_count = 0
    variant_count = 0
    face_count = 0
    changed_files = 0
    seen_models: set[Path] = set()
    for module_path in sorted(catalog.glob("modules*.json")):
        data = json.loads(module_path.read_text("utf-8"))
        model_root = str(data.get("model_root", "Models"))
        for module in data.get("modules", []):
            module_count += 1
            module_key = str(module["key"])
            for variant in module.get("variants", []):
                variant_count += 1
                variant_key = str(variant["key"])
                mesh = build_model(module_key, variant_key)
                validate_model(module_key, variant_key, mesh, surface_keys)
                anchors = {
                    str(anchor["key"]): [float(value) for value in anchor["position"]]
                    for anchor in variant.get("anchors", [])
                }
                if not anchors:
                    raise ValueError(f"{module_key}.{variant_key} 没有锚点")
                relative = str(variant.get("model") or f"{model_root}/{module_key}/{variant_key}.obj")
                model_path = resolve_model_path(catalog, relative)
                if model_path in seen_models:
                    raise ValueError(f"模型输出路径重复: {model_path}")
                seen_models.add(model_path)
                face_count += len(mesh.faces)
                expected = {
                    model_path: render_obj(module_key, variant_key, model_path.with_suffix(".mtl").name, mesh.faces),
                    model_path.with_suffix(".mtl"): render_mtl(mesh.faces),
                    model_path.with_suffix(".anchors.json"): json.dumps(
                        {"anchors": anchors}, ensure_ascii=False, indent=2
                    ) + "\n",
                }
                mismatches = [
                    path for path, content in expected.items()
                    if not path.exists() or path.read_text("utf-8") != content
                ]
                changed_files += len(mismatches)
                if check:
                    continue
                write_variant(model_path, module_key, variant_key, mesh.faces, anchors)

    actual_models = set((catalog / "Models").rglob("*.obj"))
    extras = sorted(actual_models - seen_models)
    if extras:
        raise ValueError("存在未被 catalog 引用的模型: " + ", ".join(str(path) for path in extras[:5]))
    return RebuildStats(module_count, variant_count, face_count, changed_files)


def build_model(module_key: str, variant_key: str) -> ModelBuilder:
    for factory in (build_relic, build_sword_seal, build_vessel, build_robe, build_arcane):
        mesh = factory(module_key, variant_key)
        if mesh is not None:
            return mesh
    raise KeyError(f"没有重制配方: {module_key}.{variant_key}")


def validate_model(
    module_key: str,
    variant_key: str,
    mesh: ModelBuilder,
    surface_keys: set[str],
) -> None:
    identity = f"{module_key}.{variant_key}"
    if len(mesh.faces) < 8:
        raise ValueError(f"{identity} 几何过少: {len(mesh.faces)} faces")
    if len(mesh.faces) > 5000:
        raise ValueError(f"{identity} 几何过多: {len(mesh.faces)} faces")
    object_names = set()
    for face in mesh.faces:
        if len(face.points) < 3:
            raise ValueError(f"{identity} 存在退化面")
        if not face.material or not face.object_name:
            raise ValueError(f"{identity} 存在空材质或空对象名")
        if face.surface not in surface_keys:
            raise ValueError(f"{identity} 使用未知表面 {face.surface}")
        object_names.add(face.object_name)
        for point in face.points:
            if len(point) != 3 or not all(math.isfinite(value) for value in point):
                raise ValueError(f"{identity} 存在非法顶点 {point}")
    if len(object_names) < 2:
        raise ValueError(f"{identity} 缺少独立结构或装饰对象")
    low, high = bounds(mesh.faces)
    if max(high[axis] - low[axis] for axis in range(3)) <= 0.05:
        raise ValueError(f"{identity} 模型尺寸异常")


def load_surface_keys(catalog: Path) -> set[str]:
    keys = {
        str(style["key"])
        for path in sorted(catalog.glob("surfaces*.json"))
        for style in json.loads(path.read_text("utf-8")).get("styles", [])
    }
    if not keys:
        raise ValueError("AppearanceCatalog 没有 surface style")
    return keys


def resolve_model_path(catalog: Path, relative: str) -> Path:
    path = (catalog / relative).resolve()
    try:
        path.relative_to(catalog)
    except ValueError as error:
        raise ValueError(f"模型路径越过 AppearanceCatalog: {relative}") from error
    if path.suffix.lower() != ".obj":
        raise ValueError(f"模型必须是 OBJ: {relative}")
    return path
