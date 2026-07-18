#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""读取运行时 AppearanceCatalog，渲染覆盖全部 variant 的组合画廊。"""

from __future__ import annotations

import argparse
import json
import shutil
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageDraw

from .obj_io import load_model
from .pipeline import ViewRecord, _render_instance, _validate_lod
from .types import (
    AppearanceTemplate,
    ModuleDefinition,
    ModuleVariant,
    Placement,
    PrototypeCatalog,
    ViewSpec,
    VisualInstance,
)


PALETTES = ("celestial_jade", "moon_silver", "copper_ember")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--catalog",
        type=Path,
        default=Path("Content/Artifacts/AppearanceCatalog"),
        help="AppearanceCatalog 路径",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("artifacts/artifact_model_rebuild/runtime_gallery"),
        help="画廊输出目录",
    )
    parser.add_argument(
        "--shape",
        action="append",
        dest="shapes",
        help="只渲染指定器形；可重复传入",
    )
    parser.add_argument("--check", action="store_true", help="重复生成并检查确定性哈希")
    args = parser.parse_args(argv)
    report = render_gallery(args.catalog, args.output, args.shapes)
    if args.check:
        repeated = render_gallery(
            args.catalog,
            args.output.with_name(args.output.name + "_repeat"),
            args.shapes,
        )
        if report["hashes"] != repeated["hashes"]:
            raise AssertionError("运行时模型画廊重复渲染不一致")
        shutil.rmtree(args.output.with_name(args.output.name + "_repeat"))
    print(
        f"templates={report['templates']} instances={report['instances']} "
        f"views={report['views']} covered_variants={report['covered_variants']} "
        f"deterministic={args.check}"
    )
    print(f"gallery={args.output.resolve()}")
    return 0


def render_gallery(catalog_path: Path, output: Path, shapes: list[str] | None = None) -> dict:
    output = output.resolve()
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)
    catalog, variant_total = load_runtime_catalog(catalog_path.resolve())
    if shapes:
        requested = set(shapes)
        selected_templates = {
            key: template
            for key, template in catalog.templates.items()
            if template.shape in requested
        }
        missing = requested - {template.shape for template in selected_templates.values()}
        if missing:
            raise KeyError(f"不存在的器形: {', '.join(sorted(missing))}")
        required_modules = {
            placement.module_key
            for template in selected_templates.values()
            for placement in template.placements
        }
        variant_total = sum(len(catalog.modules[key].variants) for key in required_modules)
        catalog = PrototypeCatalog(catalog.models, catalog.modules, selected_templates)
    instances = build_instances(catalog)
    records: list[ViewRecord] = []
    covered: set[str] = set()
    manifests: list[dict] = []
    for instance in instances:
        rendered, manifest = _render_instance(catalog, instance, output)
        records.extend(rendered)
        manifests.append(manifest)
        for placement in instance.template.placements:
            covered.add(f"{placement.module_key}.{instance.variants[placement.slot]}")
    if len(covered) != variant_total:
        raise AssertionError(f"画廊没有覆盖全部 variant: {len(covered)}/{variant_total}")
    _validate_lod(records)
    sheets = write_shape_sheets(output, instances, records)
    validate_icon_output(records)
    hashes = [record.png_hash for record in records]
    report = {
        "templates": len(catalog.templates),
        "instances": len(instances),
        "views": len(records),
        "covered_variants": len(covered),
        "hashes": hashes,
        "sheets": [str(path.relative_to(output)).replace("\\", "/") for path in sheets],
        "items": manifests,
    }
    (output / "manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", "utf-8")
    return report


def validate_icon_output(records: list[ViewRecord]) -> None:
    failures = []
    by_instance: dict[str, dict[str, ViewRecord]] = defaultdict(dict)
    for record in records:
        by_instance[record.instance][record.view] = record
    for instance, views in by_instance.items():
        icon = views["icon_56"]
        active = views["world_active_56"]
        left, top, right, bottom = icon.opaque_bounds
        span = max(right - left, bottom - top)
        if icon.size < 56 or span < round(icon.size * 0.68):
            failures.append(f"{instance}: size={icon.size}, span={span}")
            continue
        if (
            icon.size != active.size
            or icon.opaque_bounds != active.opaque_bounds
            or icon.semantic_hash != active.semantic_hash
            or icon.png_hash != active.png_hash
        ):
            failures.append(f"{instance}: 图标没有完整复用激活态渲染")
    if failures:
        raise AssertionError("高质量图标输出不合格:\n" + "\n".join(failures))


