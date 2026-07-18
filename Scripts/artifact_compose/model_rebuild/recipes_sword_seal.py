#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""剑与印两类法器的模块模型。"""

from __future__ import annotations

from .builder import ModelBuilder, crescent_outline
from .motifs import cloud_pair, diamond_rune, jewel, lotus, small_bell, star_rune, tassel, trigram


def build_sword_seal(module: str, variant: str) -> ModelBuilder | None:
    factory = _FACTORIES.get(module)
    return factory(variant) if factory else None


def _faceted_blade(
    mesh: ModelBuilder,
    name: str,
    material: str,
    surface: str,
    profile: tuple[tuple[float, float], ...],
    depth: float,
) -> None:
    """按高度-半宽曲线构建菱形截面的剑身。"""
    sections = [
        (
            (-width, y, 0),
            (0, y, depth * 0.5),
            (width, y, 0),
            (0, y, -depth * 0.5),
        )
        for y, width in profile
    ]
    for index in range(len(sections) - 1):
        current = sections[index]
        following = sections[index + 1]
        mesh.face(f"{name}_left", material, surface, (current[1], current[0], following[0], following[1]))
        mesh.face(f"{name}_right", material, surface, (current[2], current[1], following[1], following[2]))
        mesh.face(f"{name}_back_right", material, surface, (current[3], current[2], following[2], following[3]))
        mesh.face(f"{name}_back_left", material, surface, (current[0], current[3], following[3], following[0]))
    mesh.face(f"{name}_base", material, surface, tuple(reversed(sections[0])))


