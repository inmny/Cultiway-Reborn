#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""旗、钟、扇、葫芦四类法器的模块模型。"""

from __future__ import annotations

import math

from .builder import ModelBuilder, crescent_outline
from .motifs import bead_chain, cloud_pair, diamond_rune, flame, jewel, lotus, small_bell, star_rune, tassel


def build_relic(module: str, variant: str) -> ModelBuilder | None:
    factory = _FACTORIES.get(module)
    return factory(variant) if factory else None


def banner_pole(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "black_iron_spine":
        mesh.lathe("iron_spine", "metal", "aged_metal", ((0.10, -1.12), (0.13, -0.96), (0.11, 0.96), (0.08, 1.12)), 8)
        for y in (-0.78, -0.26, 0.26, 0.78):
            mesh.torus(f"spine_band_{y}", "ridge", "polished_metal", 0.12, 0.025, 8, 4, (0, y, 0))
    elif variant == "jade_bamboo_staff":
        mesh.lathe("jade_staff", "handle", "jade", ((0.105, -1.12), (0.12, 1.12)), 10)
        for y in (-0.72, -0.18, 0.36, 0.90):
            mesh.torus(f"bamboo_node_{y}", "edge", "polished_metal", 0.125, 0.030, 10, 4, (0, y, 0))
        diamond_rune(mesh, "staff_rune", (0, 0.10, 0.13), 0.10, material="edge", surface="emissive")
    else:
        mesh.lathe("bone_standard", "pommel", "bone", ((0.14, -1.12), (0.10, -0.80), (0.13, -0.20), (0.09, 0.45), (0.12, 1.12)), 8)
        for y, angle in ((-0.58, 24), (0.06, -26), (0.68, 20)):
            mesh.ribbon(f"bone_carving_{y}", "ridge", "aged_metal", ((-0.10, -0.13), (0.10, 0.13)), 0.035, 0.05, (0, y, 0.10))
    return mesh


def banner_cloth(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "cloud_war_banner":
        mesh.cloth_panel(
            "war_banner", "cloth", "silk", 1.14, 1.02, 0.10,
            flare=0.02, curve=0.07, wave=0.055, folds=4, segments_x=9, segments_y=7,
            offset=(0.58, 0, 0.01),
        )
        mesh.ribbon("banner_top_trim", "trim", "polished_metal", ((0.03, 0.50), (0.58, 0.54), (1.13, 0.49)), 0.045, 0.035, (0, 0, 0.14))
        mesh.ribbon("banner_fold_upper", "fold", "silk", ((0.08, 0.34), (0.56, 0.26), (1.06, 0.34)), 0.045, 0.03, (0, 0, 0.13))
        mesh.ribbon("banner_fold_lower", "fold", "silk", ((0.08, -0.22), (0.62, -0.13), (1.07, -0.27)), 0.040, 0.03, (0, 0, 0.13))
        cloud_pair(mesh, "banner_cloud", (0.66, 0.02, 0.15), 0.38, material="trim", surface="polished_metal", depth=0.045)
    elif variant == "swallow_tail_pennon":
        mesh.cloth_panel(
            "swallow_root", "cloth", "silk", 0.54, 1.00, 0.09,
            flare=0.02, curve=0.045, wave=0.035, folds=3, segments_x=5, segments_y=7,
            offset=(0.28, 0, 0.01),
        )
        for sign in (-1, 1):
            tail = ModelBuilder()
            tail.cloth_panel(
                "swallow_tail", "cloth", "silk", 0.64, 0.42, 0.09,
                flare=0.10, curve=0.055, wave=0.050, folds=3, segments_x=6, segments_y=4,
            )
            mesh.extend(tail, rotation=(0, sign * 3, sign * 5), offset=(0.80, sign * 0.28, 0.01), prefix=f"swallow_{sign}_")
            mesh.ribbon(
                f"pennon_fold_{sign}", "fold", "silk",
                ((0.12, sign * 0.30), (0.62, sign * 0.24), (1.06, sign * 0.34)), 0.040, 0.03, (0, 0, 0.13),
            )
        mesh.ribbon("swallow_mount", "trim", "polished_metal", ((0.02, -0.50), (0.02, 0.50)), 0.045, 0.04, (0, 0, 0.12))
        jewel(mesh, "pennon_gem", (0.20, 0, 0.16), 0.085, material="gem", surface="jade")
    else:
        mesh.cloth_panel(
            "spirit_streamer", "cloth", "silk", 0.92, 1.04, 0.085,
            flare=0.08, curve=0.075, wave=0.060, folds=4, segments_x=8, segments_y=7,
            offset=(0.48, 0, 0.01),
        )
        mesh.ribbon("streamer_border", "trim", "polished_metal", ((0.04, 0.49), (0.48, 0.53), (0.91, 0.43)), 0.045, 0.035, (0, 0, 0.13))
        for index, y in enumerate((0.25, 0.0, -0.25)):
            diamond_rune(mesh, f"script_{index}", (0.42 + index * 0.09, y, 0.15), 0.075, material="main", surface="emissive")
        mesh.ribbon("streamer_tail", "cloth", "silk", ((0.82, -0.42), (1.02, -0.68)), 0.14, 0.06, (0, 0, 0.06))
    return mesh


def banner_finial(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "spearhead":
        mesh.extrude("spearhead", "edge", "polished_metal", ((0, 0.56), (0.17, 0.18), (0.08, -0.08), (0, -0.16), (-0.08, -0.08), (-0.17, 0.18)), 0.20)
        mesh.extrude("spear_ridge", "rim", "emissive", ((0, 0.48), (0.045, 0.12), (0, -0.04), (-0.045, 0.12)), 0.06, (0, 0, 0.13))
    elif variant == "crescent_moon":
        mesh.box("crescent_socket", "edge", "polished_metal", (0.12, 0.22, 0.14), (0, -0.05, 0))
        mesh.extrude("moon_finial", "edge", "polished_metal", crescent_outline(0.26, 0.18, samples=8), 0.15, (0, 0.28, 0))
        jewel(mesh, "moon_gem", (0.02, 0.30, 0.10), 0.09, material="gem", surface="emissive")
    else:
        mesh.box("beast_socket", "metal", "aged_metal", (0.16, 0.22, 0.16), (0, -0.04, 0))
        mesh.extrude("beast_brow", "edge", "polished_metal", ((-0.17, 0.17), (-0.08, 0.40), (0, 0.30), (0.08, 0.40), (0.17, 0.17), (0, 0.05)), 0.18, (0, 0.08, 0))
        for x in (-0.10, 0.10):
            jewel(mesh, f"beast_eye_{x}", (x, 0.28, 0.12), 0.035, material="edge", surface="emissive")
    return mesh


def banner_tassel(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "twin_silk":
        jewel(mesh, "twin_knot", (0, 0.18, 0), 0.12, material="gem", surface="jade")
        for x in (-0.09, 0.09):
            mesh.ribbon(f"silk_{x}", "charm", "silk", ((0, 0), (x * 0.5, -0.26), (x, -0.62)), 0.07, 0.06, (0, 0.10, 0.03))
    elif variant == "spirit_bells":
        mesh.ring("bell_knot", "charm", "silk", (0.12, 0.12), (0.07, 0.07), 0.08, 8, (0, 0.10, 0))
        for x in (-0.12, 0.12):
            mesh.ribbon(f"bell_cord_{x}", "charm", "silk", ((0, 0), (x * 0.45, -0.16), (x, -0.30)), 0.035, 0.04, (0, 0.08, 0))
            small_bell(mesh, f"spirit_bell_{x}", (x, -0.26, 0), 0.28, material="rim")
    else:
        mesh.ribbon("tablet_cord", "wrap", "silk", ((0, 0), (0.03, -0.18)), 0.05, 0.04, (0, 0.18, 0))
        mesh.beveled_box("jade_tablet", "gem", "jade", (0.30, 0.45, 0.10), 0.06, (0, -0.22, 0))
        diamond_rune(mesh, "tablet_glint", (0, -0.20, 0.08), 0.08, material="glint", surface="emissive")
    return mesh


def bell_body(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "ancient_bronze":
        profile = ((0.22, 0.56), (0.37, 0.42), (0.43, 0.10), (0.46, -0.28), (0.53, -0.58))
        mesh.lathe("ancient_bell", "metal", "aged_metal", profile, 12, cap_bottom=False)
        for y in (0.24, -0.18):
            mesh.torus(f"bronze_band_{y}", "ridge", "polished_metal", 0.44 + (0.08 if y < 0 else 0), 0.035, 12, 4, (0, y, 0))
        diamond_rune(mesh, "bronze_inscription", (0, -0.04, 0.47), 0.14, material="main", surface="emissive")
    elif variant == "jade_temple_bell":
        profile = ((0.20, 0.56), (0.34, 0.44), (0.40, 0.02), (0.50, -0.56))
        mesh.lathe("jade_bell", "metal", "jade", profile, 14, cap_bottom=False)
        lotus(mesh, "temple_lotus", (0, -0.13, 0.45), 0.18, petals=5, material="edge", surface="polished_metal")
        for y in (0.34, -0.38):
            mesh.torus(f"jade_bell_band_{y}", "edge", "polished_metal", 0.38 if y > 0 else 0.50, 0.032, 14, 4, (0, y, 0))
    else:
        profile = ((0.18, 0.56), (0.36, 0.40), (0.45, 0.06), (0.52, -0.56))
        mesh.lathe("thunder_bell", "right", "aged_metal", profile, 10, cap_bottom=False)
        mesh.ribbon("thunder_vein_left", "glint", "emissive", ((-0.10, 0.34), (0.06, 0.12), (-0.08, -0.08), (0.12, -0.34)), 0.06, 0.05, (0, 0, 0.48))
        mesh.ribbon("thunder_vein_right", "rim", "polished_metal", ((0.18, 0.42), (0.02, 0.18), (0.18, -0.02)), 0.045, 0.04, (0, 0, 0.46))
    return mesh


def bell_mouth(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "lotus_lip":
        mesh.torus("lotus_mouth", "rim", "polished_metal", 0.52, 0.075, 14, 5)
        petal = ModelBuilder()
        petal.extrude("lip_petal", "main", "jade", ((-0.08, 0), (0, 0.14), (0.08, 0), (0, -0.07)), 0.08)
        for index in range(8):
            angle = math.tau * index / 8
            mesh.extend(petal, rotation=(0, math.degrees(angle), 0), offset=(math.cos(angle) * 0.50, 0.04, math.sin(angle) * 0.50), prefix=f"petal_{index}_")
    elif variant == "eight_tone_ring":
        mesh.torus("tone_ring_outer", "rim", "polished_metal", 0.53, 0.075, 16, 5)
        for index in range(8):
            angle = math.tau * index / 8
            jewel(mesh, f"tone_{index}", (math.cos(angle) * 0.54, -0.02, math.sin(angle) * 0.54), 0.055, material="main", surface="emissive")
    else:
        mesh.torus("wave_lip", "rim", "polished_metal", 0.54, 0.07, 14, 5)
        for index in range(6):
            angle = math.tau * index / 6
            mesh.torus(f"wave_glyph_{index}", "glint", "emissive", 0.065, 0.018, 8, 4, (math.cos(angle) * 0.50, 0.05, math.sin(angle) * 0.50))
        mesh.torus("mouth_inner", "main", "aged_metal", 0.43, 0.025, 14, 4, (0, -0.03, 0))
    return mesh


def bell_clapper(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "iron_heart":
        mesh.lathe("iron_stem", "handle", "aged_metal", ((0.055, 0.31), (0.06, -0.26)), 8)
        mesh.ellipsoid("iron_heart", "metal", "aged_metal", (0.14, 0.18, 0.12), 8, 4, (0, -0.34, 0))
    elif variant == "jade_tongue":
        mesh.ribbon("jade_cord", "wrap", "silk", ((0, 0.31), (0.03, -0.12)), 0.055, 0.045)
        mesh.beveled_box("jade_tongue", "gem", "jade", (0.22, 0.48, 0.11), 0.07, (0, -0.24, 0))
        diamond_rune(mesh, "tongue_glint", (0, -0.22, 0.08), 0.07, material="glint", surface="emissive")
    else:
        mesh.ribbon("thunder_chain", "ridge", "polished_metal", ((0, 0.31), (-0.04, 0.02), (0.04, -0.20)), 0.05, 0.05)
        jewel(mesh, "thunder_seed", (0, -0.34, 0), 0.16, material="core", surface="emissive", squash=1.25)
        star_rune(mesh, "seed_glint", (0, -0.34, 0.14), 0.08, points=5)
    return mesh


def bell_crown(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "dragon_loop":
        mesh.ring("dragon_loop", "handle", "aged_metal", (0.27, 0.30), (0.15, 0.18), 0.13, 12, (0, 0.10, 0))
        for x in (-0.20, 0.20):
            jewel(mesh, f"dragon_eye_{x}", (x, 0.25, 0.08), 0.045, material="gem", surface="emissive")
    elif variant == "lotus_hook":
        mesh.extrude("lotus_hook", "main", "jade", ((-0.22, -0.18), (-0.16, 0.16), (0, 0.37), (0.16, 0.16), (0.22, -0.18), (0, -0.04)), 0.15)
        lotus(mesh, "hook_lotus", (0, 0.20, 0.10), 0.14, petals=5, material="edge")
    else:
        mesh.extrude("heavenly_arch", "handle", "polished_metal", ((-0.28, -0.18), (-0.26, 0.13), (-0.14, 0.35), (0, 0.22), (0.14, 0.35), (0.26, 0.13), (0.28, -0.18), (0.15, -0.08), (0, 0.06), (-0.15, -0.08)), 0.15)
        diamond_rune(mesh, "arch_core", (0, 0.16, 0.11), 0.08)
    return mesh


def fan_ribs(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    configs = {
        "gilded_six_ribs": (6, "ridge", "polished_metal", 0.08),
        "jade_seven_ribs": (7, "handle", "jade", 0.07),
        "cloud_bone_ribs": (5, "pommel", "bone", 0.09),
    }
    count, material, surface, width = configs[variant]
    for index in range(count):
        x = -0.78 + 1.56 * index / max(1, count - 1)
        mesh.extrude(
            f"rib_{index:02d}", material, surface,
            ((-width, -0.38), (x - width * 0.35, 0.66), (x + width * 0.35, 0.66), (width, -0.38)),
            0.08,
            (0, 0, 0.03),
        )
    jewel(mesh, "fan_hinge", (0, -0.38, 0.11), 0.13, material="gem", surface="emissive")
    if variant == "gilded_six_ribs":
        mesh.ribbon("gilded_arc", "edge", "polished_metal", ((-0.72, 0.52), (0, 0.70), (0.72, 0.52)), 0.06, 0.05, (0, 0, 0.12))
    elif variant == "jade_seven_ribs":
        for x in (-0.48, 0, 0.48):
            jewel(mesh, f"rib_jade_{x}", (x, 0.38, 0.12), 0.06, material="gem", surface="emissive")
    else:
        cloud_pair(mesh, "bone_cloud", (0, 0.34, 0.13), 0.52, material="glint", surface="emissive", depth=0.04)
    return mesh


def fan_leaf(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "cloud_silk_leaf":
        outline = ((-0.86, 0.22), (-0.72, 0.54), (-0.30, 0.71), (0, 0.74), (0.30, 0.71), (0.72, 0.54), (0.86, 0.22), (0, -0.38))
        mesh.extrude("cloud_silk", "cloth", "silk", outline, 0.08, (0, 0, 0.02))
        cloud_pair(mesh, "leaf_cloud", (0, 0.30, 0.10), 0.60, material="trim", surface="polished_metal", depth=0.04)
        for x in (-0.48, 0.48):
            mesh.ribbon(f"silk_fold_{x}", "fold", "silk", ((0, -0.28), (x * 0.58, 0.18), (x, 0.52)), 0.045, 0.04, (0, 0, 0.10))
    elif variant == "crane_feather_leaf":
        for index in range(7):
            x = -0.72 + index * 0.24
            mesh.extrude(
                f"feather_{index:02d}", "cloth" if index % 2 else "fold", "silk",
                ((-0.10, -0.34), (x - 0.14, 0.36), (x, 0.72), (x + 0.14, 0.36), (0.10, -0.34)),
                0.07,
                (0, 0, 0.02 + index * 0.004),
            )
        jewel(mesh, "crane_eye", (0, 0.28, 0.14), 0.09, material="gem", surface="emissive")
    else:
        outline = ((-0.88, 0.18), (-0.72, 0.58), (-0.28, 0.72), (0.28, 0.72), (0.72, 0.58), (0.88, 0.18), (0, -0.38))
        mesh.extrude("iron_leaf", "metal", "aged_metal", outline, 0.13, (0, 0, 0.02), side_material="edge", side_surface="polished_metal")
        for angle in (-35, 0, 35):
            blade = ModelBuilder()
            blade.ribbon("iron_vein", "main", "polished_metal", ((0, -0.26), (0, 0.54)), 0.055, 0.045, (0, 0, 0.12))
            mesh.extend(blade, rotation=(0, 0, angle), prefix=f"vein_{angle}_")
    return mesh


def fan_handle(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "bronze_cloud_handle":
        mesh.lathe("bronze_handle", "handle", "aged_metal", ((0.10, -0.34), (0.12, 0.34)), 8)
        cloud_pair(mesh, "handle_cloud", (0, -0.04, 0.12), 0.22, material="ridge", surface="polished_metal", depth=0.05)
        for y in (-0.26, 0.25):
            mesh.torus(f"handle_band_{y}", "ridge", "polished_metal", 0.12, 0.025, 8, 4, (0, y, 0))
    elif variant == "jade_spine_handle":
        mesh.lathe("jade_handle", "handle", "jade", ((0.09, -0.34), (0.12, -0.24), (0.10, 0.24), (0.08, 0.34)), 10)
        jewel(mesh, "handle_jewel", (0, -0.18, 0.11), 0.08, material="gem", surface="emissive")
    else:
        mesh.lathe("bone_handle", "pommel", "bone", ((0.12, -0.34), (0.09, -0.10), (0.13, 0.12), (0.09, 0.34)), 8)
        for y in (-0.18, 0.05, 0.27):
            mesh.ribbon(f"talisman_wrap_{y}", "wrap", "silk", ((-0.11, -0.05), (0.11, 0.05)), 0.045, 0.05, (0, y, 0.08))
    return mesh


def fan_pendant(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "red_double_tassel":
        tassel(mesh, "double_tassel", (0, 0.16, 0), 0.54, strands=2)
    elif variant == "moon_ring_pendant":
        mesh.ribbon("moon_cord", "wrap", "silk", ((0, 0.16), (0, -0.04)), 0.045, 0.04)
        mesh.ring("moon_ring", "rim", "polished_metal", (0.14, 0.14), (0.085, 0.085), 0.07, 10, (0, -0.14, 0))
        star_rune(mesh, "moon_glint", (0, -0.14, 0.06), 0.06, points=4)
    else:
        mesh.ribbon("tablet_cord", "wrap", "silk", ((0, 0.16), (0, -0.02)), 0.045, 0.04)
        mesh.beveled_box("fan_tablet", "gem", "jade", (0.25, 0.34, 0.09), 0.05, (0, -0.20, 0))
        diamond_rune(mesh, "tablet_glint", (0, -0.20, 0.07), 0.06, material="glint", surface="emissive")
    return mesh


def gourd_body(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "golden_double_calabash":
        mesh.lathe(
            "golden_gourd", "metal", "aged_metal",
            ((0.14, 0.84), (0.29, 0.70), (0.38, 0.52), (0.36, 0.34),
             (0.19, 0.14), (0.25, 0.02), (0.46, -0.12), (0.54, -0.38),
             (0.47, -0.68), (0.20, -0.90)),
            16,
        )
        for y, radius, thickness in ((0.16, 0.22, 0.032), (-0.10, 0.46, 0.027), (-0.65, 0.43, 0.030)):
            mesh.torus(f"gold_band_{y}", "ridge", "polished_metal", radius, thickness, 16, 4, (0, y, 0))
        flute = ModelBuilder()
        flute.ribbon("lower_flute", "ridge", "polished_metal", ((0, -0.14), (0, -0.62)), 0.026, 0.022, (0, 0, 0.555))
        for index in range(8):
            mesh.extend(flute, rotation=(0, index * 45, 0), prefix=f"gold_flute_{index:02d}_")
        cloud_pair(mesh, "golden_cloud", (0, 0.48, 0.35), 0.30, material="edge", surface="polished_metal", depth=0.035)
        jewel(mesh, "golden_gem", (0, -0.25, 0.56), 0.085, material="gem", surface="emissive")
    elif variant == "jade_spirit_gourd":
        mesh.lathe(
            "jade_gourd", "right", "jade",
            ((0.13, 0.84), (0.28, 0.70), (0.38, 0.52), (0.35, 0.34),
             (0.18, 0.14), (0.24, 0.02), (0.44, -0.13), (0.53, -0.39),
             (0.46, -0.70), (0.18, -0.90)),
            16,
        )
        mesh.torus("jade_waist", "ridge", "polished_metal", 0.21, 0.030, 16, 4, (0, 0.15, 0))
        mesh.torus("jade_lower_band", "edge", "jade", 0.48, 0.024, 16, 4, (0, -0.60, 0))
        mesh.ribbon("jade_vine", "edge", "polished_metal", ((-0.24, 0.48), (0.18, 0.18), (-0.14, -0.18), (0.22, -0.54)), 0.052, 0.042, (0, 0, 0.50))
        cloud_pair(mesh, "jade_cloud", (0, -0.27, 0.55), 0.36, material="ridge", surface="jade", depth=0.032)
        star_rune(mesh, "spirit_glint", (0, -0.24, 0.57), 0.085, points=5, material="glint")
    else:
        mesh.lathe(
            "void_gourd", "right", "aged_metal",
            ((0.13, 0.84), (0.29, 0.69), (0.39, 0.50), (0.35, 0.32),
             (0.18, 0.13), (0.25, 0.01), (0.46, -0.14), (0.54, -0.40),
             (0.46, -0.70), (0.18, -0.90)),
            14,
        )
        mesh.torus("void_waist", "ridge", "polished_metal", 0.21, 0.034, 14, 4, (0, 0.14, 0))
        mesh.torus("void_lower_seal", "ridge", "aged_metal", 0.50, 0.026, 14, 4, (0, -0.59, 0))
        mesh.torus("void_orbit", "metal", "polished_metal", 0.31, 0.040, 14, 4, (0, -0.28, 0.48))
        mesh.faceted_disc("void_core", "core", "emissive", 0.19, 0.19, 0.07, 0.09, 10, (0, -0.28, 0.57))
        mesh.ribbon("void_script", "main", "emissive", ((-0.17, 0.51), (0.09, 0.34), (-0.09, 0.17)), 0.045, 0.035, (0, 0, 0.35))
    return mesh


def gourd_mouth(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "bronze_neck":
        mesh.lathe("bronze_neck", "rim", "aged_metal", ((0.16, -0.18), (0.17, 0.14), (0.23, 0.24)), 10)
        mesh.torus("neck_edge", "edge", "polished_metal", 0.22, 0.035, 10, 4, (0, 0.19, 0))
    elif variant == "lotus_mouth":
        mesh.lathe("lotus_neck", "metal", "jade", ((0.13, -0.18), (0.15, 0.12), (0.20, 0.20)), 10)
        lotus(mesh, "mouth_lotus", (0, 0.20, 0), 0.18, petals=6, material="main", surface="polished_metal")
    else:
        mesh.lathe("spatial_neck", "right", "aged_metal", ((0.14, -0.18), (0.16, 0.13), (0.24, 0.22)), 10)
        mesh.torus("spatial_rim", "core", "emissive", 0.22, 0.045, 12, 4, (0, 0.19, 0))
        for index in range(3):
            angle = math.tau * index / 3
            jewel(mesh, f"rim_glint_{index}", (math.cos(angle) * 0.22, 0.20, math.sin(angle) * 0.22), 0.045, material="glint", surface="emissive")
    return mesh


def gourd_stopper(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "jade_plug":
        mesh.lathe("jade_plug", "gem", "jade", ((0.14, -0.12), (0.17, 0.06), (0.11, 0.22), (0.04, 0.26)), 10)
        mesh.torus("plug_edge", "edge", "polished_metal", 0.15, 0.025, 10, 4, (0, -0.04, 0))
    elif variant == "cloud_cork":
        mesh.lathe("cloud_cork", "handle", "wood", ((0.16, -0.12), (0.16, 0.10), (0.10, 0.24)), 8)
        cloud_pair(mesh, "cork_cloud", (0, 0.08, 0.14), 0.20, material="ridge", surface="bone", depth=0.05)
    else:
        mesh.lathe("bead_socket", "rim", "polished_metal", ((0.14, -0.12), (0.16, 0.05), (0.10, 0.12)), 10)
        jewel(mesh, "spirit_bead", (0, 0.18, 0), 0.15, material="core", surface="emissive")
        mesh.torus("bead_seal", "edge", "polished_metal", 0.14, 0.025, 10, 4, (0, 0.12, 0))
    return mesh


def gourd_tie(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "red_silk_knot":
        tassel(mesh, "gourd_silk", (0, 0.18, 0), 0.56, strands=3, material="charm")
        mesh.ring("silk_loop", "wrap", "silk", (0.24, 0.17), (0.15, 0.09), 0.07, 10, (0, 0.12, 0))
    elif variant == "vine_binding":
        mesh.ring("vine_loop", "handle", "wood", (0.22, 0.17), (0.15, 0.10), 0.09, 9, (0, 0.14, 0))
        for x in (-0.12, 0.12):
            mesh.extrude(f"vine_leaf_{x}", "gem", "jade", ((0, 0.10), (0.10, 0), (0, -0.15), (-0.08, 0)), 0.06, (x, -0.15, 0.04))
    else:
        points = ((0, 0.18, 0), (-0.08, 0.02, 0), (0.06, -0.12, 0), (-0.04, -0.28, 0))
        bead_chain(mesh, "chain", points, 0.055, material="rim", surface="polished_metal")
        mesh.beveled_box("chain_talisman", "gem", "jade", (0.24, 0.30, 0.08), 0.05, (0, -0.36, 0))
        diamond_rune(mesh, "chain_glint", (0, -0.36, 0.07), 0.055, material="glint", surface="emissive")
    return mesh


_FACTORIES = {
    "banner_pole3d": banner_pole,
    "banner_cloth3d": banner_cloth,
    "banner_finial3d": banner_finial,
    "banner_tassel3d": banner_tassel,
    "bell_body3d": bell_body,
    "bell_mouth3d": bell_mouth,
    "bell_clapper3d": bell_clapper,
    "bell_crown3d": bell_crown,
    "fan_ribs3d": fan_ribs,
    "fan_leaf3d": fan_leaf,
    "fan_handle3d": fan_handle,
    "fan_pendant3d": fan_pendant,
    "gourd_body3d": gourd_body,
    "gourd_mouth3d": gourd_mouth,
    "gourd_stopper3d": gourd_stopper,
    "gourd_tie3d": gourd_tie,
}