def load_runtime_catalog(root: Path) -> tuple[PrototypeCatalog, int]:
    models = {}
    modules = {}
    variant_total = 0
    for path in sorted(root.glob("modules*.json")):
        data = json.loads(path.read_text("utf-8"))
        model_root = str(data.get("model_root", "Models"))
        for module_data in data.get("modules", []):
            module_key = str(module_data["key"])
            variants = []
            for variant_data in module_data.get("variants", []):
                variant_total += 1
                variant_key = str(variant_data["key"])
                model_key = f"{module_key}.{variant_key}"
                relative = variant_data.get("model") or f"{model_root}/{module_key}/{variant_key}.obj"
                models[model_key] = load_model(model_key, (root / str(relative)).resolve())
                variants.append(ModuleVariant(variant_key, model_key))
            modules[module_key] = ModuleDefinition(module_key, tuple(variants))

    templates = {}
    for path in sorted(root.glob("templates*.json")):
        data = json.loads(path.read_text("utf-8"))
        for template_data in data.get("templates", []):
            key = str(template_data["key"])
            camera = dict(template_data.get("camera", {}))
            light = dict(template_data.get("light", {}))
            placements = tuple(
                Placement(
                    str(item["slot"]),
                    str(item["module"]),
                    str(item.get("anchor", "origin")),
                    tuple(float(value) for value in item.get("position", [0, 0, 0])),
                    tuple(float(value) for value in item.get("rotation", [0, 0, 0])),
                    tuple(float(value) for value in item.get("scale", [1, 1, 1])),
                    int(item.get("z", index)),
                )
                for index, item in enumerate(template_data.get("placements", []))
            )
            configured_views = {
                str(item["key"]): dict(item)
                for item in template_data.get("views", [])
            }
            active_icon_view = configured_views.get("world_active")
            views = (
                _runtime_view_spec(
                    "icon_56", "icon", 56, 3, 2,
                    active_icon_view,
                    {} if active_icon_view is not None else camera,
                    light,
                    auto_frame_default=active_icon_view is not None,
                ),
                _runtime_view_spec(
                    "world_idle_24", "world_idle", 24, 2, 1,
                    configured_views.get("world_idle"), {}, light, auto_frame_default=True,
                ),
                _runtime_view_spec(
                    "world_active_56", "world_active", 56, 3, 2,
                    configured_views.get("world_active"), {}, light, auto_frame_default=True,
                ),
            )
            templates[key] = AppearanceTemplate(key, str(template_data["shape"]), placements, views)
    catalog = PrototypeCatalog(models, modules, templates)
    catalog.validate()
    return catalog, variant_total


def _runtime_view_spec(
    output_key: str,
    runtime_key: str,
    default_size: int,
    default_margin: int,
    supersample: int,
    configured: dict | None,
    fallback_camera: dict,
    fallback_light: dict,
    *,
    auto_frame_default: bool,
) -> ViewSpec:
    source = configured or {}
    camera = dict(source.get("camera", {})) if configured is not None else dict(fallback_camera)
    configured_light = dict(source.get("light", {}))
    light = configured_light if configured_light else fallback_light
    size = int(source.get("size", 0)) or default_size
    auto_frame = bool(source.get("auto_frame", True if configured is not None else auto_frame_default))
    target_data = camera.get("target")
    target = None if target_data is None else tuple(float(value) for value in target_data)
    return ViewSpec(
        key=output_key,
        size=size,
        rotation=(
            -float(camera.get("pitch", 0)),
            -float(camera.get("yaw", 0)),
            -float(camera.get("roll", 0)),
        ),
        light_yaw=float(light.get("yaw", -35)),
        light_pitch=float(light.get("pitch", 55)),
        margin=int(source.get("margin", 2 if configured is not None else default_margin)),
        supersample=supersample,
        auto_frame=auto_frame,
        target=target,
        fixed_scale=float(camera.get("scale", 8.0)) if not auto_frame else 0.0,
    )


def build_instances(catalog: PrototypeCatalog) -> tuple[VisualInstance, ...]:
    instances = []
    for template in sorted(catalog.templates.values(), key=lambda value: value.key):
        for sample in range(3):
            selected = {}
            for slot_index, placement in enumerate(template.placements):
                variants = catalog.modules[placement.module_key].variants
                selected[placement.slot] = variants[(sample + slot_index) % len(variants)].key
            instances.append(VisualInstance(
                f"{template.key}_{sample + 1}",
                template,
                PALETTES[sample],
                selected,
            ))
    return tuple(instances)


def write_shape_sheets(
    output: Path,
    instances: tuple[VisualInstance, ...],
    records: list[ViewRecord],
) -> list[Path]:
    by_shape: dict[str, list[VisualInstance]] = defaultdict(list)
    for instance in instances:
        by_shape[instance.template.shape].append(instance)
    lookup = {(record.instance, record.view): record for record in records}
    paths = []
    for view_key, suffix, scale, source_size in (
        ("world_idle_24", "idle", 10, 24),
        ("world_active_56", "active", 5, 56),
        ("icon_56", "icon", 5, 56),
    ):
        tile_size = source_size * scale
        cell_width = tile_size + 18
        cell_height = tile_size + 42
        for shape, shape_instances in sorted(by_shape.items()):
            template_keys = sorted({instance.template.key for instance in shape_instances})
            sheet = Image.new("RGB", (cell_width * 3, cell_height * len(template_keys)), (24, 27, 30))
            draw = ImageDraw.Draw(sheet)
            for row, template_key in enumerate(template_keys):
                row_instances = [instance for instance in shape_instances if instance.template.key == template_key]
                for column, instance in enumerate(row_instances):
                    record = lookup[(instance.key, view_key)]
                    image = Image.open(record.preview_path).convert("RGBA")
                    checker = checkerboard(image.size)
                    preview = Image.alpha_composite(checker, image).resize((tile_size, tile_size), Image.Resampling.NEAREST)
                    x = column * cell_width + 9
                    y = row * cell_height + 28
                    sheet.paste(preview.convert("RGB"), (x, y))
                    draw.text((x, row * cell_height + 7), f"{template_key} / v{column + 1}", fill=(225, 229, 232))
            path = output / f"gallery_{shape}_{suffix}.png"
            sheet.save(path)
            paths.append(path)
    return paths


def checkerboard(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size)
    image.putdata([
        (42, 44, 47, 255) if (x // 2 + y // 2) % 2 == 0 else (49, 51, 54, 255)
        for y in range(size[1])
        for x in range(size[0])
    ])
    return image


if __name__ == "__main__":
    raise SystemExit(main())