def sword_blade(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "crystal":
        _faceted_blade(
            mesh, "crystal_blade", "metal", "crystal",
            ((0, 0.10), (0.24, 0.19), (2.16, 0.18), (2.46, 0.12), (2.70, 0.006)),
            0.18,
        )
        mesh.ribbon("crystal_ridge", "edge", "polished_metal", ((0, 0.16), (0, 2.42)), 0.030, 0.025, (0, 0, 0.105))
        diamond_rune(mesh, "crystal_vein", (0, 1.34, 0.13), 0.085, material="edge", surface="emissive")
    elif variant == "jade":
        _faceted_blade(
            mesh, "jade_blade", "metal", "jade",
            ((0, 0.11), (0.30, 0.21), (2.08, 0.19), (2.28, 0.24), (2.48, 0.11), (2.70, 0.006)),
            0.19,
        )
        mesh.ribbon("jade_ridge", "edge", "polished_metal", ((0, 0.18), (0, 2.45)), 0.034, 0.026, (0, 0, 0.11))
        for y in (0.78, 1.48):
            diamond_rune(mesh, f"jade_script_{y}", (0, y, 0.14), 0.060, material="edge", surface="emissive")
    else:
        _faceted_blade(
            mesh, "thorn_blade", "metal", "aged_metal",
            ((0, 0.085), (0.24, 0.16), (1.84, 0.15), (2.04, 0.27), (2.18, 0.14), (2.48, 0.09), (2.70, 0.006)),
            0.17,
        )
        mesh.ribbon("thorn_ridge", "edge", "polished_metal", ((0, 0.14), (0, 2.48)), 0.028, 0.024, (0, 0, 0.10))
        mesh.ribbon("blood_channel", "edge", "emissive", ((0.035, 0.46), (0.035, 2.05)), 0.026, 0.022, (0, 0, 0.125))
    return mesh


def sword_guard(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "bar":
        mesh.extrude("ritual_bar", "metal", "polished_metal", ((-0.55, -0.06), (-0.43, 0.08), (-0.12, 0.05), (0, 0.11), (0.12, 0.05), (0.43, 0.08), (0.55, -0.06), (0.22, -0.10), (-0.22, -0.10)), 0.20)
        jewel(mesh, "bar_gem", (0, 0, 0.14), 0.11, material="gem", surface="jade")
        for x in (-0.40, 0.40):
            diamond_rune(mesh, f"bar_edge_{x}", (x, 0, 0.12), 0.045, material="edge", surface="emissive")
    elif variant == "crescent":
        mesh.extrude("left_crescent", "metal", "polished_metal", crescent_outline(0.36, 0.25, samples=7), 0.18, (-0.22, -0.02, 0))
        right = ModelBuilder()
        right.extrude("right_crescent", "metal", "polished_metal", crescent_outline(0.36, 0.25, samples=7), 0.18)
        mesh.extend(right, rotation=(0, 0, 180), offset=(0.22, -0.02, 0))
        jewel(mesh, "crescent_guard_gem", (0, 0, 0.14), 0.12, material="gem", surface="emissive")
        mesh.ring("crescent_edge", "edge", "polished_metal", (0.18, 0.13), (0.11, 0.07), 0.08, 10, (0, 0, 0.16))
    else:
        mesh.extrude("wing_guard", "metal", "aged_metal", ((-0.55, -0.08), (-0.40, 0.02), (-0.28, 0.16), (-0.08, 0.04), (0, 0.11), (0.08, 0.04), (0.28, 0.16), (0.40, 0.02), (0.55, -0.08), (0.24, -0.06), (0, -0.02), (-0.24, -0.06)), 0.19)
        for x in (-0.34, 0.34):
            mesh.ribbon(f"wing_edge_{x}", "edge", "polished_metal", ((0, -0.02), (x * 0.62, 0.05), (x, 0.12)), 0.035, 0.05, (0, 0, 0.12))
        jewel(mesh, "wing_gem", (0, 0.01, 0.14), 0.10, material="gem", surface="jade")
    return mesh


def sword_grip_legacy(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "wrapped":
        mesh.box("wrapped_grip", "grip", "wood", (0.22, 0.78, 0.20), (0, -0.40, 0))
        for index in range(5):
            y = -0.12 - index * 0.14
            mesh.ribbon(f"grip_wrap_{index}", "wrap", "silk", ((-0.12, -0.04), (0.12, 0.04)), 0.045, 0.04, (0, y, 0.12))
        mesh.extrude("wrapped_pommel", "pommel", "polished_metal", ((-0.14, -0.78), (0, -0.88), (0.14, -0.78), (0, -0.68)), 0.15)
        jewel(mesh, "wrapped_gem", (0, -0.78, 0.11), 0.075, material="gem", surface="emissive")
    else:
        mesh.lathe("ringed_grip", "grip", "wood", ((0.10, -0.88), (0.11, 0)), 8)
        for index, y in enumerate((-0.10, -0.28, -0.46, -0.64)):
            mesh.torus(f"grip_ring_{index}", "wrap", "polished_metal", 0.12, 0.025, 8, 4, (0, y, 0))
        mesh.ring("ring_pommel", "pommel", "polished_metal", (0.14, 0.14), (0.07, 0.07), 0.10, 10, (0, -0.76, 0))
    return mesh


def sword_grip_core(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "silk_cord":
        mesh.box("silk_grip_core", "grip", "wood", (0.19, 0.70, 0.18), (0, -0.35, 0))
        for index in range(6):
            y = -0.07 - index * 0.105
            direction = -1 if index % 2 else 1
            mesh.ribbon(f"silk_wrap_{index}", "wrap", "silk", ((-0.11 * direction, -0.04), (0.11 * direction, 0.04)), 0.04, 0.035, (0, y, 0.105))
    elif variant == "jade_handle":
        mesh.lathe("jade_handle", "grip", "jade", ((0.10, -0.70), (0.115, -0.56), (0.095, -0.12), (0.11, 0)), 10)
        for y in (-0.58, -0.34, -0.10):
            mesh.torus(f"jade_edge_{y}", "edge", "polished_metal", 0.11, 0.022, 10, 4, (0, y, 0))
        jewel(mesh, "handle_gem", (0, -0.35, 0.105), 0.065, material="gem", surface="emissive")
    else:
        mesh.lathe("bone_handle", "grip", "bone", ((0.11, -0.70), (0.09, -0.55), (0.13, -0.35), (0.09, -0.15), (0.11, 0)), 8)
        for y in (-0.52, -0.30, -0.08):
            mesh.extrude(f"bone_ridge_{y}", "main", "aged_metal", ((-0.13, -0.04), (0, 0.06), (0.13, -0.04), (0, -0.08)), 0.05, (0, y, 0.10))
        mesh.ribbon("bone_edge", "edge", "polished_metal", ((0, -0.64), (0, -0.06)), 0.03, 0.035, (0, 0, 0.13))
    return mesh


def sword_pommel(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "cloud_ring":
        mesh.ring("cloud_ring", "pommel", "polished_metal", (0.18, 0.17), (0.09, 0.08), 0.10, 10, (0, -0.16, 0))
        cloud_pair(mesh, "pommel_cloud", (0, -0.12, 0.08), 0.22, material="pommel", surface="polished_metal", depth=0.04)
        jewel(mesh, "cloud_ring_gem", (0, -0.16, 0.09), 0.06, material="gem", surface="emissive")
    elif variant == "lotus_knot":
        lotus(mesh, "pommel_lotus", (0, -0.12, 0), 0.17, petals=6, material="main", surface="polished_metal")
        jewel(mesh, "lotus_gem", (0, -0.14, 0.12), 0.065, material="gem", surface="jade")
    else:
        mesh.extrude("needle_pommel", "pommel", "aged_metal", ((-0.13, 0), (-0.08, -0.17), (0, -0.35), (0.08, -0.17), (0.13, 0), (0, -0.07)), 0.11)
        mesh.ribbon("needle_edge", "edge", "polished_metal", ((0, -0.04), (0, -0.31)), 0.035, 0.04, (0, 0, 0.08))
        jewel(mesh, "needle_gem", (0, -0.08, 0.09), 0.055, material="gem", surface="emissive")
    return mesh


def sword_charm(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "silk_tassel":
        mesh.ribbon("tassel_wrap", "wrap", "silk", ((0, 0), (0.03, -0.12)), 0.04, 0.035)
        tassel(mesh, "sword_tassel", (0.02, -0.12, 0), 0.60, strands=3, material="charm")
    elif variant == "jade_tablet":
        mesh.ribbon("tablet_wrap", "wrap", "silk", ((0, 0), (0.04, -0.20)), 0.04, 0.035)
        mesh.beveled_box("sword_tablet", "charm", "jade", (0.26, 0.36, 0.09), 0.05, (0.04, -0.38, 0))
        mesh.ribbon("tablet_edge", "edge", "polished_metal", ((0, -0.52), (0, -0.72)), 0.035, 0.035, (0.04, 0, 0.05))
    else:
        mesh.ribbon("bell_wrap", "wrap", "silk", ((0, 0), (0.02, -0.24)), 0.04, 0.035)
        small_bell(mesh, "sword_bell", (0.02, -0.40, 0), 0.30, material="charm", surface="polished_metal")
        jewel(mesh, "bell_gem", (0.02, -0.38, 0.12), 0.05, material="gem", surface="emissive")
        mesh.ribbon("bell_tail", "edge", "silk", ((0, -0.52), (0, -0.72)), 0.045, 0.035, (0.02, 0, 0))
    return mesh


def seal_mountain(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    body_surface = "jade" if variant == "green" else "stone"
    mesh.frustum("mountain_base_lower", "right", body_surface, (0.78, 0.56), (0.70, 0.50), 0.22, 4, (0, -0.64, 0), start_angle=45)
    mesh.frustum("mountain_base_upper", "ridge", "polished_metal", (0.69, 0.49), (0.60, 0.43), 0.12, 4, (0, -0.47, 0), start_angle=45)
    peaks = (
        (-0.30, -0.38, 0.42, 0.78, -0.08),
        (0.04, -0.34, 0.50, 1.12, 0.02),
        (0.36, -0.37, 0.34, 0.70, 0.10),
    )
    for index, (x, base_y, half_width, height, z) in enumerate(peaks):
        outline = ((-half_width, 0), (-half_width * 0.48, height * 0.42), (0, height), (half_width * 0.45, height * 0.36), (half_width, 0))
        mesh.extrude(f"mountain_peak_{index}", "left" if index == 1 else "right", body_surface, outline, 0.22, (x, base_y, z))
        mesh.ribbon(
            f"mountain_ridge_{index}", "ridge", "polished_metal",
            ((x - half_width * 0.30, base_y + height * 0.20), (x, base_y + height * 0.82)),
            0.038, 0.035, (0, 0, z + 0.13),
        )
    mesh.ribbon("mountain_water", "water", "emissive", ((-0.55, -0.38), (-0.20, -0.31), (0.14, -0.38), (0.52, -0.30)), 0.055, 0.04, (0, 0, 0.28))
    jewel(mesh, "mountain_moon", (-0.36, 0.14, 0.14), 0.06, material="top", surface="emissive")
    return mesh


def seal_base(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "square_bevel":
        mesh.frustum("square_base", "right", "jade", (0.64, 0.50), (0.58, 0.45), 0.20, 4, (0, -0.12, 0), start_angle=45)
        mesh.frustum("square_step", "ridge", "polished_metal", (0.57, 0.44), (0.48, 0.37), 0.11, 4, (0, 0.03, 0), start_angle=45)
    elif variant == "octagonal_step":
        mesh.frustum("octagonal_base", "right", "stone", (0.66, 0.51), (0.59, 0.45), 0.20, 8, (0, -0.12, 0), start_angle=22.5)
        mesh.frustum("octagonal_step", "ridge", "polished_metal", (0.58, 0.44), (0.48, 0.36), 0.11, 8, (0, 0.03, 0), start_angle=22.5)
    else:
        mesh.frustum("lotus_base", "right", "jade", (0.64, 0.49), (0.56, 0.43), 0.18, 8, (0, -0.11, 0), start_angle=22.5)
        mesh.frustum("lotus_step", "ridge", "polished_metal", (0.55, 0.42), (0.46, 0.35), 0.10, 8, (0, 0.03, 0), start_angle=22.5)
        lotus(mesh, "lotus_plinth", (0, 0.05, 0.38), 0.24, petals=8, material="main", surface="polished_metal")
        mesh.ribbon("plinth_ridge", "ridge", "polished_metal", ((-0.46, -0.02), (0.46, -0.02)), 0.038, 0.03, (0, 0, 0.42))
    return mesh


def seal_body(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "jade_block":
        mesh.frustum("jade_block", "top", "jade", (0.50, 0.42), (0.44, 0.37), 0.64, 4, (0, 0.32, 0), start_angle=45)
        mesh.beveled_box("jade_inset", "left", "polished_metal", (0.64, 0.42, 0.045), 0.065, (0, 0.31, 0.39))
        mesh.ribbon("jade_block_shoulder", "ridge", "polished_metal", ((-0.38, 0.58), (0.38, 0.58)), 0.035, 0.03, (0, 0, 0.25))
    elif variant == "tall_tablet":
        mesh.frustum("tall_tablet", "top", "stone", (0.39, 0.34), (0.34, 0.29), 0.68, 4, (0, 0.34, 0), start_angle=45)
        for y in (0.20, 0.38, 0.56):
            mesh.ribbon(f"tablet_ridge_{y}", "ridge", "polished_metal", ((-0.20, 0), (0.20, 0)), 0.032, 0.028, (0, y, 0.32))
    else:
        mesh.lathe("round_dragon_body", "top", "jade", ((0.40, 0), (0.46, 0.10), (0.44, 0.52), (0.34, 0.64)), 16, cap_bottom=True)
        mesh.torus("round_body_foot", "ridge", "polished_metal", 0.42, 0.028, 16, 4, (0, 0.05, 0))
        mesh.ribbon("dragon_coil", "ridge", "polished_metal", ((-0.27, 0.16), (0.22, 0.28), (-0.20, 0.43), (0.18, 0.57)), 0.042, 0.035, (0, 0, 0.43))
    return mesh


def seal_crown(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "mountain_peak":
        mesh.extrude("mountain_crown_back", "top", "jade", ((-0.38, 0), (-0.22, 0.24), (-0.08, 0.18), (0.04, 0.52), (0.17, 0.24), (0.36, 0)), 0.26, (0, 0, -0.04))
        mesh.extrude("mountain_crown_front", "main", "jade", ((-0.26, 0), (-0.12, 0.18), (0.02, 0.40), (0.14, 0.15), (0.28, 0)), 0.15, (0.05, 0, 0.15))
        mesh.ribbon("peak_ridge", "ridge", "polished_metal", ((0.04, 0.08), (0.04, 0.46)), 0.035, 0.03, (0, 0, 0.18))
        jewel(mesh, "peak_moon", (-0.20, 0.24, 0.14), 0.042, material="gem", surface="emissive")
    elif variant == "guardian_lion":
        mesh.ellipsoid("lion_body", "top", "stone", (0.28, 0.15, 0.19), 12, 5, (-0.06, 0.15, 0))
        mesh.ellipsoid("lion_chest", "main", "stone", (0.15, 0.18, 0.15), 10, 5, (0.13, 0.20, 0.04))
        mesh.ring("lion_mane", "ridge", "aged_metal", (0.20, 0.19), (0.145, 0.135), 0.08, 12, (0.13, 0.34, 0.05))
        mesh.ellipsoid("lion_head", "main", "stone", (0.145, 0.13, 0.13), 10, 5, (0.14, 0.34, 0.10))
        for x in (0.07, 0.20):
            mesh.extrude(f"lion_ear_{x}", "ridge", "aged_metal", ((-0.045, 0), (0, 0.10), (0.045, 0)), 0.06, (x, 0.43, 0.08))
        jewel(mesh, "lion_eye", (0.20, 0.37, 0.22), 0.024, material="gem", surface="emissive")
        mesh.ribbon("lion_tail", "ridge", "aged_metal", ((-0.27, 0.15), (-0.37, 0.28), (-0.29, 0.38)), 0.045, 0.05, (0, 0, 0.08))
        for x in (-0.18, 0.15):
            mesh.ellipsoid(f"lion_paw_{x}", "main", "stone", (0.09, 0.065, 0.08), 8, 4, (x, 0.035, 0.10))
    else:
        mesh.ring("dragon_loop", "top", "jade", (0.29, 0.27), (0.18, 0.16), 0.18, 14, (0, 0.26, 0))
        mesh.ribbon("dragon_spine", "ridge", "polished_metal", ((-0.23, 0.13), (-0.05, 0.44), (0.23, 0.29)), 0.042, 0.04, (0, 0, 0.13))
        mesh.extrude("dragon_head", "main", "jade", ((-0.08, 0.40), (0.04, 0.52), (0.15, 0.42), (0.06, 0.33)), 0.12, (0, 0, 0.10))
        jewel(mesh, "dragon_loop_gem", (0, 0.26, 0.16), 0.055, material="gem", surface="emissive")
    return mesh


def seal_face(variant: str) -> ModelBuilder:
    mesh = ModelBuilder()
    if variant == "heaven_script":
        mesh.beveled_box("script_plate", "ridge", "polished_metal", (0.54, 0.36, 0.055), 0.06, (0, 0.32, 0.40))
        for index, y in enumerate((0.22, 0.32, 0.42)):
            mesh.ribbon(f"heaven_script_{index}", "core", "emissive", ((-0.14 + index * 0.035, 0), (0.14 - index * 0.02, 0)), 0.025, 0.025, (0, y, 0.45))
    elif variant == "eight_trigrams":
        mesh.disc("trigram_plate", "ridge", "polished_metal", 0.28, 0.24, 0.055, 16, (0, 0.32, 0.40))
        trigram(mesh, "seal_trigram", (0, 0.32, 0.45), 0.17, material="main", surface="aged_metal")
        jewel(mesh, "trigram_gem", (0, 0.32, 0.50), 0.038, material="gem", surface="emissive")
    else:
        mesh.extrude("eye_plate", "ridge", "polished_metal", ((-0.28, 0.32), (-0.14, 0.45), (0, 0.49), (0.14, 0.45), (0.28, 0.32), (0.14, 0.19), (0, 0.15), (-0.14, 0.19)), 0.06, (0, 0, 0.40))
        mesh.faceted_disc("celestial_eye", "core", "emissive", 0.11, 0.08, 0.05, 0.04, 12, (0, 0.32, 0.47))
        jewel(mesh, "eye_pupil", (0, 0.32, 0.52), 0.030, material="gem", surface="crystal")
    return mesh


_FACTORIES = {
    "sword_blade3d": sword_blade,
    "sword_guard3d": sword_guard,
    "sword_grip3d": sword_grip_legacy,
    "sword_grip_core3d": sword_grip_core,
    "sword_pommel3d": sword_pommel,
    "sword_charm3d": sword_charm,
    "seal_mountain3d": seal_mountain,
    "seal_base3d": seal_base,
    "seal_body3d": seal_body,
    "seal_crown3d": seal_crown,
    "seal_face3d": seal_face,
}
