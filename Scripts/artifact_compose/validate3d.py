#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""3D 法器数据校验和渲染冒烟检查。"""

from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image

from .colors import parse_hex_color
from .compose3d import ArtifactComposer3D, collect_variant_materials
from .models3d import Catalog3D, ColorScheme3D, ModuleDef3D, ModuleVariant3D, TemplateDef3D
from .render3d import render_png3d


REQUIRED_SHAPES = {"sword", "seal", "robe", "mirror", "ding"}
SUPPORTED_3D_PRIMITIVES = {
    "box", "beveled_box", "poly_prism", "blade", "cylinder", "frustum", "ellipsoid",
    "torus", "lathe", "capsule", "tube", "cloth_panel", "radial_repeat",
}


def validate_catalog3d(catalog: Catalog3D) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    if "neutral" not in catalog.surface_styles:
        errors.append("3D 表面风格目录缺少 neutral")
    validate_modules3d(catalog.modules.values(), set(catalog.surface_styles), errors, warnings)
    validate_color_schemes3d(catalog, errors, warnings)
    validate_templates3d(catalog, errors, warnings)
    missing = sorted(REQUIRED_SHAPES - set(catalog.shapes))
    if missing:
        errors.append(f"缺少基础器形 3D 模板: {', '.join(missing)}")
    return errors, warnings


def validate_modules3d(
    modules: Iterable[ModuleDef3D],
    surfaces: set[str],
    errors: list[str],
    warnings: list[str],
) -> None:
    for module in modules:
        if "palettes" in module.source_keys:
            errors.append(f"3D 模块 {module.key} 不应定义 palettes；module 只容纳 variants")
        if not module.variants:
            errors.append(f"3D 模块 {module.key} 没有任何变体")
        validate_module_anchor_contract3d(module, errors)
        for variant in module.variants:
            validate_variant3d(variant, surfaces, errors, warnings)


def validate_module_anchor_contract3d(module: ModuleDef3D, errors: list[str]) -> None:
    expected: set[str] | None = None
    for variant in module.variants:
        keys = {anchor.key for anchor in variant.anchors}
        if expected is None:
            expected = keys
            continue
        if keys != expected:
            errors.append(
                f"3D 模块 {module.key} 的变体锚点 key 不一致: "
                f"{variant.key}={sorted(keys)}, expected={sorted(expected)}"
            )


def validate_color_schemes3d(catalog: Catalog3D, errors: list[str], warnings: list[str]) -> None:
    materials: set[str] = set()
    for module in catalog.modules.values():
        for variant in module.variants:
            materials.update(collect_variant_materials(variant))
    if not catalog.color_schemes:
        errors.append("3D 缺少颜色方案；Instance 无法记录配色")
        return
    for scheme in catalog.color_schemes.values():
        validate_color_scheme3d(scheme, materials, errors, warnings)


def validate_color_scheme3d(
    scheme: ColorScheme3D,
    materials: set[str],
    errors: list[str],
    warnings: list[str],
) -> None:
    if not scheme.colors:
        errors.append(f"3D 颜色方案 {scheme.key} 为空")
    for color_name, color in scheme.colors.items():
        try:
            parse_hex_color(color)
        except ValueError as exc:
            errors.append(f"3D 颜色方案 {scheme.key}.{color_name} 非法: {exc}")
    missing = sorted(materials - set(scheme.colors.keys()))
    if missing:
        errors.append(f"3D 颜色方案 {scheme.key} 缺少材质颜色: {', '.join(missing)}")
    extra = sorted(set(scheme.colors.keys()) - materials)
    if extra:
        warnings.append(f"3D 颜色方案 {scheme.key} 有未使用颜色: {', '.join(extra)}")


