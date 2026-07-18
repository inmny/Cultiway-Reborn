#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""剑、镜、鼎三套 Blockbench 往返样例及其模板、模块、Instance 数据。"""

from __future__ import annotations

from pathlib import Path

from Scripts.artifact_compose.math3d import Vec3, add, rotate_euler

from .mesh_builder import MeshBuilder
from .obj_io import load_model, write_model
from .types import (
    AppearanceTemplate,
    MeshFace,
    ModuleDefinition,
    ModuleVariant,
    Placement,
    PrototypeCatalog,
    ViewSpec,
    VisualInstance,
)


def build_demo_catalog(model_directory: Path) -> PrototypeCatalog:
    sources = _write_demo_models(model_directory)
    models = {key: load_model(key, path) for key, path in sources.items()}
    modules = {
        "sword.blade": ModuleDefinition("sword.blade", (
            ModuleVariant("willow", "sword_blade_willow"),
            ModuleVariant("ritual", "sword_blade_ritual"),
        )),
        "sword.guard": ModuleDefinition("sword.guard", (
            ModuleVariant("cloud", "sword_guard_cloud"),
            ModuleVariant("lotus", "sword_guard_lotus"),
        )),
        "sword.grip": ModuleDefinition("sword.grip", (ModuleVariant("corded", "sword_grip_corded"),)),
        "sword.charm": ModuleDefinition("sword.charm", (ModuleVariant("tassel", "sword_charm_tassel"),)),
        "mirror.frame": ModuleDefinition("mirror.frame", (
            ModuleVariant("moon", "mirror_frame_moon"),
            ModuleVariant("octagonal", "mirror_frame_octagonal"),
        )),
        "mirror.surface": ModuleDefinition("mirror.surface", (ModuleVariant("deep", "mirror_surface_deep"),)),
        "mirror.handle": ModuleDefinition("mirror.handle", (ModuleVariant("jade", "mirror_handle_jade"),)),
        "mirror.crown": ModuleDefinition("mirror.crown", (ModuleVariant("cloud", "mirror_crown_cloud"),)),
        "ding.vessel": ModuleDefinition("ding.vessel", (ModuleVariant("round", "ding_vessel_round"),)),
        "ding.lid": ModuleDefinition("ding.lid", (ModuleVariant("spire", "ding_lid_spire"),)),
        "ding.ears": ModuleDefinition("ding.ears", (ModuleVariant("loop", "ding_ears_loop"),)),
        "ding.legs": ModuleDefinition("ding.legs", (ModuleVariant("beast", "ding_legs_beast"),)),
        "ding.core": ModuleDefinition("ding.core", (ModuleVariant("ember", "ding_core_ember"),)),
    }
    templates = {
        "sword.flying": AppearanceTemplate(
            "sword.flying",
            "Sword",
            (
                Placement("blade", "sword.blade", "hilt", position=(0.0, -0.7, 0.0), order=1),
                Placement("guard", "sword.guard", "mount", position=(0.0, -0.7, 0.0), order=3),
                Placement("grip", "sword.grip", "guard", position=(0.0, -0.7, 0.0), order=2),
                Placement("charm", "sword.charm", "knot", position=(0.0, -0.7, 0.0), order=4),
            ),
            (
                ViewSpec("icon_56", 56, (0.0, -12.0, 0.0), margin=3, supersample=2),
                ViewSpec("world_idle_24", 24, (0.0, -8.0, 180.0), margin=2),
                ViewSpec("world_active_56", 56, (0.0, -12.0, 0.0), margin=3),
            ),
        ),
        "mirror.lunar": AppearanceTemplate(
            "mirror.lunar",
            "Mirror",
            (
                Placement("surface", "mirror.surface", "center", order=0),
                Placement("frame", "mirror.frame", "center", order=2),
                Placement("handle", "mirror.handle", "center", order=1),
                Placement("crown", "mirror.crown", "center", order=3),
            ),
            (
                ViewSpec("icon_56", 56, (0.0, -12.0, 0.0), margin=3, supersample=2),
                ViewSpec("world_idle_24", 24, (0.0, 0.0, 0.0), margin=2),
                ViewSpec("world_active_56", 56, (0.0, -12.0, 0.0), margin=3),
            ),
        ),
        "ding.alchemy": AppearanceTemplate(
            "ding.alchemy",
            "Ding",
            (
                Placement("core", "ding.core", "center", order=0),
                Placement("vessel", "ding.vessel", "center", order=1),
                Placement("ears", "ding.ears", "center", order=2),
                Placement("legs", "ding.legs", "center", order=2),
                Placement("lid", "ding.lid", "center", order=3),
            ),
            (
                ViewSpec("icon_56", 56, (-15.0, -30.0, 0.0), margin=3, supersample=2),
                ViewSpec("world_idle_24", 24, (-12.0, -22.0, 0.0), margin=2),
                ViewSpec("world_active_56", 56, (-15.0, -30.0, 0.0), margin=3),
            ),
        ),
    }
    catalog = PrototypeCatalog(models, modules, templates)
    catalog.validate()
    return catalog


