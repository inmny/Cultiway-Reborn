#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""法袍模块模型。"""

from __future__ import annotations

from .builder import ModelBuilder
from .motifs import cloud_pair, diamond_rune, jewel, star_rune, tassel


def build_robe(module: str, variant: str) -> ModelBuilder | None:
    factory = _FACTORIES.get(module)
    return factory(variant) if factory else None


def robe_body(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "cloud_fall":
        mesh.cloth_panel(
            "cloud_fall_body", "cloth", "silk", 0.70, 1.88, 0.12,
            flare=0.72, curve=0.11, wave=0.035, folds=4, segments_x=8, segments_y=10,
            offset=(0, 0, 0.01),
        )
        for x, sign in ((-0.17, -1), (0.17, 1)):
            panel = ModelBuilder()
            panel.cloth_panel(
                "front_skirt", "cloth", "silk", 0.31, 1.22, 0.045,
                flare=0.34, curve=0.025, wave=0.018, folds=2, segments_x=4, segments_y=7,
            )
            mesh.extend(panel, rotation=(0, sign * 4, sign * 1.5), offset=(x, -0.28, 0.13), prefix=f"cloud_front_{sign}_")
        mesh.ribbon("cloud_fall_overlap", "fold", "silk", ((-0.09, 0.70), (0.08, 0.28), (-0.04, -0.80)), 0.035, 0.025, (0, 0, 0.20))
        cloud_pair(mesh, "body_cloud", (0, -0.63, 0.20), 0.38, material="fold", surface="polished_metal", depth=0.035)
    elif variant == "crane_split":
        for sign in (-1, 1):
            panel = ModelBuilder()
            panel.cloth_panel(
                "crane_panel", "cloth", "silk", 0.43, 1.86, 0.10,
                flare=0.68, curve=0.07, wave=0.030, folds=3, segments_x=6, segments_y=10,
            )
            mesh.extend(panel, rotation=(0, sign * 5, sign * 2), offset=(sign * 0.19, 0, 0.02), prefix=f"crane_{sign}_")
            mesh.ribbon(
                f"crane_fold_{sign}", "fold", "silk",
                ((sign * 0.10, 0.72), (sign * 0.25, -0.72)), 0.035, 0.025, (0, 0, 0.17),
            )
        mesh.ribbon("crane_split_line", "trim", "polished_metal", ((0, 0.72), (0, -0.56)), 0.028, 0.025, (0, 0, 0.19))
        for sign in (-1, 1):
            mesh.ribbon(f"crane_feather_{sign}", "fold", "polished_metal", ((sign * 0.18, 0.16), (sign * 0.40, -0.44)), 0.035, 0.025, (0, 0, 0.18))
    else:
        mesh.cloth_panel(
            "star_veil_body", "cloth", "silk", 0.74, 1.88, 0.10,
            flare=0.62, curve=0.12, wave=0.04, folds=5, segments_x=9, segments_y=11,
            offset=(0, 0, 0.01),
        )
        veil = ModelBuilder()
        veil.cloth_panel(
            "star_veil_layer", "cloth", "crystal", 0.58, 1.72, 0.03,
            flare=0.56, curve=0.05, wave=0.025, folds=4, segments_x=7, segments_y=10,
        )
        mesh.extend(veil, offset=(0, -0.06, 0.15))
        for index, (x, y) in enumerate(((-0.30, 0.30), (0.22, 0.46), (-0.10, -0.10), (0.34, -0.34), (-0.30, -0.58))):
            star_rune(mesh, f"veil_star_{index}", (x, y, 0.205), 0.045, points=4, material="fold", surface="emissive")
    return mesh


def robe_collar(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "crossed_jade":
        mesh.extrude("collar_left", "trim", "polished_metal", ((-0.40, 0.18), (-0.18, 0.22), (0.10, -0.20), (-0.04, -0.27)), 0.11, (0, 0, 0.03))
        mesh.extrude("collar_right", "trim", "polished_metal", ((0.40, 0.18), (0.18, 0.22), (-0.10, -0.20), (0.04, -0.27)), 0.11, (0, 0, 0.05))
        jewel(mesh, "collar_jade", (0, -0.12, 0.14), 0.09, material="gem", surface="jade")
    elif variant == "high_cloud":
        mesh.extrude("high_collar", "trim", "silk", ((-0.34, -0.18), (-0.30, 0.18), (-0.12, 0.24), (0, 0.08), (0.12, 0.24), (0.30, 0.18), (0.34, -0.18), (0, -0.02)), 0.14)
        cloud_pair(mesh, "collar_cloud", (0, 0.08, 0.11), 0.34, material="edge", surface="polished_metal", depth=0.05)
    else:
        for index, x in enumerate((-0.26, -0.13, 0.13, 0.26)):
            mesh.extrude(f"feather_collar_{index}", "main", "silk", ((-0.10, -0.16), (-0.07, 0.10), (0, 0.22), (0.07, 0.10), (0.10, -0.16)), 0.10, (x, 0, 0.02 + index * 0.01))
        jewel(mesh, "feather_collar_gem", (0, -0.02, 0.14), 0.075, material="gem", surface="emissive")
    return mesh


def robe_sleeves(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "flowing_pair":
        for sign in (-1, 1):
            sleeve = ModelBuilder()
            sleeve.cloth_panel(
                "flowing_sleeve", "cloth", "silk", 0.34, 1.18, 0.10,
                flare=0.82, curve=0.055, wave=0.032, folds=3, segments_x=5, segments_y=8,
            )
            mesh.extend(sleeve, rotation=(0, sign * 4, sign * 74), offset=(sign * 0.66, 0.10, 0.01), prefix=f"flowing_{sign}_")
            mesh.ribbon(
                f"flowing_trim_{sign}", "trim", "polished_metal",
                ((sign * 0.98, -0.02), (sign * 1.18, -0.18)), 0.055, 0.035, (0, 0, 0.14),
            )
    elif variant == "narrow_pair":
        for sign in (-1, 1):
            sleeve = ModelBuilder()
            sleeve.cloth_panel(
                "narrow_sleeve", "cloth", "silk", 0.25, 1.05, 0.09,
                flare=0.30, curve=0.045, wave=0.018, folds=2, segments_x=4, segments_y=7,
            )
            mesh.extend(sleeve, rotation=(0, sign * 3, sign * 72), offset=(sign * 0.59, 0.10, 0.01), prefix=f"narrow_{sign}_")
            mesh.beveled_box(f"narrow_cuff_{sign}", "trim", "polished_metal", (0.16, 0.27, 0.10), 0.035, (sign * 1.08, -0.08, 0.10))
    else:
        for side in (-1, 1):
            for index in range(3):
                feather = ModelBuilder()
                feather.cloth_panel(
                    "feather", "main", "silk", 0.20, 0.68 - index * 0.06, 0.065,
                    flare=0.62, curve=0.025, wave=0.018, folds=2, segments_x=3, segments_y=5,
                )
                mesh.extend(
                    feather,
                    rotation=(0, side * (2 + index * 2), side * (64 + index * 7)),
                    offset=(side * (0.43 + index * 0.25), 0.08 - index * 0.05, 0.02 + index * 0.015),
                    prefix=f"feather_sleeve_{side}_{index}_",
                )
            jewel(mesh, f"feather_cuff_{side}", (side * 1.02, -0.12, 0.13), 0.06, material="gem", surface="emissive")
    return mesh


def robe_belt(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "jade_sash":
        mesh.ribbon("jade_sash", "trim", "silk", ((-0.50, 0.015), (-0.25, -0.01), (0, -0.025), (0.25, -0.01), (0.50, 0.015)), 0.14, 0.08, (0, 0, 0.06))
        mesh.ribbon("sash_edge", "edge", "polished_metal", ((-0.48, 0.065), (0, 0.025), (0.48, 0.065)), 0.025, 0.025, (0, 0, 0.12))
        jewel(mesh, "sash_jade", (0, -0.02, 0.15), 0.075, material="gem", surface="jade")
        tassel(mesh, "sash_charm", (0.23, -0.07, 0.12), 0.40, strands=2, material="charm")
    elif variant == "ring_belt":
        mesh.ribbon("ring_belt", "trim", "aged_metal", ((-0.51, 0.02), (-0.26, -0.01), (0, -0.025), (0.26, -0.01), (0.51, 0.02)), 0.12, 0.08, (0, 0, 0.06))
        for x in (-0.34, -0.17, 0, 0.17, 0.34):
            mesh.ring(f"belt_ring_{x}", "edge", "polished_metal", (0.07, 0.07), (0.04, 0.04), 0.05, 10, (x, -0.02, 0.13))
        jewel(mesh, "ring_belt_gem", (0, -0.02, 0.16), 0.05, material="gem", surface="emissive")
    else:
        mesh.ribbon("cloud_belt", "trim", "silk", ((-0.48, 0.02), (-0.24, -0.01), (0, -0.02), (0.24, -0.01), (0.48, 0.02)), 0.13, 0.08, (0, 0, 0.06))
        cloud_pair(mesh, "belt_cloud", (0, 0, 0.14), 0.36, material="trim", surface="polished_metal", depth=0.04)
        tassel(mesh, "cloud_knot", (0, -0.06, 0.15), 0.44, strands=3, material="charm")
    return mesh


def robe_hem(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "wave_border":
        mesh.ribbon("wave_border", "trim", "polished_metal", ((-0.62, 0.18), (-0.34, 0.04), (0, 0.16), (0.34, 0.04), (0.62, 0.18)), 0.10, 0.08, (0, 0, 0.03))
        mesh.ribbon("wave_inner", "main", "emissive", ((-0.56, 0.04), (-0.28, -0.10), (0, 0.02), (0.28, -0.10), (0.56, 0.04)), 0.05, 0.05, (0, 0, 0.10))
    elif variant == "split_ribbons":
        for x in (-0.30, 0.30):
            mesh.ribbon(f"hem_ribbon_{x}", "trim", "silk", ((x, 0.28), (x * 1.18, -0.30)), 0.18, 0.10, (0, 0, 0.02))
            jewel(mesh, f"hem_gem_{x}", (x, 0.20, 0.12), 0.065, material="gem", surface="emissive")
    else:
        mesh.beveled_box("talisman_hem", "trim", "polished_metal", (1.14, 0.16, 0.09), 0.04, (0, 0.22, 0.02))
        for index, x in enumerate((-0.45, -0.22, 0, 0.22, 0.45)):
            mesh.beveled_box(f"hem_talisman_{index}", "main", "silk", (0.16, 0.42, 0.07), 0.04, (x, -0.06, 0.06))
            diamond_rune(mesh, f"hem_script_{index}", (x, -0.06, 0.12), 0.045, material="main", surface="emissive")
    return mesh


def robe_panel(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "wide_blue":
        width, flare, wave = 0.92, 0.70, 0.035
    else:
        width, flare, wave = 0.82, 0.62, 0.028
    mesh.cloth_panel(
        "legacy_robe", "cloth", "silk", width, 1.94, 0.12,
        flare=flare, curve=0.11, wave=wave, folds=4, segments_x=9, segments_y=11,
        offset=(0, -0.20, 0.02),
    )
    for sign in (-1, 1):
        sleeve = ModelBuilder()
        sleeve.cloth_panel(
            "legacy_sleeve", "cloth", "silk", 0.33, 1.00, 0.09,
            flare=0.58, curve=0.045, wave=0.025, folds=3, segments_x=5, segments_y=7,
        )
        mesh.extend(sleeve, rotation=(0, sign * 4, sign * 70), offset=(sign * 0.60, 0.22, 0.01), prefix=f"legacy_sleeve_{sign}_")
    mesh.extrude("legacy_collar", "trim", "polished_metal", ((-0.30, 0.60), (-0.12, 0.76), (0, 0.46), (0.12, 0.76), (0.30, 0.60), (0, 0.35)), 0.065, (0, 0, 0.17))
    mesh.ribbon("legacy_belt", "trim", "silk", ((-0.50, -0.16), (0, -0.20), (0.50, -0.16)), 0.13, 0.07, (0, 0, 0.17))
    mesh.ribbon("legacy_overlap", "fold", "silk", ((-0.10, 0.52), (0.08, 0.12), (-0.04, -0.92)), 0.035, 0.025, (0, 0, 0.20))
    tassel(mesh, "legacy_charm", (0.24, -0.24, 0.18), 0.54, strands=2, material="charm")
    return mesh


_FACTORIES = {
    "robe_body3d": robe_body,
    "robe_collar3d": robe_collar,
    "robe_sleeves3d": robe_sleeves,
    "robe_belt3d": robe_belt,
    "robe_hem3d": robe_hem,
    "robe_panel3d": robe_panel,
}
