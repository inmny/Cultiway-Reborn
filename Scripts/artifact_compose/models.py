#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""法器组合数据模型。"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class Anchor:
    key: str
    x: float
    y: float
    angle: float = 0.0

    @staticmethod
    def from_json(data: dict[str, Any]) -> "Anchor":
        return Anchor(
            key=str(data["key"]),
            x=float(data["x"]),
            y=float(data["y"]),
            angle=float(data.get("angle", 0.0)),
        )


@dataclass(frozen=True)
class ModuleVariant:
    module_key: str
    key: str
    anchors: tuple[Anchor, ...]
    shapes: tuple[dict[str, Any], ...]
    colors: dict[str, str]

    @staticmethod
    def from_json(module_key: str, data: dict[str, Any]) -> "ModuleVariant":
        return ModuleVariant(
            module_key=module_key,
            key=str(data["key"]),
            anchors=tuple(Anchor.from_json(item) for item in data.get("anchors", [])),
            shapes=tuple(dict(item) for item in data.get("shapes", [])),
            colors=dict(data.get("colors", {})),
        )

    def get_anchor(self, key: str) -> Anchor | None:
        for anchor in self.anchors:
            if anchor.key == key:
                return anchor
        return None


@dataclass(frozen=True)
class ModuleDef:
    key: str
    variants: tuple[ModuleVariant, ...]

    @staticmethod
    def from_json(data: dict[str, Any]) -> "ModuleDef":
        key = str(data["key"])
        return ModuleDef(
            key=key,
            variants=tuple(ModuleVariant.from_json(key, item) for item in data.get("variants", [])),
        )

    def get_variant(self, key: str) -> ModuleVariant | None:
        for variant in self.variants:
            if variant.key == key:
                return variant
        return None


@dataclass(frozen=True)
class Placement:
    slot: str
    module: str
    anchor: str
    x: float
    y: float
    angle: float = 0.0
    scale: float = 1.0
    scale_x: float = 1.0
    scale_y: float = 1.0
    mirror_x: bool = False
    variant: str | None = None
    palette: str | None = None
    z: int = 0

    @staticmethod
    def from_json(data: dict[str, Any], index: int) -> "Placement":
        return Placement(
            slot=str(data.get("slot", f"slot_{index:02d}")),
            module=str(data["module"]),
            anchor=str(data["anchor"]),
            x=float(data["x"]),
            y=float(data["y"]),
            angle=float(data.get("angle", 0.0)),
            scale=float(data.get("scale", 1.0)),
            scale_x=float(data.get("scale_x", 1.0)),
            scale_y=float(data.get("scale_y", 1.0)),
            mirror_x=bool(data.get("mirror_x", False)),
            variant=data.get("variant"),
            palette=data.get("palette"),
            z=int(data.get("z", index)),
        )

    @property
    def final_scale_x(self) -> float:
        value = self.scale * self.scale_x
        return -value if self.mirror_x else value

    @property
    def final_scale_y(self) -> float:
        return self.scale * self.scale_y


@dataclass(frozen=True)
class TemplateDef:
    key: str
    shape: str
    artifact: str
    placements: tuple[Placement, ...]
    shading: tuple[dict[str, Any], ...]
    palettes: dict[str, dict[str, str]]

    @staticmethod
    def from_json(data: dict[str, Any]) -> "TemplateDef":
        return TemplateDef(
            key=str(data["key"]),
            shape=str(data["shape"]),
            artifact=str(data.get("artifact", "")),
            placements=tuple(Placement.from_json(item, i) for i, item in enumerate(data.get("placements", []))),
            shading=tuple(dict(item) for item in data.get("shading", [])),
            palettes={str(key): dict(value) for key, value in data.get("palettes", {}).items()},
        )


@dataclass(frozen=True)
class Catalog:
    canvas: int
    modules: dict[str, ModuleDef]
    templates: dict[str, TemplateDef]

    def templates_for_shape(self, shape: str) -> list[TemplateDef]:
        return [template for template in self.templates.values() if template.shape == shape]

    @property
    def shapes(self) -> list[str]:
        return sorted({template.shape for template in self.templates.values()})

