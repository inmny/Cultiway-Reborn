#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""数据校验和渲染冒烟检查。"""

from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image

from .colors import parse_hex_color
from .compose import ArtifactComposer
from .models import Catalog, ModuleDef, ModuleVariant, TemplateDef
from .render import SUPPORTED_PRIMITIVES, render_png
from .svg_writer import write_svg


REQUIRED_SHAPES = {"sword", "seal", "robe", "mirror", "ding"}


def validate_catalog(catalog: Catalog) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    validate_modules(catalog.modules.values(), errors, warnings)
    validate_templates(catalog, errors, warnings)
    missing = sorted(REQUIRED_SHAPES - set(catalog.shapes))
    if missing:
        errors.append(f"缺少基础器形模板: {', '.join(missing)}")
    return errors, warnings


def validate_modules(modules: Iterable[ModuleDef], errors: list[str], warnings: list[str]) -> None:
    for module in modules:
        if not module.variants:
            errors.append(f"模块 {module.key} 没有任何变体")
        seen_variants: set[str] = set()
        for variant in module.variants:
            if variant.key in seen_variants:
                errors.append(f"模块 {module.key} 存在重复变体 {variant.key}")
            seen_variants.add(variant.key)
            validate_variant(variant, errors, warnings)


def validate_variant(variant: ModuleVariant, errors: list[str], warnings: list[str]) -> None:
    if not variant.anchors:
        errors.append(f"{variant.module_key}.{variant.key} 没有锚点")
    if not variant.shapes:
        errors.append(f"{variant.module_key}.{variant.key} 没有矢量形状")
    seen_anchors: set[str] = set()
    for anchor in variant.anchors:
        if anchor.key in seen_anchors:
            errors.append(f"{variant.module_key}.{variant.key} 存在重复锚点 {anchor.key}")
        seen_anchors.add(anchor.key)
    for name, color in variant.colors.items():
        try:
            parse_hex_color(color)
        except ValueError as exc:
            errors.append(f"{variant.module_key}.{variant.key} 颜色 {name} 非法: {exc}")
    for primitive in variant.shapes:
        kind = primitive.get("type")
        if kind not in SUPPORTED_PRIMITIVES:
            errors.append(f"{variant.module_key}.{variant.key} 有不支持的形状类型 {kind}")
        for color_field in ("fill", "stroke"):
            token = primitive.get(color_field)
            if token and token != "none" and not str(token).startswith("#") and token not in variant.colors:
                warnings.append(f"{variant.module_key}.{variant.key} 的 {color_field}={token} 依赖模板调色板覆盖")


def validate_templates(catalog: Catalog, errors: list[str], warnings: list[str]) -> None:
    for template in catalog.templates.values():
        validate_template(catalog, template, errors, warnings)


def validate_template(catalog: Catalog, template: TemplateDef, errors: list[str], warnings: list[str]) -> None:
    if not template.placements:
        errors.append(f"模板 {template.key} 没有放置方案")
    for palette_name, palette in template.palettes.items():
        for color_name, color in palette.items():
            try:
                parse_hex_color(color)
            except ValueError as exc:
                errors.append(f"模板 {template.key} 调色板 {palette_name}.{color_name} 非法: {exc}")
    for index, shade in enumerate(template.shading):
        if shade.get("type") not in SUPPORTED_PRIMITIVES:
            errors.append(f"模板 {template.key} 阴影 {index} 使用了不支持的形状类型 {shade.get('type')}")
    for placement in template.placements:
        module = catalog.modules.get(placement.module)
        if module is None:
            errors.append(f"模板 {template.key} 引用了不存在的模块 {placement.module}")
            continue
        if placement.palette and placement.palette not in template.palettes:
            warnings.append(f"模板 {template.key} 放置 {placement.slot} 引用了不存在的调色板 {placement.palette}")
        choices = [variant for variant in module.variants if variant.get_anchor(placement.anchor) is not None]
        if placement.variant:
            variant = module.get_variant(placement.variant)
            if variant is None:
                errors.append(f"模板 {template.key} 放置 {placement.slot} 指定了不存在的变体 {placement.variant}")
            elif variant.get_anchor(placement.anchor) is None:
                errors.append(f"模板 {template.key} 放置 {placement.slot} 的变体 {placement.variant} 缺少锚点 {placement.anchor}")
        elif not choices:
            errors.append(f"模板 {template.key} 放置 {placement.slot} 找不到带锚点 {placement.anchor} 的模块变体")


def render_smoke(catalog: Catalog, output_dir: Path, seed: str = "validate") -> list[str]:
    errors: list[str] = []
    composer = ArtifactComposer(catalog)
    output_dir.mkdir(parents=True, exist_ok=True)
    for template in sorted(catalog.templates.values(), key=lambda item: item.key):
        composed = composer.compose(template, seed, 0)
        stem = f"{template.shape}_{template.key}"
        svg_path = output_dir / f"{stem}.svg"
        png_path = output_dir / f"{stem}.png"
        write_svg(composed, svg_path, catalog.canvas)
        image = render_png(composed, png_path, catalog.canvas)
        inspect_generated_png(image, png_path, errors)
    return errors


def inspect_generated_png(image: Image.Image, path: Path, errors: list[str]) -> None:
    if image.size != (28, 28):
        errors.append(f"{path} 尺寸不是 28x28: {image.size}")
    alphas = set(image.getchannel("A").getdata())
    if not alphas.issubset({0, 255}):
        errors.append(f"{path} alpha 未硬化: {sorted(alphas)[:8]}")
    colors = {pixel for pixel in image.getdata() if pixel[3] > 0}
    if len(colors) > 128:
        errors.append(f"{path} 颜色数超过 128: {len(colors)}")
