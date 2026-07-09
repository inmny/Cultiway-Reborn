#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""输出组合后的 SVG 中间产物。"""

from __future__ import annotations

from html import escape
from pathlib import Path

from .colors import resolve_color
from .compose import ComposedArtifact
from .geometry import identity_transform, primitive_points


def write_svg(composed: ComposedArtifact, output_path: Path, size: int = 28) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {size} {size}" shape-rendering="geometricPrecision">',
        f'  <title>{escape(composed.template.key)} {escape(str(composed.sample_index))}</title>',
    ]
    for placed in sorted(composed.modules, key=lambda item: item.placement.z):
        lines.append(f'  <g id="{escape(placed.id)}">')
        for primitive in sorted(placed.variant.shapes, key=lambda item: int(item.get("z", 0))):
            lines.append("    " + primitive_to_svg(primitive, placed.transform, placed.palette))
        lines.append("  </g>")
    if composed.template.shading:
        lines.append('  <g id="template_shading">')
        for shade in composed.template.shading:
            lines.append("    " + shade_to_svg(shade))
        lines.append("  </g>")
    lines.append("</svg>")
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def primitive_to_svg(primitive: dict, transform, palette: dict[str, str]) -> str:
    kind = primitive.get("type")
    points = primitive_points(primitive, transform)
    attrs = style_attrs(primitive, palette, transform.average_scale)
    if kind == "line":
        return f'<polyline points="{format_points(points)}" {attrs} fill="none" />'
    return f'<polygon points="{format_points(points)}" {attrs} />'


def shade_to_svg(shade: dict) -> str:
    points = primitive_points(shade, identity_transform())
    opacity = float(shade.get("opacity", 0.18))
    mode = str(shade.get("mode", "darken"))
    color = "#ffffff" if mode == "lighten" else "#000000"
    if shade.get("type") == "line":
        width = float(shade.get("stroke_width", 1))
        return f'<polyline points="{format_points(points)}" fill="none" stroke="{color}" stroke-width="{width:.3g}" opacity="{opacity:.3g}" />'
    return f'<polygon points="{format_points(points)}" fill="{color}" opacity="{opacity:.3g}" />'


def style_attrs(primitive: dict, palette: dict[str, str], scale: float) -> str:
    opacity = float(primitive.get("opacity", 1.0))
    fill = resolve_color(primitive.get("fill"), palette, opacity)
    stroke = resolve_color(primitive.get("stroke"), palette, opacity)
    parts: list[str] = []
    if fill is None:
        parts.append('fill="none"')
    else:
        parts.append(f'fill="{rgba_to_svg(fill)}"')
        if fill.a < 255:
            parts.append(f'fill-opacity="{fill.a / 255:.3g}"')
    if stroke is None:
        parts.append('stroke="none"')
    else:
        parts.append(f'stroke="{rgba_to_svg(stroke)}"')
        parts.append(f'stroke-width="{float(primitive.get("stroke_width", 0)) * scale:.3g}"')
        if stroke.a < 255:
            parts.append(f'stroke-opacity="{stroke.a / 255:.3g}"')
    return " ".join(parts)


def rgba_to_svg(color) -> str:
    return f"#{color.r:02x}{color.g:02x}{color.b:02x}"


def format_points(points) -> str:
    return " ".join(f"{x:.3g},{y:.3g}" for x, y in points)

