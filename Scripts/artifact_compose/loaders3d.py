#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""读取 3D 模块和模板配置。"""

from __future__ import annotations

import json
from pathlib import Path

from .models3d import Catalog3D, ColorScheme3D, ModuleDef3D, SurfaceStyle3D, TemplateDef3D


def default_data3d_dir() -> Path:
    return Path(__file__).resolve().parent / "data3d"


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as file:
        return json.load(file)


def load_json_files(root: Path, pattern: str) -> list[dict]:
    return [load_json(path) for path in sorted(root.glob(pattern))]


def load_catalog3d(data_dir: Path | None = None) -> Catalog3D:
    root = data_dir or default_data3d_dir()
    module_files = load_json_files(root, "modules*.json")
    template_files = load_json_files(root, "templates*.json")
    color_files = load_json_files(root, "colors*.json")
    surface_files = load_json_files(root, "surfaces*.json")
    modules = {
        module.key: module
        for data in module_files
        for module in (ModuleDef3D.from_json(item) for item in data.get("modules", []))
    }
    templates = {
        template.key: template
        for data in template_files
        for template in (TemplateDef3D.from_json(item) for item in data.get("templates", []))
    }
    color_schemes = {
        scheme.key: scheme
        for data in color_files
        for scheme in (ColorScheme3D.from_json(item) for item in data.get("schemes", []))
    }
    surface_styles = {
        style.key: style
        for data in surface_files
        for style in (SurfaceStyle3D.from_json(item) for item in data.get("styles", []))
    }
    canvas_sources = module_files + template_files
    return Catalog3D(
        canvas=int(next((data["canvas"] for data in canvas_sources if "canvas" in data), 28)),
        modules=modules,
        templates=templates,
        color_schemes=color_schemes,
        surface_styles=surface_styles,
    )