def build_demo_instances(catalog: PrototypeCatalog) -> tuple[VisualInstance, ...]:
    return (
        VisualInstance(
            "moonlit_flying_sword",
            catalog.templates["sword.flying"],
            "moon_silver",
            {"blade": "willow", "guard": "cloud", "grip": "corded", "charm": "tassel"},
        ),
        VisualInstance(
            "celestial_lunar_mirror",
            catalog.templates["mirror.lunar"],
            "celestial_jade",
            {"surface": "deep", "frame": "moon", "handle": "jade", "crown": "cloud"},
        ),
        VisualInstance(
            "ember_alchemy_ding",
            catalog.templates["ding.alchemy"],
            "copper_ember",
            {"core": "ember", "vessel": "round", "ears": "loop", "legs": "beast", "lid": "spire"},
        ),
    )


def _write_demo_models(directory: Path) -> dict[str, Path]:
    definitions = {
        "sword_blade_willow": (_sword_blade_willow(), {"hilt": (0.0, 0.0, 0.0)}),
        "sword_blade_ritual": (_sword_blade_ritual(), {"hilt": (0.0, 0.0, 0.0)}),
        "sword_guard_cloud": (_sword_guard_cloud(), {"mount": (0.0, 0.0, 0.0)}),
        "sword_guard_lotus": (_sword_guard_lotus(), {"mount": (0.0, 0.0, 0.0)}),
        "sword_grip_corded": (_sword_grip(), {"guard": (0.0, 0.0, 0.0)}),
        "sword_charm_tassel": (_sword_charm(), {"knot": (0.0, 0.0, 0.0)}),
        "mirror_frame_moon": (_mirror_frame(18), {"center": (0.0, 0.0, 0.0)}),
        "mirror_frame_octagonal": (_mirror_frame(8), {"center": (0.0, 0.0, 0.0)}),
        "mirror_surface_deep": (_mirror_surface(), {"center": (0.0, 0.0, 0.0)}),
        "mirror_handle_jade": (_mirror_handle(), {"center": (0.0, 0.0, 0.0)}),
        "mirror_crown_cloud": (_mirror_crown(), {"center": (0.0, 0.0, 0.0)}),
        "ding_vessel_round": (_ding_vessel(), {"center": (0.0, 0.0, 0.0)}),
        "ding_lid_spire": (_ding_lid(), {"center": (0.0, 0.0, 0.0)}),
        "ding_ears_loop": (_ding_ears(), {"center": (0.0, 0.0, 0.0)}),
        "ding_legs_beast": (_ding_legs(), {"center": (0.0, 0.0, 0.0)}),
        "ding_core_ember": (_ding_core(), {"center": (0.0, 0.0, 0.0)}),
    }
    result: dict[str, Path] = {}
    for key, (builder, anchors) in definitions.items():
        path = directory / f"{key}.obj"
        write_model(path, builder.faces, anchors)
        result[key] = path
    return result


def _sword_blade_willow() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.extrude_polygon("willow_blade", "metal", (
        (-0.22, 0.0), (-0.46, 0.85), (-0.40, 4.65), (-0.20, 5.65),
        (0.0, 6.35), (0.20, 5.65), (0.40, 4.65), (0.46, 0.85), (0.22, 0.0),
    ), 0.28)
    mesh.extrude_polygon("blade_ridge", "trim", (
        (-0.08, 0.3), (-0.10, 4.7), (0.0, 5.75), (0.10, 4.7), (0.08, 0.3),
    ), 0.10, (0.0, 0.0, 0.18))
    mesh.extrude_polygon("blade_rune", "glow", (
        (0.0, 2.65), (-0.11, 2.9), (0.0, 3.18), (0.11, 2.9),
    ), 0.08, (0.0, 0.0, 0.24))
    return mesh


def _sword_blade_ritual() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.extrude_polygon("ritual_blade", "metal", (
        (-0.30, 0.0), (-0.62, 0.95), (-0.50, 4.2), (-0.72, 4.65),
        (-0.28, 5.05), (0.0, 6.25), (0.28, 5.05), (0.72, 4.65),
        (0.50, 4.2), (0.62, 0.95), (0.30, 0.0),
    ), 0.32)
    mesh.extrude_polygon("ritual_ridge", "jade", (
        (-0.11, 0.35), (-0.14, 4.45), (0.0, 5.55), (0.14, 4.45), (0.11, 0.35),
    ), 0.11, (0.0, 0.0, 0.20))
    return mesh


