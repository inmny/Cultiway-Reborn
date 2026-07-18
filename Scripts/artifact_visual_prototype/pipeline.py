#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Blockbench 模型到图标、世界实体图层的完整离线验证管线。"""

from __future__ import annotations

import hashlib
import json
import shutil
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw

from .compositor import FrameImages, compose_layers, debug_layer_image
from .demo import build_demo_catalog, build_demo_instances
from .obj_io import write_combined_obj
from .palette import get_theme
from .raster import bake_layer, resolve_view, transform_model
from .types import BakeLayer, MeshFace, PrototypeCatalog, VisualInstance


@dataclass(frozen=True)
class ViewRecord:
    instance: str
    view: str
    size: int
    opaque_pixels: int
    opaque_bounds: tuple[int, int, int, int]
    semantic_hash: str
    png_hash: str
    body_path: Path
    preview_path: Path


@dataclass(frozen=True)
class PrototypeReport:
    output: Path
    records: tuple[ViewRecord, ...]
    manifest_path: Path
    contact_sheet_path: Path


def run_prototype(output: Path, clean: bool = True) -> PrototypeReport:
    output = output.resolve()
    if clean and output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)

    model_directory = output / "blockbench_models"
    catalog = build_demo_catalog(model_directory)
    instances = build_demo_instances(catalog)
    records: list[ViewRecord] = []
    manifests: list[dict] = []
    for instance in instances:
        instance_records, instance_manifest = _render_instance(catalog, instance, output)
        records.extend(instance_records)
        manifests.append(instance_manifest)

    _validate_lod(records)
    contact_sheet_path = _write_contact_sheet(output / "comparison_sheet.png", records)
    blockbench_path = _find_blockbench()
    manifest = {
        "prototype": "artifact_visual_blockbench_pipeline_v1",
        "deterministic": True,
        "blockbench_exchange": {
            "detected": blockbench_path is not None,
            "executable": str(blockbench_path) if blockbench_path else None,
            "format": "OBJ+MTL",
            "anchor_sidecar": "*.anchors.json",
            "generated_models": len(catalog.models),
            "roundtrip_loaded_faces": sum(len(model.faces) for model in catalog.models.values()),
        },
        "rules": {
            "template_owns": ["module slots", "anchor keys", "placement transforms", "views", "lights"],
            "module_owns": ["variant collection", "shared anchor key contract"],
            "variant_owns": ["fixed model", "free anchor coordinates"],
            "instance_owns": ["template", "selected variants", "global palette", "optional icon override"],
            "outlines": "applied once after all module depth layers are composed",
        },
        "instances": manifests,
    }
    manifest_path = output / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return PrototypeReport(output, tuple(records), manifest_path, contact_sheet_path)


def _render_instance(
    catalog: PrototypeCatalog,
    instance: VisualInstance,
    output: Path,
) -> tuple[list[ViewRecord], dict]:
    instance_directory = output / instance.key
    instance_directory.mkdir(parents=True, exist_ok=True)
    theme = get_theme(instance.palette_key)
    selected = _resolve_instance_faces(catalog, instance)
    all_faces = tuple(face for _, faces in selected for face in faces)
    write_combined_obj(instance_directory / "assembled.obj", selected)

    records: list[ViewRecord] = []
    view_manifests: list[dict] = []
    for view_spec in instance.template.views:
        view = resolve_view(all_faces, view_spec)
        layers = [
            bake_layer(slot, order, faces, view, theme)
            for (slot, faces), order in zip(
                selected,
                (placement.order for placement in instance.template.placements),
            )
        ]
        frame = compose_layers(layers, theme)
        repeated = compose_layers(layers, theme)
        _assert_deterministic(instance.key, view_spec.key, frame, repeated)

        view_directory = instance_directory / view_spec.key
        debug_directory = view_directory / "modules"
        debug_directory.mkdir(parents=True, exist_ok=True)
        for layer in layers:
            debug_layer_image(layer, theme).save(debug_directory / f"{layer.slot}.png")

        body = frame.body
        if instance.icon_override is not None and view_spec.key.startswith("icon"):
            body = Image.open(instance.icon_override).convert("RGBA").resize(
                (view_spec.size, view_spec.size),
                Image.Resampling.NEAREST,
            )
        body_path = view_directory / "body.png"
        emission_path = view_directory / "emission.png"
        shadow_path = view_directory / "shadow.png"
        mask_path = view_directory / "coverage.png"
        preview_path = view_directory / "preview.png"
        body.save(body_path)
        frame.emission.save(emission_path)
        frame.shadow.save(shadow_path)
        frame.mask.save(mask_path)
        preview = _compose_preview(body, frame.emission, frame.shadow)
        preview.save(preview_path)

        record = ViewRecord(
            instance.key,
            view_spec.key,
            view_spec.size,
            frame.opaque_pixels,
            frame.mask.getbbox() or (0, 0, 0, 0),
            frame.source_hash,
            _image_hash(body, frame.emission, frame.shadow),
            body_path,
            preview_path,
        )
        records.append(record)
        view_manifests.append({
            "key": record.view,
            "size": record.size,
            "opaque_pixels": record.opaque_pixels,
            "opaque_bounds": list(record.opaque_bounds),
            "semantic_hash": record.semantic_hash,
            "png_hash": record.png_hash,
            "target": list(view.target),
            "pixels_per_model_unit": round(view.scale, 6),
            "outputs": {
                "body": str(body_path.relative_to(output)).replace("\\", "/"),
                "emission": str(emission_path.relative_to(output)).replace("\\", "/"),
                "shadow": str(shadow_path.relative_to(output)).replace("\\", "/"),
                "coverage": str(mask_path.relative_to(output)).replace("\\", "/"),
            },
        })
    return records, {
        "key": instance.key,
        "template": instance.template.key,
        "shape": instance.template.shape,
        "palette": instance.palette_key,
        "variants": instance.variants,
        "icon_override": str(instance.icon_override) if instance.icon_override else None,
        "assembled_obj": str((instance_directory / "assembled.obj").relative_to(output)).replace("\\", "/"),
        "views": view_manifests,
    }


