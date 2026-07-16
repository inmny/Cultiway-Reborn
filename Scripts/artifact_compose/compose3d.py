#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""3D 法器模块组合。"""

from __future__ import annotations

from dataclasses import dataclass

from .compose import stable_int
from .math3d import Vec3, add, mul, rotate_euler, sub
from .mesh3d import Face3D, part_faces
from .models3d import Catalog3D, ModuleDef3D, ModuleVariant3D, Placement3D, SurfaceStyle3D, TemplateDef3D


INSTANCE_MATERIAL_SEPARATOR = "::"


@dataclass(frozen=True)
class InstanceModule3D:
    placement: Placement3D
    variant: ModuleVariant3D
    anchor_position: Vec3
    color_scheme_key: str
    colors: dict[str, str]

    @property
    def id(self) -> str:
        return f"{self.placement.slot}:{self.variant.module_key}.{self.variant.key}"


@dataclass(frozen=True)
class ArtifactInstance3D:
    template: TemplateDef3D
    seed: str
    sample_index: int
    modules: tuple[InstanceModule3D, ...]
    surface_styles: dict[str, SurfaceStyle3D]

    @property
    def variant_map(self) -> dict[str, str]:
        return {
            placed.placement.slot: f"{placed.variant.module_key}.{placed.variant.key}"
            for placed in self.modules
        }

    @property
    def color_map(self) -> dict[str, dict[str, object]]:
        return {
            placed.placement.slot: {
                "scheme": placed.color_scheme_key,
                "colors": dict(placed.colors),
            }
            for placed in self.modules
        }


class ArtifactComposer3D:
    def __init__(self, catalog: Catalog3D):
        self.catalog = catalog

    def compose(self, template: TemplateDef3D, seed: str, sample_index: int = 0) -> ArtifactInstance3D:
        modules: list[InstanceModule3D] = []
        for placement in sorted(template.placements, key=lambda item: item.z):
            module = self._get_module(template, placement)
            variant = self._select_variant(template, placement, module, seed, sample_index)
            anchor = variant.get_anchor(placement.anchor)
            if anchor is None:
                raise ValueError(f"{variant.module_key}.{variant.key} 缺少 3D 锚点 {placement.anchor}")
            color_scheme_key, colors = self._select_colors(template, placement, module, variant, seed, sample_index)
            modules.append(InstanceModule3D(placement, variant, anchor.position, color_scheme_key, colors))
        return ArtifactInstance3D(template, str(seed), sample_index, tuple(modules), self.catalog.surface_styles)

    def pick_template(self, shape: str, seed: str, sample_index: int) -> TemplateDef3D:
        choices = self.catalog.templates_for_shape(shape)
        if not choices:
            raise KeyError(f"没有 shape={shape} 的 3D 模板")
        index = stable_int(f"{seed}|3d_template|{shape}|{sample_index}") % len(choices)
        return sorted(choices, key=lambda item: item.key)[index]

    def _get_module(self, template: TemplateDef3D, placement: Placement3D) -> ModuleDef3D:
        module = self.catalog.modules.get(placement.module)
        if module is None:
            raise KeyError(f"3D 模板 {template.key} 引用了不存在的模块 {placement.module}")
        return module

    def _select_variant(
        self,
        template: TemplateDef3D,
        placement: Placement3D,
        module: ModuleDef3D,
        seed: str,
        sample_index: int,
    ) -> ModuleVariant3D:
        choices = [variant for variant in module.variants if variant.get_anchor(placement.anchor) is not None]
        if not choices:
            raise KeyError(f"3D 模块 {module.key} 没有带锚点 {placement.anchor} 的变体")
        index = stable_int(f"{seed}|{sample_index}|{template.key}|{placement.slot}|{module.key}|3d") % len(choices)
        return sorted(choices, key=lambda item: item.key)[index]

    def _select_colors(
        self,
        template: TemplateDef3D,
        placement: Placement3D,
        module: ModuleDef3D,
        variant: ModuleVariant3D,
        seed: str,
        sample_index: int,
    ) -> tuple[str, dict[str, str]]:
        materials = sorted(collect_variant_materials(variant))
        if self.catalog.color_schemes:
            choices = sorted(self.catalog.color_schemes.values(), key=lambda item: item.key)
            index = stable_int(
                f"{seed}|{sample_index}|{template.key}|{placement.slot}|{module.key}|{variant.key}|colors3d"
            ) % len(choices)
            scheme = choices[index]
            return scheme.key, {material: scheme.colors.get(material, "#9aa0a8") for material in materials}
        return "fallback", {material: "#9aa0a8" for material in materials}


def build_world_faces(instance: ArtifactInstance3D) -> list[Face3D]:
    faces: list[Face3D] = []
    for module in instance.modules:
        for part in module.variant.parts:
            for face in part_faces(part):
                points = tuple(transform_module_point(point, module) for point in face.points)
                faces.append(Face3D(
                    points,
                    instance_material_key(module.placement.slot, face.material),
                    face.surface,
                ))
    return faces


def transform_module_point(point: Vec3, module: InstanceModule3D) -> Vec3:
    local = sub(point, module.anchor_position)
    local = mul(local, module.placement.scale)
    local = rotate_euler(local, module.placement.rotation)
    return add(local, module.placement.position)


def collect_variant_materials(variant: ModuleVariant3D) -> set[str]:
    materials: set[str] = set()
    for part in variant.parts:
        collect_part_materials(part, materials)
    return materials


def collect_part_materials(part: dict, materials: set[str]) -> None:
    if part.get("primitive", part.get("type")) == "radial_repeat":
        child = part.get("part")
        if isinstance(child, dict):
            collect_part_materials(child, materials)
        return
    materials.add(str(part.get("material", "main")))


def instance_material_key(slot: str, material: str) -> str:
    return f"{slot}{INSTANCE_MATERIAL_SEPARATOR}{material}"


def split_instance_material_key(value: str) -> tuple[str | None, str]:
    if INSTANCE_MATERIAL_SEPARATOR not in value:
        return None, value
    slot, material = value.split(INSTANCE_MATERIAL_SEPARATOR, 1)
    return slot, material
