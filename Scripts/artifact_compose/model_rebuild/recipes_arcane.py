#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""宝珠与塔两类法器的模块模型。"""

from __future__ import annotations

import math

from .builder import ModelBuilder
from .motifs import bead_chain, cloud_pair, diamond_rune, jewel, lotus, small_bell, star_rune


def build_arcane(module: str, variant: str) -> ModelBuilder | None:
    factory = _FACTORIES.get(module)
    return factory(variant) if factory else None


def pearl_core(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "moonwater_pearl":
        mesh.ellipsoid("moonwater_core", "water", "crystal", (0.42, 0.42, 0.42), 12, 6)
        mesh.ribbon("moonwater_wave", "glint", "emissive", ((-0.28, 0.06), (-0.10, 0.18), (0.12, 0.04), (0.28, 0.16)), 0.055, 0.045, (0, 0, 0.40))
        star_rune(mesh, "moonwater_light", (-0.12, 0.18, 0.40), 0.09, points=4, material="core", surface="emissive")
    elif variant == "thunder_vein_pearl":
        mesh.ellipsoid("thunder_core", "right", "crystal", (0.42, 0.42, 0.42), 12, 6)
        for index, path in enumerate((((-0.20, 0.30), (0.02, 0.08), (-0.08, -0.10), (0.18, -0.32)), ((0.18, 0.26), (0.06, 0.04), (0.24, -0.12)))):
            mesh.ribbon(f"thunder_vein_{index}", "glint", "emissive", path, 0.05, 0.045, (0, 0, 0.40))
        jewel(mesh, "thunder_seed", (0, 0, 0.42), 0.10, material="core", surface="emissive")
    else:
        outline = ((0, 0.42), (0.34, 0.18), (0.38, -0.22), (0, -0.42), (-0.38, -0.22), (-0.34, 0.18))
        mesh.extrude("five_element_prism", "gem", "crystal", outline, 0.54)
        for index in range(5):
            angle = math.tau * index / 5
            jewel(mesh, f"element_{index}", (math.cos(angle) * 0.25, math.sin(angle) * 0.25, 0.32), 0.065, material="main", surface="jade")
        star_rune(mesh, "prism_glint", (0, 0, 0.34), 0.10, points=5, material="glint", surface="emissive")
    return mesh


def pearl_shell(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "dragon_armillary_shell":
        for index, rotation in enumerate(((0, 0, 0), (58, 0, 28), (-58, 0, -28))):
            ring = ModelBuilder()
            ring.ring("armillary_ring", "metal", "polished_metal", (0.54, 0.54), (0.48, 0.48), 0.08, 14)
            mesh.extend(ring, rotation=rotation, prefix=f"armillary_{index}_")
        mesh.ribbon("dragon_spine", "ridge", "aged_metal", ((-0.42, -0.18), (-0.12, 0.38), (0.36, 0.14)), 0.07, 0.06, (0, 0, 0.45))
        jewel(mesh, "armillary_gem", (0, 0.50, 0.05), 0.07, material="gem", surface="emissive")
    elif variant == "lotus_petal_shell":
        petal = ModelBuilder()
        petal.extrude("lotus_shell_petal", "main", "jade", ((-0.15, -0.10), (0, 0.58), (0.15, -0.10), (0, -0.40)), 0.10)
        for index in range(8):
            mesh.extend(petal, rotation=(0, index * 45, 0), prefix=f"shell_petal_{index}_")
        mesh.torus("lotus_shell_rim", "rim", "polished_metal", 0.46, 0.035, 14, 4)
    else:
        for index, rotation in enumerate(((0, 0, 0), (45, 0, 45), (-45, 0, -45))):
            ring = ModelBuilder()
            ring.ring("void_lattice", "right", "aged_metal", (0.54, 0.54), (0.49, 0.49), 0.065, 12)
            mesh.extend(ring, rotation=rotation, prefix=f"void_lattice_{index}_")
        for index in range(4):
            angle = math.tau * index / 4
            diamond_rune(mesh, f"void_node_{index}", (math.cos(angle) * 0.50, math.sin(angle) * 0.50, 0.08), 0.07, material="core", surface="emissive")
        mesh.torus("void_shell_core", "main", "crystal", 0.34, 0.025, 12, 4)
    return mesh


def pearl_halo(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "solar_glyph_halo":
        mesh.ring("solar_halo", "rim", "polished_metal", (0.72, 0.72), (0.62, 0.62), 0.10, 16)
        ray = ModelBuilder()
        ray.extrude("solar_ray", "main", "polished_metal", ((-0.04, 0.64), (0, 0.76), (0.04, 0.64), (0, 0.58)), 0.07)
        for index in range(12):
            mesh.extend(ray, rotation=(0, 0, index * 30), prefix=f"solar_ray_{index}_")
        star_rune(mesh, "solar_glint", (0, 0, 0.10), 0.16, points=8, material="glint", surface="emissive")
    elif variant == "water_ripple_halo":
        for index, radius in enumerate((0.42, 0.58, 0.74)):
            mesh.ring(f"water_ripple_{index}", "water", "emissive", (radius, radius * 0.88), (radius - 0.035, radius * 0.88 - 0.035), 0.055, 16, (0, 0, index * 0.03))
        for x in (-0.46, 0.46):
            jewel(mesh, f"ripple_node_{x}", (x, 0, 0.10), 0.055, material="main", surface="crystal")
    else:
        mesh.ring("star_script_halo", "right", "aged_metal", (0.72, 0.72), (0.64, 0.64), 0.09, 14)
        for index in range(7):
            angle = math.tau * index / 7
            star_rune(mesh, f"script_star_{index}", (math.cos(angle) * 0.68, math.sin(angle) * 0.68, 0.10), 0.075, points=4, material="glint", surface="emissive")
        mesh.ring("script_inner", "main", "crystal", (0.50, 0.50), (0.47, 0.47), 0.06, 14, (0, 0, 0.03))
    return mesh


def pearl_companions(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "three_talent_companions":
        points = ((0, 0.72, 0.08), (-0.62, -0.36, 0.08), (0.62, -0.36, 0.08))
        bead_chain(mesh, "three_talent", points, 0.12, material="main", surface="jade")
        for index, point in enumerate(points):
            star_rune(mesh, f"talent_glint_{index}", (point[0], point[1], point[2] + 0.10), 0.055, points=4, material="glint", surface="emissive")
    elif variant == "seven_star_companions":
        points = tuple((math.cos(math.tau * index / 7) * 0.76, math.sin(math.tau * index / 7) * 0.76, 0.06) for index in range(7))
        bead_chain(mesh, "seven_star", points, 0.085, material="main", surface="crystal")
    else:
        jewel(mesh, "yang_pearl", (-0.38, 0, 0.06), 0.20, material="top", surface="polished_metal")
        jewel(mesh, "yin_pearl", (0.38, 0, 0.06), 0.20, material="right", surface="aged_metal")
        for index, x in enumerate((-0.38, 0.38)):
            star_rune(mesh, f"yin_yang_glint_{index}", (x, 0, 0.22), 0.07, points=4, material="glint", surface="emissive")
        mesh.ring("yin_yang_orbit", "rim", "polished_metal", (0.72, 0.52), (0.67, 0.47), 0.06, 14, (0, 0, 0.02))
    return mesh


def tower_base(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "lotus_square_base":
        mesh.frustum("lotus_square_lower", "metal", "aged_metal", (0.60, 0.54), (0.53, 0.47), 0.18, 4, (0, -0.10, 0), start_angle=45)
        mesh.frustum("lotus_square_upper", "rim", "polished_metal", (0.53, 0.47), (0.43, 0.39), 0.12, 4, (0, 0.05, 0), start_angle=45)
        lotus(mesh, "tower_lotus", (0, 0.15, 0.42), 0.23, petals=8, material="main", surface="polished_metal")
        jewel(mesh, "lotus_base_core", (0, 0.04, 0.50), 0.055, material="gem", surface="emissive")
    elif variant == "octagonal_stone_base":
        mesh.frustum("octagonal_stone_lower", "right", "stone", (0.62, 0.54), (0.54, 0.47), 0.20, 8, (0, -0.09, 0), start_angle=22.5)
        mesh.frustum("octagonal_stone_upper", "ridge", "polished_metal", (0.53, 0.46), (0.43, 0.38), 0.10, 8, (0, 0.06, 0), start_angle=22.5)
        mesh.torus("stone_base_rim", "rim", "polished_metal", 0.44, 0.025, 16, 4, (0, 0.12, 0))
        diamond_rune(mesh, "base_core", (0, 0, 0.48), 0.075, material="core", surface="emissive")
    else:
        mesh.frustum("floating_cloud_plinth", "top", "jade", (0.58, 0.48), (0.43, 0.36), 0.17, 8, (0, -0.07, 0), start_angle=22.5)
        for sign in (-1, 1):
            cloud_pair(mesh, f"base_cloud_{sign}", (sign * 0.18, 0.08, 0.40), 0.34, material="fold", surface="crystal", depth=0.045)
        jewel(mesh, "base_cloud_gem", (0, 0.08, 0.48), 0.06, material="gem", surface="emissive")
    return mesh


def tower_level(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "golden_scripture_chamber":
        mesh.frustum("scripture_chamber", "metal", "polished_metal", (0.38, 0.35), (0.34, 0.31), 0.54, 4, start_angle=45)
        mesh.frustum("scripture_sill", "ridge", "aged_metal", (0.40, 0.37), (0.38, 0.35), 0.07, 4, (0, -0.255, 0), start_angle=45)
        for x in (-0.26, 0.26):
            mesh.beveled_box(f"chamber_window_{x}", "right", "aged_metal", (0.13, 0.24, 0.045), 0.03, (x, 0, 0.34))
            diamond_rune(mesh, f"window_glint_{x}", (x, 0, 0.38), 0.042, material="glint", surface="emissive")
        mesh.ribbon("chamber_edge", "edge", "polished_metal", ((-0.28, 0.20), (0.28, 0.20)), 0.032, 0.03, (0, 0, 0.38))
    elif variant == "jade_octagonal_level":
        mesh.frustum("jade_octagonal", "top", "jade", (0.41, 0.38), (0.35, 0.32), 0.56, 8, start_angle=22.5)
        mesh.frustum("jade_sill", "rim", "polished_metal", (0.42, 0.39), (0.40, 0.37), 0.065, 8, (0, -0.27, 0), start_angle=22.5)
        mesh.ring("octagonal_window", "ridge", "polished_metal", (0.20, 0.18), (0.15, 0.13), 0.045, 12, (0, 0, 0.35))
        jewel(mesh, "level_gem", (0, 0, 0.39), 0.055, material="gem", surface="emissive")
    else:
        mesh.frustum("crystal_prison", "glass", "crystal", (0.38, 0.36), (0.35, 0.33), 0.56, 8, start_angle=22.5)
        mesh.frustum("prison_sill", "ridge", "polished_metal", (0.41, 0.39), (0.38, 0.36), 0.065, 8, (0, -0.27, 0), start_angle=22.5)
        for x in (-0.26, 0.26):
            mesh.ribbon(f"prison_bar_{x}", "main", "polished_metal", ((x, -0.21), (x, 0.21)), 0.032, 0.03, (0, 0, 0.36))
        diamond_rune(mesh, "prison_core", (0, 0, 0.39), 0.085, material="core", surface="emissive")
    return mesh


def tower_eaves(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "broad_square_eaves":
        mesh.frustum("broad_eaves", "metal", "aged_metal", (0.64, 0.60), (0.34, 0.32), 0.19, 4, start_angle=45)
        mesh.frustum("broad_eave_cap", "ridge", "polished_metal", (0.37, 0.35), (0.32, 0.30), 0.055, 4, (0, 0.105, 0), start_angle=45)
        for x in (-0.48, 0.48):
            jewel(mesh, f"eave_gem_{x}", (x, -0.07, 0.42), 0.042, material="gem", surface="emissive")
        mesh.ribbon("broad_eave_edge", "edge", "polished_metal", ((-0.48, -0.07), (0.48, -0.07)), 0.035, 0.03, (0, 0, 0.47))
    elif variant == "octagonal_lotus_eaves":
        mesh.frustum("octagonal_eaves", "top", "jade", (0.62, 0.58), (0.34, 0.32), 0.19, 8, start_angle=22.5)
        mesh.frustum("octagonal_eave_cap", "ridge", "polished_metal", (0.37, 0.35), (0.32, 0.30), 0.055, 8, (0, 0.105, 0), start_angle=22.5)
        lotus(mesh, "eave_lotus", (0, -0.01, 0.47), 0.19, petals=8, material="main", surface="polished_metal")
        mesh.torus("eave_rim", "rim", "polished_metal", 0.48, 0.025, 16, 4, (0, -0.065, 0))
    else:
        mesh.frustum("bell_eaves", "right", "stone", (0.62, 0.58), (0.33, 0.31), 0.19, 8, start_angle=22.5)
        for index, (x, z) in enumerate(((-0.50, 0.46), (0.50, 0.46), (-0.50, -0.46), (0.50, -0.46))):
            small_bell(mesh, f"eave_bell_{index}", (x, -0.12, z), 0.14, material="main", surface="polished_metal")
        star_rune(mesh, "eave_glint", (0, -0.02, 0.48), 0.06, points=4, material="glint", surface="emissive")
    return mesh


def tower_finial(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "lotus_spire":
        lotus(mesh, "spire_lotus", (0, -0.02, 0), 0.24, petals=8, material="main", surface="polished_metal")
        mesh.extrude("lotus_spire", "metal", "aged_metal", ((-0.08, -0.04), (0, 0.42), (0.08, -0.04), (0, 0.04)), 0.16)
        jewel(mesh, "spire_gem", (0, 0.18, 0.12), 0.065, material="gem", surface="emissive")
    elif variant == "heaven_sword_spire":
        mesh.extrude("heaven_sword", "rim", "polished_metal", ((-0.10, -0.18), (-0.13, 0.06), (0, 0.42), (0.13, 0.06), (0.10, -0.18)), 0.16)
        mesh.ribbon("sword_spire_edge", "edge", "crystal", ((0, -0.10), (0, 0.34)), 0.04, 0.04, (0, 0, 0.12))
        diamond_rune(mesh, "sword_spire_core", (0, 0.04, 0.15), 0.07, material="core", surface="emissive")
    else:
        mesh.lathe("seven_pearl_spine", "ridge", "polished_metal", ((0.05, -0.18), (0.045, 0.38)), 8)
        points = tuple((0, -0.10 + index * 0.08, 0) for index in range(7))
        bead_chain(mesh, "seven_pearl", points, 0.07, material="gem", surface="jade")
        star_rune(mesh, "pearl_spire_glint", (0, 0.38, 0.10), 0.08, points=7, material="glint", surface="emissive")
        diamond_rune(mesh, "pearl_spire_core", (0, 0.10, 0.12), 0.06, material="core", surface="emissive")
    return mesh


_FACTORIES = {
    "pearl_core3d": pearl_core,
    "pearl_shell3d": pearl_shell,
    "pearl_halo3d": pearl_halo,
    "pearl_companions3d": pearl_companions,
    "tower_base3d": tower_base,
    "tower_level3d": tower_level,
    "tower_eaves3d": tower_eaves,
    "tower_finial3d": tower_finial,
}
