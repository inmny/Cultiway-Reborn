#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""原型中的模型、模板、Instance 与烘焙图层数据。"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

from Scripts.artifact_compose.math3d import Vec3


@dataclass(frozen=True)
class MeshFace:
    points: tuple[Vec3, ...]
    material: str
    object_name: str = "mesh"
    surface: str = "neutral"
    normals: tuple[Vec3, ...] = ()


@dataclass(frozen=True)
class ModelAsset:
    key: str
    path: Path
    faces: tuple[MeshFace, ...]
    anchors: dict[str, Vec3]


@dataclass(frozen=True)
class ModuleVariant:
    key: str
    model_key: str


@dataclass(frozen=True)
class ModuleDefinition:
    key: str
    variants: tuple[ModuleVariant, ...]

    def get_variant(self, key: str) -> ModuleVariant:
        for variant in self.variants:
            if variant.key == key:
                return variant
        raise KeyError(f"模块 {self.key} 没有 variant {key}")


@dataclass(frozen=True)
class Placement:
    slot: str
    module_key: str
    anchor: str
    position: Vec3 = (0.0, 0.0, 0.0)
    rotation: Vec3 = (0.0, 0.0, 0.0)
    scale: Vec3 = (1.0, 1.0, 1.0)
    order: int = 0


@dataclass(frozen=True)
class ViewSpec:
    key: str
    size: int
    rotation: Vec3
    light_yaw: float = -35.0
    light_pitch: float = 55.0
    margin: int = 2
    supersample: int = 1
    auto_frame: bool = True
    target: Vec3 | None = None
    fixed_scale: float = 0.0


@dataclass(frozen=True)
class ResolvedView:
    spec: ViewSpec
    target: Vec3
    scale: float


@dataclass(frozen=True)
class AppearanceTemplate:
    key: str
    shape: str
    placements: tuple[Placement, ...]
    views: tuple[ViewSpec, ...]


@dataclass(frozen=True)
class VisualInstance:
    key: str
    template: AppearanceTemplate
    palette_key: str
    variants: dict[str, str]
    icon_override: Path | None = None


@dataclass(frozen=True)
class PrototypeCatalog:
    models: dict[str, ModelAsset]
    modules: dict[str, ModuleDefinition]
    templates: dict[str, AppearanceTemplate]

    def validate(self) -> None:
        for module in self.modules.values():
            if not module.variants:
                raise ValueError(f"模块 {module.key} 没有 variant")
            anchor_keys: frozenset[str] | None = None
            for variant in module.variants:
                model = self.models.get(variant.model_key)
                if model is None:
                    raise KeyError(f"模块 {module.key} 引用了不存在的模型 {variant.model_key}")
                current = frozenset(model.anchors)
                if anchor_keys is None:
                    anchor_keys = current
                elif current != anchor_keys:
                    raise ValueError(
                        f"模块 {module.key} 的 variant 锚点 key 不一致: "
                        f"期望 {sorted(anchor_keys)}，实际 {sorted(current)}"
                    )
        for template in self.templates.values():
            for placement in template.placements:
                module = self.modules.get(placement.module_key)
                if module is None:
                    raise KeyError(
                        f"模板 {template.key} 的槽位 {placement.slot} 引用了不存在的模块 "
                        f"{placement.module_key}"
                    )
                model = self.models[module.variants[0].model_key]
                if placement.anchor not in model.anchors:
                    raise KeyError(
                        f"模板 {template.key} 的槽位 {placement.slot} 使用了模块 "
                        f"{module.key} 不具备的锚点 {placement.anchor}"
                    )


@dataclass
class BakeLayer:
    width: int
    height: int
    slot: str
    order: int
    sample_scale: int = 1
    depth: list[float] = field(init=False)
    materials: list[str | None] = field(init=False)
    surfaces: list[str | None] = field(init=False)
    objects: list[str | None] = field(init=False)
    positions: list[Vec3] = field(init=False)
    lights: list[float] = field(init=False)
    shades: list[int] = field(init=False)
    emissions: list[int] = field(init=False)

    def __post_init__(self) -> None:
        count = self.width * self.height
        self.depth = [-1e9] * count
        self.materials = [None] * count
        self.surfaces = [None] * count
        self.objects = [None] * count
        self.positions = [(0.0, 0.0, 0.0)] * count
        self.lights = [0.0] * count
        self.shades = [0] * count
        self.emissions = [0] * count


@dataclass(frozen=True)
class ComposedFrame:
    body_path: Path
    emission_path: Path
    shadow_path: Path
    opaque_pixels: int
    source_hash: str