def _sword_guard_cloud() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.extrude_polygon("left_cloud", "trim", (
        (0.0, -0.16), (-0.58, -0.36), (-1.38, -0.20), (-1.90, 0.18),
        (-1.25, 0.12), (-0.62, 0.38), (0.0, 0.20),
    ), 0.38)
    mesh.extrude_polygon("right_cloud", "trim", (
        (0.0, -0.16), (0.58, -0.36), (1.38, -0.20), (1.90, 0.18),
        (1.25, 0.12), (0.62, 0.38), (0.0, 0.20),
    ), 0.38)
    mesh.disc("guard_jade", "jade", 0.48, 0.48, 0.46, 12, (0.0, 0.0, 0.08))
    return mesh


def _sword_guard_lotus() -> MeshBuilder:
    mesh = MeshBuilder()
    for offset_x, rotation in ((-0.75, 24.0), (0.75, -24.0)):
        petal = MeshBuilder()
        petal.extrude_polygon("lotus_petal", "trim", (
            (-0.72, -0.20), (0.0, -0.42), (0.78, 0.0), (0.0, 0.36),
        ), 0.34)
        _append(mesh, petal, (0.0, 0.0, rotation), (offset_x, 0.0, 0.0))
    mesh.disc("lotus_core", "jade", 0.50, 0.46, 0.46, 12, (0.0, 0.0, 0.08))
    return mesh


def _sword_grip() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.box("grip_core", "grip", (0.48, 2.15, 0.45), (0.0, -1.08, 0.0))
    for index in range(5):
        y = -0.30 - index * 0.40
        mesh.box(f"grip_band_{index}", "trim", (0.58, 0.13, 0.52), (0.0, y, 0.0))
    mesh.disc("pommel", "jade", 0.48, 0.48, 0.52, 10, (0.0, -2.28, 0.0))
    return mesh


def _sword_charm() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.disc("charm_knot", "trim", 0.20, 0.20, 0.26, 8, (0.48, -2.28, 0.10))
    mesh.extrude_polygon("tassel_upper", "cloth", (
        (0.42, -2.35), (0.72, -2.50), (0.96, -3.20), (0.70, -3.05),
    ), 0.16, (0.0, 0.0, 0.08))
    mesh.extrude_polygon("tassel_lower", "cloth", (
        (0.70, -3.02), (0.98, -3.20), (0.82, -3.88), (0.56, -3.52),
    ), 0.14, (0.0, 0.0, 0.08))
    return mesh


def _mirror_frame(segments: int) -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.ring("mirror_frame", "trim", (2.60, 2.60), (2.03, 2.03), 0.42, segments)
    mesh.ring("mirror_inner_bezel", "metal", (2.14, 2.14), (1.98, 1.98), 0.48, segments, (0.0, 0.0, 0.04))
    for x, y in ((-2.38, 0.0), (2.38, 0.0), (0.0, 2.38), (0.0, -2.38)):
        mesh.disc("frame_jewel", "jade", 0.30, 0.30, 0.50, 8, (x, y, 0.10))
    return mesh


def _mirror_surface() -> MeshBuilder:
    mesh = MeshBuilder()
    _faceted_disc(mesh, "mirror_surface", "crystal", 1.98, 1.98, 20, 0.04, 0.18)
    mesh.extrude_polygon("lunar_crescent", "jade", (
        (-1.28, -0.62), (-1.04, -1.02), (-0.54, -1.25), (0.06, -1.20),
        (-0.46, -0.86), (-0.78, -0.38), (-0.86, 0.18), (-0.68, 0.72),
        (-0.26, 1.10), (-0.82, 0.94), (-1.20, 0.52), (-1.38, -0.02),
    ), 0.07, (0.0, 0.0, 0.25))
    mesh.extrude_polygon("surface_gleam", "glow", (
        (0.18, 0.78), (0.46, 1.22), (1.12, 0.70), (0.58, 0.58),
    ), 0.06, (0.0, 0.0, 0.27))
    for x, y in ((0.72, 0.08), (1.05, -0.48), (0.40, -0.78)):
        mesh.disc("surface_star", "trim", 0.11, 0.11, 0.08, 6, (x, y, 0.29))
    return mesh


