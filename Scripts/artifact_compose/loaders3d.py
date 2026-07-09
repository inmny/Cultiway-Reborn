#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""读取 3D 模块和模板配置。"""

from __future__ import annotations

import json
from pathlib import Path

from .models3d import Catalog3D, ColorScheme3D, ModuleDef3D, TemplateDef3D


def default_data3d_dir() -> Path:
    return Path(__file__).resolve().parent / "data3d"


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as file:
        return json.load(file)


def load_catalog3d(data_dir: Path | None = None) -> Catalog3D:
    root = data_dir or default_data3d_dir()
    modules_data = load_json(root / "modules.json")
    templates_data = load_json(root / "templates.json")
    colors_path = root / "colors.json"
    colors_data = load_json(colors_path) if colors_path.exists() else {"schemes": []}
    modules = {
        module.key: module
        for module in (ModuleDef3D.from_json(item) for item in modules_data.get("modules", []))
    }
    templates = {
        template.key: template
        for template in (TemplateDef3D.from_json(item) for item in templates_data.get("templates", []))
    }
    color_schemes = {
        scheme.key: scheme
        for scheme in (ColorScheme3D.from_json(item) for item in colors_data.get("schemes", []))
    }
    return Catalog3D(
        canvas=int(modules_data.get("canvas", templates_data.get("canvas", 28))),
        modules=modules,
        templates=templates,
        color_schemes=color_schemes,
    )
