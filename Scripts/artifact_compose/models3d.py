#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""3D 法器组合数据模型。"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from .math3d import Vec3, v3


@dataclass(frozen=True)
class Anchor3D:
    key: str
    position: Vec3

    @staticmethod
    def from_json(data: dict[str, Any]) -> "Anchor3D":
        return Anchor3D(str(data["key"]), v3(data.get("position", data.get("pos", [0, 0, 0]))))


@dataclass(frozen=True)
class ModuleVariant3D:
    module_key: str
    key: str
    anchors: tuple[Anchor3D, ...]
    parts: tuple[dict[str, Any], ...]
    source_keys: frozenset[str]

    @staticmethod
    def from_json(module_key: str, data: dict[str, Any]) -> "ModuleVariant3D":
        return ModuleVariant3D(
            module_key=module_key,
            key=str(data["key"]),
            anchors=tuple(Anchor3D.from_json(item) for item in data.get("anchors", [])),
            parts=tuple(dict(item) for item in data.get("parts", [])),
            source_keys=frozenset(str(key) for key in data.keys()),
        )

    def get_anchor(self, key: str) -> Anchor3D | None:
        for anchor in self.anchors:
            if anchor.key == key:
                return anchor
        return None


@dataclass(frozen=True)
class ModuleDef3D:
    key: str
    variants: tuple[ModuleVariant3D, ...]
    source_keys: frozenset[str]

    @staticmethod
    def from_json(data: dict[str, Any]) -> "ModuleDef3D":
        key = str(data["key"])
        return ModuleDef3D(
            key=key,
            variants=tuple(ModuleVariant3D.from_json(key, item) for item in data.get("variants", [])),
            source_keys=frozenset(str(item) for item in data.keys()),
        )

    def get_variant(self, key: str) -> ModuleVariant3D | None:
        for variant in self.variants:
            if variant.key == key:
                return variant
        return None


@dataclass(frozen=True)
class ColorScheme3D:
    key: str
    colors: dict[str, str]
    source_keys: frozenset[str]

    @staticmethod
    def from_json(data: dict[str, Any]) -> "ColorScheme3D":
        return ColorScheme3D(
            key=str(data["key"]),
            colors=dict(data.get("colors", {})),
            source_keys=frozenset(str(item) for item in data.keys()),
        )


@dataclass(frozen=True)
class SurfaceStyle3D:
    key: str
    diffuse: float = 0.56
    side_shadow: float = 1.0
    brightness: float = 0.0
    emission: float = 0.0
    texture_dark: float = 0.0
    texture_light: float = 0.0
    texture_frequency: int = 0
    sparkle_frequency: int = 0

    @staticmethod
    def from_json(data: dict[str, Any]) -> "SurfaceStyle3D":
        return SurfaceStyle3D(
            key=str(data["key"]),
            diffuse=float(data.get("diffuse", 0.56)),
            side_shadow=float(data.get("side_shadow", 1.0)),
            brightness=float(data.get("brightness", 0.0)),
            emission=float(data.get("emission", 0.0)),
            texture_dark=float(data.get("texture_dark", 0.0)),
            texture_light=float(data.get("texture_light", 0.0)),
            texture_frequency=int(data.get("texture_frequency", 0)),
            sparkle_frequency=int(data.get("sparkle_frequency", 0)),
        )


@dataclass(frozen=True)
class Placement3D:
    slot: str
    module: str
    anchor: str
    position: Vec3
    rotation: Vec3
    scale: Vec3
    z: int = 0
    source_keys: frozenset[str] = frozenset()

    @staticmethod
    def from_json(data: dict[str, Any], index: int) -> "Placement3D":
        return Placement3D(
            slot=str(data.get("slot", f"slot_{index:02d}")),
            module=str(data["module"]),
            anchor=str(data.get("anchor", "origin")),
            position=v3(data.get("position", data.get("pos", [0, 0, 0]))),
            rotation=v3(data.get("rotation", data.get("rot", [0, 0, 0]))),
            scale=v3(data.get("scale", [1, 1, 1]), (1.0, 1.0, 1.0)),
            z=int(data.get("z", index)),
            source_keys=frozenset(str(key) for key in data.keys()),
        )


@dataclass(frozen=True)
class TemplateDef3D:
    key: str
    shape: str
    artifact: str
    camera: dict[str, Any]
    light: dict[str, Any]
    placements: tuple[Placement3D, ...]
    source_keys: frozenset[str]

    @staticmethod
    def from_json(data: dict[str, Any]) -> "TemplateDef3D":
        return TemplateDef3D(
            key=str(data["key"]),
            shape=str(data["shape"]),
            artifact=str(data.get("artifact", "")),
            camera=dict(data.get("camera", {})),
            light=dict(data.get("light", {})),
            placements=tuple(Placement3D.from_json(item, i) for i, item in enumerate(data.get("placements", []))),
            source_keys=frozenset(str(key) for key in data.keys()),
        )


@dataclass(frozen=True)
class Catalog3D:
    canvas: int
    modules: dict[str, ModuleDef3D]
    templates: dict[str, TemplateDef3D]
    color_schemes: dict[str, ColorScheme3D]
    surface_styles: dict[str, SurfaceStyle3D]

    def templates_for_shape(self, shape: str) -> list[TemplateDef3D]:
        return [template for template in self.templates.values() if template.shape == shape]

    @property
    def shapes(self) -> list[str]:
        return sorted({template.shape for template in self.templates.values()})