def validate_variant3d(
    variant: ModuleVariant3D,
    surfaces: set[str],
    errors: list[str],
    warnings: list[str],
) -> None:
    if "colors" in variant.source_keys:
        errors.append(f"{variant.module_key}.{variant.key} 不应定义 colors；配色应记录在 Instance")
    if not variant.anchors:
        errors.append(f"{variant.module_key}.{variant.key} 没有 3D 锚点")
    if not variant.parts:
        errors.append(f"{variant.module_key}.{variant.key} 没有 3D 几何 part")
    seen_anchors: set[str] = set()
    for anchor in variant.anchors:
        if anchor.key in seen_anchors:
            errors.append(f"{variant.module_key}.{variant.key} 存在重复 3D 锚点 {anchor.key}")
        seen_anchors.add(anchor.key)
    for index, part in enumerate(variant.parts):
        validate_part3d(part, f"{variant.module_key}.{variant.key}.parts[{index}]", surfaces, errors)


def validate_part3d(part: dict, path: str, surfaces: set[str], errors: list[str]) -> None:
    primitive = part.get("primitive", part.get("type"))
    if primitive not in SUPPORTED_3D_PRIMITIVES:
        errors.append(f"{path} 使用了不支持的 3D 几何体 {primitive}")
        return
    if primitive == "radial_repeat":
        child = part.get("part")
        if not isinstance(child, dict):
            errors.append(f"{path} 缺少 radial_repeat.part")
            return
        validate_part3d(child, f"{path}.part", surfaces, errors)
        return
    surface = str(part.get("surface", "neutral"))
    if surface not in surfaces:
        errors.append(f"{path} 引用了不存在的表面风格 {surface}")


def validate_templates3d(catalog: Catalog3D, errors: list[str], warnings: list[str]) -> None:
    for template in catalog.templates.values():
        validate_template3d(catalog, template, errors, warnings)


def validate_template3d(catalog: Catalog3D, template: TemplateDef3D, errors: list[str], warnings: list[str]) -> None:
    if not template.placements:
        errors.append(f"3D 模板 {template.key} 没有放置方案")
    if "palettes" in template.source_keys:
        errors.append(f"3D 模板 {template.key} 不应定义 palettes；模板只负责槽位、锚点、视角和光照")
    for placement in template.placements:
        forbidden = sorted({"variant", "palette"} & placement.source_keys)
        if forbidden:
            errors.append(f"3D 模板 {template.key} 放置 {placement.slot} 不应定义 {', '.join(forbidden)}；这些属于 Instance")
        module = catalog.modules.get(placement.module)
        if module is None:
            errors.append(f"3D 模板 {template.key} 引用了不存在的模块 {placement.module}")
            continue
        choices = [variant for variant in module.variants if variant.get_anchor(placement.anchor) is not None]
        if not choices:
            errors.append(f"3D 模板 {template.key} 放置 {placement.slot} 找不到带锚点 {placement.anchor} 的模块变体")


def render_smoke3d(catalog: Catalog3D, output_dir: Path, seed: str = "validate3d") -> list[str]:
    errors: list[str] = []
    composer = ArtifactComposer3D(catalog)
    output_dir.mkdir(parents=True, exist_ok=True)
    for template in sorted(catalog.templates.values(), key=lambda item: item.key):
        composed = composer.compose(template, seed, 0)
        png_path = output_dir / f"{template.shape}_{template.key}.png"
        image = render_png3d(composed, png_path, catalog.canvas)
        inspect_generated_png3d(image, png_path, errors)
    return errors


def inspect_generated_png3d(image: Image.Image, path: Path, errors: list[str]) -> None:
    if image.size != (28, 28):
        errors.append(f"{path} 尺寸不是 28x28: {image.size}")
    alphas = set(image.getchannel("A").getdata())
    if not alphas.issubset({0, 255}):
        errors.append(f"{path} alpha 未硬化: {sorted(alphas)[:8]}")
    opaque = sum(1 for pixel in image.getdata() if pixel[3] > 0)
    if opaque < 24:
        errors.append(f"{path} 不透明像素过少: {opaque}")
