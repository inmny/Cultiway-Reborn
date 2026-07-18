#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""鼎与镜两类法器的模块模型。"""

from __future__ import annotations

import math

from .builder import ModelBuilder
from .motifs import cloud_pair, diamond_rune, flame, jewel, lotus, star_rune, trigram


def build_vessel(module: str, variant: str) -> ModelBuilder | None:
    factory = _FACTORIES.get(module)
    return factory(variant) if factory else None


def _repeat_body_relief(
    target: ModelBuilder,
    panel: ModelBuilder,
    prefix: str,
    *,
    count: int = 4,
) -> None:
    """把薄浮雕沿炉腹重复，保证任意模板视角都能读到器物纹饰。"""
    for index in range(count):
        target.extend(
            panel,
            rotation=(0, 360 * index / count, 0),
            prefix=f"{prefix}_{index:02d}_",
        )


def _taotie_relief(radius: float, y: float) -> ModelBuilder:
    panel = ModelBuilder()
    depth = radius + 0.035
    cloud_pair(
        panel,
        "brow",
        (0, y + 0.07, depth),
        0.43,
        material="ridge",
        surface="polished_metal",
        depth=0.035,
    )
    panel.ribbon(
        "jaw",
        "ridge",
        "aged_metal",
        ((-0.24, y - 0.03), (-0.11, y - 0.12), (0, y - 0.07),
         (0.11, y - 0.12), (0.24, y - 0.03)),
        0.034,
        0.03,
        (0, 0, depth),
    )
    diamond_rune(
        panel,
        "nose",
        (0, y - 0.015, depth + 0.015),
        0.055,
        material="edge",
        surface="polished_metal",
    )
    return panel


def _cloud_relief(radius: float, y: float) -> ModelBuilder:
    panel = ModelBuilder()
    depth = radius + 0.035
    cloud_pair(
        panel,
        "cloud",
        (0, y + 0.02, depth),
        0.46,
        material="ridge",
        surface="polished_metal",
        depth=0.032,
    )
    panel.ribbon(
        "cloud_tail",
        "ridge",
        "jade",
        ((-0.20, y - 0.07), (0, y - 0.13), (0.20, y - 0.07)),
        0.028,
        0.028,
        (0, 0, depth),
    )
    return panel


def _square_relief(depth: float, y: float) -> ModelBuilder:
    panel = ModelBuilder()
    z = depth + 0.025
    for name, path in (
        ("top", ((-0.30, y + 0.17), (0.30, y + 0.17))),
        ("bottom", ((-0.30, y - 0.17), (0.30, y - 0.17))),
        ("left", ((-0.30, y - 0.17), (-0.30, y + 0.17))),
        ("right", ((0.30, y - 0.17), (0.30, y + 0.17))),
    ):
        panel.ribbon(name, "ridge", "polished_metal", path, 0.028, 0.025, (0, 0, z))
    diamond_rune(
        panel,
        "ward",
        (0, y, z + 0.015),
        0.075,
        material="rim",
        surface="jade",
    )
    return panel


