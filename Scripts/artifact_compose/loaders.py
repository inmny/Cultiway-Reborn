#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""读取模块和模板配置。"""

from __future__ import annotations

import json
from pathlib import Path

from .models import Catalog, ModuleDef, TemplateDef


def default_data_dir() -> Path:
    return Path(__file__).resolve().parent / "data"


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as file:
        return json.load(file)


def load_catalog(data_dir: Path | None = None) -> Catalog:
    root = data_dir or default_data_dir()
    modules_data = load_json(root / "modules.json")
    templates_data = load_json(root / "templates.json")

    modules = {
        module.key: module
        for module in (ModuleDef.from_json(item) for item in modules_data.get("modules", []))
    }
    templates = {
        template.key: template
        for template in (TemplateDef.from_json(item) for item in templates_data.get("templates", []))
    }
    return Catalog(
        canvas=int(modules_data.get("canvas", templates_data.get("canvas", 28))),
        modules=modules,
        templates=templates,
    )

