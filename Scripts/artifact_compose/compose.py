#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""按模板和 seed 选择模块变体并生成可渲染组合。"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass

from .geometry import AnchorTransform
from .models import Catalog, ModuleVariant, Placement, TemplateDef


def stable_int(text: str) -> int:
    digest = hashlib.sha256(text.encode("utf-8")).digest()
    return int.from_bytes(digest[:8], "big", signed=False)


@dataclass(frozen=True)
class PlacedModule:
    placement: Placement
    variant: ModuleVariant
    transform: AnchorTransform
    palette: dict[str, str]

    @property
    def id(self) -> str:
        return f"{self.placement.slot}:{self.variant.module_key}.{self.variant.key}"


@dataclass(frozen=True)
class ComposedArtifact:
    template: TemplateDef
    seed: str
    sample_index: int
    modules: tuple[PlacedModule, ...]

    @property
    def variant_map(self) -> dict[str, str]:
        return {
            placed.placement.slot: f"{placed.variant.module_key}.{placed.variant.key}"
            for placed in self.modules
        }


class ArtifactComposer:
    def __init__(self, catalog: Catalog):
        self.catalog = catalog

    def compose(self, template: TemplateDef, seed: str, sample_index: int = 0) -> ComposedArtifact:
        placed_modules: list[PlacedModule] = []
        for placement in sorted(template.placements, key=lambda item: item.z):
            variant = self._select_variant(template, placement, seed, sample_index)
            anchor = variant.get_anchor(placement.anchor)
            if anchor is None:
                raise ValueError(f"{variant.module_key}.{variant.key} 缺少锚点 {placement.anchor}")
            transform = AnchorTransform(
                anchor_x=anchor.x,
                anchor_y=anchor.y,
                anchor_angle=anchor.angle,
                target_x=placement.x,
                target_y=placement.y,
                target_angle=placement.angle,
                scale_x=placement.final_scale_x,
                scale_y=placement.final_scale_y,
            )
            palette = dict(variant.colors)
            if placement.palette:
                palette.update(template.palettes.get(placement.palette, {}))
            placed_modules.append(PlacedModule(placement, variant, transform, palette))
        return ComposedArtifact(template, str(seed), sample_index, tuple(placed_modules))

    def pick_template(self, shape: str, seed: str, sample_index: int) -> TemplateDef:
        choices = self.catalog.templates_for_shape(shape)
        if not choices:
            raise KeyError(f"没有 shape={shape} 的模板")
        index = stable_int(f"{seed}|template|{shape}|{sample_index}") % len(choices)
        return sorted(choices, key=lambda item: item.key)[index]

    def _select_variant(
        self,
        template: TemplateDef,
        placement: Placement,
        seed: str,
        sample_index: int,
    ) -> ModuleVariant:
        module = self.catalog.modules.get(placement.module)
        if module is None:
            raise KeyError(f"模板 {template.key} 引用了不存在的模块 {placement.module}")

        if placement.variant:
            variant = module.get_variant(placement.variant)
            if variant is None:
                raise KeyError(f"模块 {module.key} 不存在变体 {placement.variant}")
            return variant

        choices = [variant for variant in module.variants if variant.get_anchor(placement.anchor) is not None]
        if not choices:
            raise KeyError(f"模块 {module.key} 没有带锚点 {placement.anchor} 的变体")
        index = stable_int(f"{seed}|{sample_index}|{template.key}|{placement.slot}|{module.key}") % len(choices)
        return sorted(choices, key=lambda item: item.key)[index]