def ding_vessel(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "ancient_round":
        mesh.open_lathe(
            "ancient_round", "metal", "aged_metal",
            ((0.27, -0.39), (0.43, -0.36), (0.59, -0.22), (0.64, -0.02),
             (0.60, 0.15), (0.53, 0.25), (0.62, 0.33)),
            0.09, 0.30, 18,
            rim_material="rim", rim_surface="polished_metal",
            inner_material="core", inner_surface="aged_metal",
        )
        mesh.torus("ancient_lower_band", "ridge", "polished_metal", 0.53, 0.026, 18, 4, (0, -0.27, 0))
        mesh.torus("ancient_shoulder_band", "ridge", "aged_metal", 0.605, 0.022, 18, 4, (0, 0.13, 0))
        mesh.torus("ancient_neck_band", "edge", "polished_metal", 0.545, 0.022, 18, 4, (0, 0.25, 0))
        mesh.torus("ancient_mouth_band", "rim", "polished_metal", 0.62, 0.038, 18, 4, (0, 0.33, 0))
        _repeat_body_relief(mesh, _taotie_relief(0.64, -0.03), "ancient_taotie")
    elif variant == "square_belly":
        mesh.beveled_box("square_belly", "metal", "aged_metal", (1.12, 0.62, 0.88), 0.13, (0, -0.06, 0))
        mesh.beveled_box("square_lower_band", "ridge", "polished_metal", (1.04, 0.075, 0.82), 0.07, (0, -0.30, 0))
        mesh.beveled_box("square_shoulder", "rim", "polished_metal", (1.22, 0.12, 0.98), 0.08, (0, 0.25, 0))
        mesh.beveled_box("square_neck", "metal", "aged_metal", (0.98, 0.11, 0.75), 0.09, (0, 0.31, 0))
        mesh.beveled_box("square_mouth", "core", "aged_metal", (0.90, 0.035, 0.68), 0.11, (0, 0.36, 0))
        mesh.beveled_box("square_mouth_ridge", "rim", "polished_metal", (1.08, 0.055, 0.83), 0.10, (0, 0.38, 0))
        for x in (-0.44, 0.44):
            mesh.ribbon(f"square_corner_{x}", "ridge", "polished_metal", ((x, -0.29), (x * 1.05, 0.20)), 0.040, 0.035, (0, 0, 0.45))
        _repeat_body_relief(mesh, _square_relief(0.45, -0.06), "square_panel")
    else:
        mesh.open_lathe(
            "jade_cauldron", "metal", "jade",
            ((0.26, -0.39), (0.43, -0.35), (0.60, -0.18), (0.63, 0.04),
             (0.58, 0.17), (0.52, 0.27), (0.60, 0.35)),
            0.085, 0.30, 20,
            rim_material="rim", rim_surface="polished_metal",
            inner_material="core", inner_surface="crystal",
        )
        mesh.torus("jade_lower_band", "ridge", "polished_metal", 0.52, 0.024, 18, 4, (0, -0.27, 0))
        mesh.torus("jade_shoulder_band", "ridge", "jade", 0.59, 0.020, 18, 4, (0, 0.16, 0))
        mesh.torus("jade_neck_band", "edge", "polished_metal", 0.535, 0.021, 18, 4, (0, 0.27, 0))
        mesh.torus("jade_mouth_band", "rim", "polished_metal", 0.60, 0.034, 18, 4, (0, 0.35, 0))
        _repeat_body_relief(mesh, _cloud_relief(0.63, -0.03), "cauldron_cloud")
        for index, angle in enumerate((45, 135, 225, 315)):
            gem = ModelBuilder()
            jewel(gem, "inlay", (0, -0.03, 0.665), 0.048, material="gem", surface="emissive")
            mesh.extend(gem, rotation=(0, angle, 0), prefix=f"cauldron_inlay_{index:02d}_")
    return mesh


def ding_lid(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "domed_lid":
        mesh.lathe("domed_lid", "rim", "aged_metal", ((0.59, -0.06), (0.54, 0.03), (0.39, 0.14), (0.20, 0.21), (0.08, 0.25)), 18)
        mesh.torus("dome_lip", "edge", "polished_metal", 0.57, 0.025, 18, 4, (0, -0.04, 0))
        mesh.lathe("dome_finial", "edge", "polished_metal", ((0.07, 0.23), (0.09, 0.31), (0.045, 0.38)), 10)
        jewel(mesh, "dome_gem", (0, 0.39, 0), 0.075, material="gem", surface="jade")
    elif variant == "pagoda_lid":
        mesh.lathe("pagoda_lower", "rim", "aged_metal", ((0.61, -0.06), (0.54, 0.03), (0.40, 0.10)), 16)
        mesh.torus("pagoda_lower_eave", "edge", "polished_metal", 0.49, 0.032, 16, 4, (0, 0.08, 0))
        mesh.lathe("pagoda_upper", "rim", "aged_metal", ((0.38, 0.10), (0.31, 0.17), (0.19, 0.23)), 14)
        mesh.torus("pagoda_upper_eave", "edge", "polished_metal", 0.31, 0.027, 14, 4, (0, 0.16, 0))
        mesh.lathe("pagoda_finial", "edge", "polished_metal", ((0.07, 0.22), (0.09, 0.30), (0.04, 0.39)), 9)
        jewel(mesh, "pagoda_gem", (0, 0.40, 0), 0.07, material="gem", surface="emissive")
    else:
        mesh.torus("open_lid_rim", "rim", "polished_metal", 0.57, 0.035, 18, 4, (0, 0, 0))
        for x, scale in ((-0.20, 0.20), (0, 0.30), (0.20, 0.20)):
            flame(mesh, f"open_flame_{x}", (x, 0.10 + scale * 0.35, 0), scale, material="fire", surface="emissive")
        diamond_rune(mesh, "open_core", (0, 0.22, 0.16), 0.08, material="core", surface="emissive")
    return mesh


def ding_ears(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "cloud_handles":
        for sign in (-1, 1):
            path = tuple((sign * x, y) for x, y in ((0.52, 0.16), (0.72, 0.34), (0.88, 0.28), (0.84, 0.04), (0.64, 0.02)))
            mesh.ribbon(f"cloud_handle_{sign}", "rim", "polished_metal", path, 0.095, 0.10, (0, 0, 0.02))
            cloud_pair(mesh, f"handle_cloud_{sign}", (sign * 0.77, 0.25, 0.09), 0.18, material="ridge", surface="polished_metal", depth=0.04)
            jewel(mesh, f"handle_gem_{sign}", (sign * 0.78, 0.24, 0.13), 0.045, material="gem", surface="jade")
    elif variant == "dragon_ears":
        for x, sign in ((-0.78, -1), (0.78, 1)):
            outline = ((-sign * 0.12, -0.08), (sign * 0.05, 0.08), (sign * 0.18, 0.38), (0, 0.34), (-sign * 0.10, 0.08))
            mesh.extrude(f"dragon_ear_{x}", "rim", "aged_metal", outline, 0.12, (x, 0, 0))
            mesh.ribbon(f"dragon_ear_edge_{x}", "edge", "polished_metal", ((x - sign * 0.05, 0.02), (x + sign * 0.11, 0.29)), 0.035, 0.035, (0, 0, 0.09))
            jewel(mesh, f"dragon_eye_{x}", (x + sign * 0.09, 0.28, 0.11), 0.038, material="gem", surface="emissive")
    else:
        for x in (-0.74, 0.74):
            mesh.ring(f"ring_ear_{x}", "rim", "polished_metal", (0.20, 0.25), (0.12, 0.16), 0.11, 14, (x, 0.18, 0))
            mesh.ribbon(f"ring_mount_{x}", "ridge", "aged_metal", ((x * 0.75, 0.12), (x, 0.18)), 0.08, 0.08, (0, 0, 0.02))
            jewel(mesh, f"ring_ear_gem_{x}", (x, 0.18, 0.11), 0.042, material="gem", surface="emissive")
    return mesh


def ding_legs(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "three_beast":
        leg = ModelBuilder()
        leg.ribbon("beast_shin", "main", "aged_metal", ((0, 0.04), (0.01, -0.24), (0.08, -0.48), (0.17, -0.60)), 0.13, 0.14)
        leg.extrude("beast_claw", "edge", "polished_metal", ((0.08, -0.55), (0.27, -0.64), (0.19, -0.71), (0.02, -0.66)), 0.13)
        jewel(leg, "beast_knee", (0.03, -0.28, 0.09), 0.045, material="gem", surface="jade")
        mesh.radial_repeat(leg, 3, 0.36, start_angle=30, prefix="beast_leg_")
    elif variant == "four_square":
        leg = ModelBuilder()
        leg.beveled_box("square_leg", "main", "stone", (0.14, 0.56, 0.14), 0.035, (0.04, -0.26, 0))
        leg.beveled_box("square_foot", "edge", "polished_metal", (0.25, 0.10, 0.20), 0.035, (0.10, -0.57, 0))
        leg.ribbon("square_inlay", "ridge", "polished_metal", ((0.04, -0.08), (0.07, -0.48)), 0.026, 0.025, (0, 0, 0.08))
        mesh.radial_repeat(leg, 4, 0.36, start_angle=45, prefix="square_leg_")
    else:
        leg = ModelBuilder()
        leg.ribbon("lotus_stem", "main", "jade", ((0, 0.04), (-0.01, -0.26), (0.08, -0.50), (0.16, -0.59)), 0.12, 0.13)
        lotus(leg, "lotus_foot", (0.16, -0.62, 0.02), 0.11, petals=5, material="edge", surface="polished_metal")
        jewel(leg, "lotus_knee", (0.03, -0.30, 0.08), 0.04, material="gem", surface="emissive")
        mesh.radial_repeat(leg, 3, 0.35, start_angle=30, prefix="lotus_leg_")
    return mesh


def ding_core(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "azure_flame":
        flame(mesh, "azure_flame", (0, 0.10, 0), 0.34, material="main", surface="emissive")
        diamond_rune(mesh, "azure_core", (0, 0.08, 0.18), 0.10, material="core", surface="emissive")
    elif variant == "golden_elixir":
        jewel(mesh, "golden_elixir", (0, 0.14, 0), 0.20, material="core", surface="emissive")
        for x in (-0.16, 0.16):
            flame(mesh, f"elixir_flame_{x}", (x, 0.08, 0), 0.22, material="fire", surface="emissive")
        mesh.torus("elixir_orbit", "main", "polished_metal", 0.24, 0.025, 10, 4, (0, 0.14, 0))
    else:
        mesh.torus("void_vortex_outer", "fire", "emissive", 0.22, 0.045, 12, 4, (0, 0.14, 0))
        mesh.torus("void_vortex_inner", "core", "crystal", 0.12, 0.032, 10, 4, (0, 0.14, 0.06))
        jewel(mesh, "void_eye", (0, 0.14, 0.15), 0.07, material="gem", surface="emissive")
    return mesh


def ding_legacy(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    metal_surface = "jade" if variant == "blue_fire" else "aged_metal"
    mesh.open_lathe(
        "legacy_vessel", "metal", metal_surface,
        ((0.27, -0.40), (0.44, -0.37), (0.61, -0.21), (0.66, 0.01),
         (0.61, 0.17), (0.54, 0.28), (0.63, 0.36)),
        0.09, 0.31, 18,
        rim_material="rim", rim_surface="polished_metal",
        inner_material="core", inner_surface="crystal" if variant == "blue_fire" else "aged_metal",
    )
    mesh.torus("legacy_lower_band", "ridge", "polished_metal", 0.54, 0.026, 18, 4, (0, -0.28, 0))
    mesh.torus("legacy_shoulder_band", "ridge", metal_surface, 0.615, 0.022, 18, 4, (0, 0.16, 0))
    mesh.torus("legacy_neck_band", "edge", "polished_metal", 0.55, 0.022, 18, 4, (0, 0.28, 0))
    mesh.torus("legacy_mouth_band", "rim", "polished_metal", 0.63, 0.037, 18, 4, (0, 0.36, 0))
    relief = _cloud_relief(0.66, -0.04) if variant == "blue_fire" else _taotie_relief(0.66, -0.04)
    _repeat_body_relief(mesh, relief, "legacy_relief")
    for sign in (-1, 1):
        path = tuple((sign * x, y) for x, y in ((0.55, 0.13), (0.75, 0.34), (0.88, 0.22), (0.79, 0.00), (0.62, 0.04)))
        mesh.ribbon(f"legacy_ear_{sign}", "rim", "polished_metal", path, 0.10, 0.11, (0, 0, 0.02))
    leg = ModelBuilder()
    leg.ribbon("legacy_leg", "leg", "aged_metal", ((0, 0.02), (0, -0.30), (0.09, -0.56), (0.18, -0.67)), 0.13, 0.15)
    leg.extrude("legacy_claw", "edge", "polished_metal", ((0.08, -0.61), (0.28, -0.69), (0.20, -0.76), (0.02, -0.70)), 0.14)
    mesh.radial_repeat(leg, 3, 0.37, start_angle=30, prefix="legacy_leg_")
    flame(mesh, "legacy_flame", (0, 0.43, 0), 0.26, material="fire", surface="emissive")
    diamond_rune(mesh, "legacy_core", (0, 0.40, 0.18), 0.085, material="core", surface="emissive")
    return mesh


def mirror_surface(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "full_moon":
        mesh.faceted_disc("full_moon", "glass", "crystal", 0.56, 0.55, 0.08, 0.045, 20)
        mesh.ribbon("moon_glint_broad", "glint", "crystal", ((-0.38, -0.16), (0.13, 0.36)), 0.11, 0.025, (0, 0, 0.105))
        mesh.ribbon("moon_glint_fine", "glint", "emissive", ((-0.19, -0.34), (0.33, 0.18)), 0.035, 0.018, (0, 0, 0.115))
    elif variant == "sixfold_jade":
        outline = ((0, 0.54), (0.47, 0.27), (0.47, -0.27), (0, -0.54), (-0.47, -0.27), (-0.47, 0.27))
        mesh.extrude("sixfold_surface", "glass", "jade", outline, 0.08)
        inner = tuple((x * 0.83, y * 0.83) for x, y in outline)
        mesh.extrude("sixfold_inner", "water", "crystal", inner, 0.035, (0, 0, 0.07))
        for index in range(6):
            angle = math.tau * index / 6
            jewel(mesh, f"sixfold_glint_{index}", (math.cos(angle) * 0.41, math.sin(angle) * 0.41, 0.075), 0.032, material="glint", surface="emissive")
        mesh.ribbon("sixfold_glint", "glint", "emissive", ((-0.29, -0.18), (0.17, 0.28)), 0.045, 0.018, (0, 0, 0.105))
    else:
        outline = ((0, 0.54), (0.34, 0.20), (0.44, -0.10), (0.30, -0.40), (0, -0.54), (-0.30, -0.40), (-0.44, -0.10), (-0.34, 0.20))
        mesh.extrude("water_drop_surface", "glass", "crystal", outline, 0.08)
        inner = tuple((x * 0.84, y * 0.84) for x, y in outline)
        mesh.extrude("water_drop_inner", "water", "jade", inner, 0.03, (0, 0, 0.07))
        mesh.ribbon("water_glint", "glint", "emissive", ((-0.23, 0.28), (0.06, 0.18), (-0.05, -0.11), (0.18, -0.29)), 0.045, 0.018, (0, 0, 0.105))
    return mesh


def mirror_frame(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "sun_ring":
        mesh.ring("sun_frame", "metal", "polished_metal", (0.67, 0.67), (0.58, 0.58), 0.13, 20)
        ray = ModelBuilder()
        ray.extrude("sun_ray", "edge", "polished_metal", ((-0.035, 0.64), (0, 0.75), (0.035, 0.64), (0, 0.60)), 0.08)
        for index in range(12):
            mesh.extend(ray, rotation=(0, 0, index * 30), prefix=f"sun_ray_{index:02d}_")
    elif variant == "lotus_frame":
        mesh.ring("lotus_frame_ring", "metal", "jade", (0.67, 0.67), (0.58, 0.58), 0.13, 20)
        petal = ModelBuilder()
        petal.extrude("lotus_frame_petal", "edge", "polished_metal", ((-0.065, 0.62), (0, 0.76), (0.065, 0.62), (0, 0.57)), 0.08)
        for index in range(8):
            mesh.extend(petal, rotation=(0, 0, index * 45), prefix=f"frame_petal_{index:02d}_")
    else:
        mesh.ring("cloud_frame_ring", "metal", "aged_metal", (0.67, 0.67), (0.58, 0.58), 0.13, 18)
        for y in (-0.36, 0.36):
            cloud_pair(mesh, f"frame_cloud_{y}", (0, y, 0.09), 0.36, material="edge", surface="polished_metal", depth=0.045)
    return mesh


def mirror_back(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    mesh.disc("mirror_back_plate", "handle", "aged_metal", 0.56, 0.56, 0.08, 18, (0, 0, -0.10))
    if variant == "trigram_back":
        trigram(mesh, "back_trigram", (0, 0, -0.035), 0.30, material="ridge", surface="polished_metal")
        mesh.ring("back_ridge", "ridge", "polished_metal", (0.47, 0.47), (0.435, 0.435), 0.035, 18, (0, 0, -0.03))
    elif variant == "star_lattice":
        for rotation in (0, 60, 120):
            line = ModelBuilder()
            line.ribbon("star_line", "ridge", "polished_metal", ((-0.42, 0), (0.42, 0)), 0.032, 0.03, (0, 0, -0.02))
            mesh.extend(line, rotation=(0, 0, rotation), prefix=f"lattice_{rotation}_")
        star_rune(mesh, "back_star", (0, 0, 0), 0.14, points=6, material="main", surface="emissive")
    else:
        mesh.ribbon("coiling_dragon", "ridge", "polished_metal", ((-0.34, -0.15), (-0.18, 0.25), (0.15, 0.31), (0.34, 0.06), (0.12, -0.27), (-0.17, -0.29)), 0.055, 0.04, (0, 0, -0.02))
        jewel(mesh, "dragon_eye", (0.16, 0.25, 0.02), 0.038, material="gem", surface="emissive")
    return mesh


def mirror_handle(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "straight_jade":
        mesh.lathe("straight_handle", "handle", "jade", ((0.065, -0.88), (0.10, -0.78), (0.085, -0.15), (0.12, -0.04), (0.10, 0)), 12)
        for y in (-0.72, -0.43, -0.14):
            mesh.torus(f"handle_edge_{y}", "edge", "polished_metal", 0.09, 0.016, 12, 4, (0, y, 0))
        mesh.lathe("handle_pommel", "edge", "polished_metal", ((0.06, -0.93), (0.12, -0.88), (0.05, -0.82)), 10)
        jewel(mesh, "handle_gem", (0, -0.88, 0.08), 0.052, material="gem", surface="emissive")
    elif variant == "cloud_loop":
        mesh.ribbon("cloud_handle_stem", "handle", "jade", ((0, 0), (-0.02, -0.51)), 0.13, 0.11)
        mesh.ring("cloud_handle_loop", "handle", "polished_metal", (0.16, 0.20), (0.085, 0.11), 0.10, 12, (0, -0.68, 0))
        cloud_pair(mesh, "handle_cloud", (0, -0.45, 0.08), 0.22, material="edge", surface="polished_metal", depth=0.04)
        jewel(mesh, "loop_gem", (0, -0.68, 0.09), 0.045, material="gem", surface="emissive")
    else:
        mesh.extrude("scepter_handle", "handle", "aged_metal", ((-0.075, 0), (-0.09, -0.62), (-0.05, -0.88), (0.05, -0.88), (0.09, -0.62), (0.075, 0)), 0.12)
        mesh.extrude("scepter_head", "edge", "polished_metal", ((-0.16, -0.61), (0, -0.47), (0.16, -0.61), (0, -0.76)), 0.10)
        jewel(mesh, "scepter_gem", (0, -0.61, 0.09), 0.058, material="gem", surface="emissive")
    return mesh


def mirror_crown(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "moon_pearl":
        mesh.ribbon("moon_cradle", "metal", "polished_metal", ((-0.18, 0), (-0.12, 0.13), (0, 0.18), (0.12, 0.13), (0.18, 0)), 0.045, 0.07)
        jewel(mesh, "moon_pearl", (0, 0.18, 0.07), 0.085, material="gem", surface="emissive")
    elif variant == "cloud_crown":
        cloud_pair(mesh, "mirror_cloud_crown", (0, 0.09, 0), 0.32, material="metal", surface="polished_metal", depth=0.08)
        jewel(mesh, "cloud_crown_gem", (0, 0.16, 0.07), 0.065, material="gem", surface="jade")
    else:
        mesh.extrude("phoenix_wings", "metal", "polished_metal", ((-0.20, 0), (-0.16, 0.20), (-0.06, 0.13), (0, 0.27), (0.06, 0.13), (0.16, 0.20), (0.20, 0), (0, 0.07)), 0.09)
        for x in (-0.13, 0.13):
            mesh.ribbon(f"phoenix_feather_{x}", "ridge", "aged_metal", ((0, 0.03), (x, 0.19)), 0.026, 0.025, (0, 0, 0.07))
        jewel(mesh, "phoenix_gem", (0, 0.15, 0.08), 0.055, material="gem", surface="emissive")
    return mesh


def mirror_legacy(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "bronze_round":
        mesh.disc("bronze_back", "metal", "aged_metal", 0.64, 0.64, 0.12, 18)
        mesh.ring("bronze_frame", "metal", "polished_metal", (0.68, 0.68), (0.58, 0.58), 0.15, 20)
        mesh.faceted_disc("bronze_glass", "glass", "crystal", 0.57, 0.57, 0.055, 0.04, 20, (0, 0, 0.105))
    else:
        outline = ((0, 0.67), (0.58, 0.34), (0.58, -0.34), (0, -0.67), (-0.58, -0.34), (-0.58, 0.34))
        mesh.extrude("jade_hex_back", "metal", "jade", outline, 0.13)
        inner = tuple((x * 0.82, y * 0.82) for x, y in outline)
        mesh.extrude("jade_hex_glass", "glass", "crystal", inner, 0.05, (0, 0, 0.10))
        for index, (x, y) in enumerate(outline):
            jewel(mesh, f"hex_edge_{index}", (x * 0.92, y * 0.92, 0.10), 0.038, material="edge", surface="emissive")
    jewel(mesh, "legacy_mirror_gem", (0, 0.50, 0.13), 0.06, material="gem", surface="jade")
    mesh.ribbon("legacy_glint_broad", "glint", "crystal", ((-0.36, -0.16), (0.14, 0.34)), 0.09, 0.02, (0, 0, 0.15))
    star_rune(mesh, "legacy_mirror_glint", (-0.20, 0.20, 0.17), 0.08, points=4, material="glint")
    mesh.lathe("legacy_handle", "handle", "jade" if variant == "jade_hex" else "wood", ((0.06, -1.20), (0.10, -1.10), (0.09, -0.72), (0.11, -0.63)), 12)
    mesh.torus("legacy_handle_ring", "edge", "polished_metal", 0.09, 0.015, 10, 4, (0, -0.78, 0))
    return mesh


_FACTORIES = {
    "ding_vessel3d": ding_vessel,
    "ding_lid3d": ding_lid,
    "ding_ears3d": ding_ears,
    "ding_legs3d": ding_legs,
    "ding_core3d": ding_core,
    "ding3d": ding_legacy,
    "mirror_surface3d": mirror_surface,
    "mirror_frame3d": mirror_frame,
    "mirror_back3d": mirror_back,
    "mirror_handle3d": mirror_handle,
    "mirror_crown3d": mirror_crown,
    "mirror3d": mirror_legacy,
}