def _resolve_instance_faces(
    catalog: PrototypeCatalog,
    instance: VisualInstance,
) -> list[tuple[str, tuple[MeshFace, ...]]]:
    expected_slots = {placement.slot for placement in instance.template.placements}
    actual_slots = set(instance.variants)
    if expected_slots != actual_slots:
        missing = sorted(expected_slots - actual_slots)
        extra = sorted(actual_slots - expected_slots)
        raise ValueError(f"Instance {instance.key} 的 variant 槽位不匹配: missing={missing}, extra={extra}")
    selected = []
    for placement in instance.template.placements:
        module = catalog.modules[placement.module_key]
        variant = module.get_variant(instance.variants[placement.slot])
        model = catalog.models[variant.model_key]
        selected.append((placement.slot, transform_model(model, placement)))
    return selected


def _assert_deterministic(
    instance: str,
    view: str,
    first: FrameImages,
    second: FrameImages,
) -> None:
    equal = (
        first.source_hash == second.source_hash
        and first.body.tobytes() == second.body.tobytes()
        and first.emission.tobytes() == second.emission.tobytes()
        and first.shadow.tobytes() == second.shadow.tobytes()
    )
    if not equal:
        raise AssertionError(f"同一 Instance 重复渲染不一致: {instance}/{view}")


def _validate_lod(records: list[ViewRecord]) -> None:
    by_instance: dict[str, dict[str, ViewRecord]] = {}
    for record in records:
        by_instance.setdefault(record.instance, {})[record.view] = record
    for instance, views in by_instance.items():
        idle = views["world_idle_24"]
        active = views["world_active_56"]
        if active.size <= idle.size or active.opaque_pixels <= idle.opaque_pixels:
            raise AssertionError(f"激活实体没有比常态实体更大: {instance}")


def _compose_preview(body: Image.Image, emission: Image.Image, shadow: Image.Image) -> Image.Image:
    result = Image.new("RGBA", body.size)
    result = Image.alpha_composite(result, shadow)
    result = Image.alpha_composite(result, body)
    return Image.alpha_composite(result, emission)


def _write_contact_sheet(path: Path, records: list[ViewRecord]) -> Path:
    scale = 7
    tile_width = 56 * scale + 20
    tile_height = 56 * scale + 42
    ordered_instances = list(dict.fromkeys(record.instance for record in records))
    ordered_views = ("icon_56", "world_idle_24", "world_active_56")
    sheet = Image.new("RGB", (tile_width * len(ordered_views), tile_height * len(ordered_instances)), (24, 27, 30))
    draw = ImageDraw.Draw(sheet)
    lookup = {(record.instance, record.view): record for record in records}
    for row, instance in enumerate(ordered_instances):
        for column, view in enumerate(ordered_views):
            record = lookup[(instance, view)]
            preview = Image.open(record.preview_path).convert("RGBA")
            canvas = _checkerboard(preview.size)
            canvas = Image.alpha_composite(canvas, preview)
            enlarged = canvas.resize((record.size * scale, record.size * scale), Image.Resampling.NEAREST)
            left = column * tile_width + (tile_width - enlarged.width) // 2
            top = row * tile_height + 28 + (56 * scale - enlarged.height) // 2
            sheet.paste(enlarged.convert("RGB"), (left, top))
            draw.text((column * tile_width + 8, row * tile_height + 8), f"{instance} / {view}", fill=(222, 226, 229))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)
    return path


def _checkerboard(size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size)
    pixels = []
    for y in range(height):
        for x in range(width):
            value = 42 if (x // 2 + y // 2) % 2 == 0 else 48
            pixels.append((value, value + 2, value + 3, 255))
    image.putdata(pixels)
    return image


def _image_hash(*images: Image.Image) -> str:
    digest = hashlib.sha256()
    for image in images:
        digest.update(f"{image.mode}:{image.size}".encode("ascii"))
        digest.update(image.tobytes())
    return digest.hexdigest()


def _find_blockbench() -> Path | None:
    discovered = shutil.which("Blockbench") or shutil.which("Blockbench.exe")
    candidates = [
        Path(discovered) if discovered else None,
        Path(r"C:\Program Files\Blockbench\Blockbench.exe"),
        Path.home() / "AppData/Local/Programs/Blockbench/Blockbench.exe",
    ]
    for candidate in candidates:
        if candidate is not None and candidate.exists():
            return candidate.resolve()
    return None