def _mirror_handle() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.extrude_polygon("mirror_handle", "jade", (
        (-0.48, -2.30), (-0.36, -3.85), (0.0, -4.38), (0.36, -3.85), (0.48, -2.30),
    ), 0.42)
    mesh.box("handle_band", "trim", (1.00, 0.28, 0.52), (0.0, -2.70, 0.08))
    mesh.disc("handle_pommel", "trim", 0.50, 0.42, 0.48, 10, (0.0, -4.25, 0.02))
    return mesh


def _mirror_crown() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.extrude_polygon("mirror_crown", "trim", (
        (-1.22, 2.26), (-0.92, 3.10), (-0.38, 2.75), (0.0, 3.62),
        (0.38, 2.75), (0.92, 3.10), (1.22, 2.26), (0.0, 2.48),
    ), 0.36)
    mesh.disc("crown_core", "glow", 0.34, 0.42, 0.44, 10, (0.0, 2.80, 0.12))
    return mesh


def _ding_vessel() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.lathe("ding_body", "metal", (
        (0.62, -1.50), (1.10, -1.40), (1.72, -0.90), (1.98, -0.10),
        (2.04, 0.72), (2.22, 1.02), (2.18, 1.22),
    ), 18, cap_top=False)
    mesh.torus("body_band", "trim", 1.96, 0.12, 18, 6, (0.0, 0.30, 0.0))
    mesh.lathe("dark_mouth", "dark", ((1.86, 1.14), (1.86, 1.18)), 18)
    mesh.extrude_polygon("front_ward", "jade", (
        (-0.34, -0.42), (0.0, -0.68), (0.34, -0.42), (0.28, 0.08),
        (0.0, 0.34), (-0.28, 0.08),
    ), 0.10, (0.0, 0.0, 2.02))
    mesh.extrude_polygon("front_ward_core", "trim", (
        (-0.10, -0.25), (0.0, -0.38), (0.10, -0.25), (0.08, -0.02),
        (0.0, 0.10), (-0.08, -0.02),
    ), 0.08, (0.0, 0.0, 2.10))
    return mesh


def _ding_lid() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.lathe("ding_lid", "trim", (
        (1.45, 2.10), (1.28, 2.26), (0.82, 2.43), (0.42, 2.54), (0.20, 2.72),
    ), 18)
    mesh.lathe("lid_jewel", "jade", ((0.22, 2.66), (0.34, 2.86), (0.10, 3.16)), 12)
    return mesh


def _ding_ears() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.ring("left_ear", "trim", (0.68, 0.95), (0.36, 0.58), 0.38, 12, (-2.22, 0.72, 0.0))
    mesh.ring("right_ear", "trim", (0.68, 0.95), (0.36, 0.58), 0.38, 12, (2.22, 0.72, 0.0))
    return mesh


def _ding_legs() -> MeshBuilder:
    mesh = MeshBuilder()
    for x, z, rotation in ((-1.12, 0.10, -8.0), (1.12, 0.10, 8.0), (0.0, -0.88, 0.0)):
        leg = MeshBuilder()
        leg.extrude_polygon("beast_leg", "metal", (
            (-0.25, -1.18), (-0.30, -2.16), (-0.48, -2.50), (-0.04, -2.42),
            (0.22, -1.26),
        ), 0.36)
        _append(mesh, leg, (0.0, 0.0, rotation), (x, 0.0, z))
        mesh.disc("leg_jewel", "jade", 0.22, 0.22, 0.52, 8, (x, -1.36, z + 0.10))
    return mesh


def _ding_core() -> MeshBuilder:
    mesh = MeshBuilder()
    mesh.lathe("inner_flame", "glow", (
        (0.56, 0.58), (0.82, 1.04), (0.54, 1.54), (0.18, 2.06), (0.0, 2.48),
    ), 12)
    return mesh


def _append(target: MeshBuilder, source: MeshBuilder, rotation: Vec3, offset: Vec3) -> None:
    for face in source.faces:
        target.faces.append(MeshFace(
            tuple(add(rotate_euler(point, rotation), offset) for point in face.points),
            face.material,
            face.object_name,
        ))


def _faceted_disc(
    target: MeshBuilder,
    name: str,
    material: str,
    radius_x: float,
    radius_y: float,
    segments: int,
    base_z: float,
    relief: float,
) -> None:
    import math

    center = (0.0, 0.0, base_z + relief)
    rim = [
        (
            math.cos(math.tau * index / segments) * radius_x,
            math.sin(math.tau * index / segments) * radius_y,
            base_z,
        )
        for index in range(segments)
    ]
    for index in range(segments):
        following = (index + 1) % segments
        target.faces.append(MeshFace((center, rim[index], rim[following]), material, name))
    target.faces.append(MeshFace(tuple(reversed(rim)), "dark", f"{name}_back"))
