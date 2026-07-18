#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把 AppearanceCatalog 中的旧 parts 几何导出为运行时和 Blockbench 共用的 OBJ。"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

from .mesh3d import Face3D, part_faces


SURFACE_COLORS: dict[str, tuple[float, float, float]] = {
    "neutral": (0.55, 0.57, 0.60),
    "polished_metal": (0.62, 0.69, 0.74),
    "aged_metal": (0.42, 0.38, 0.31),
    "jade": (0.25, 0.62, 0.48),
    "crystal": (0.30, 0.70, 0.82),
    "emissive": (0.42, 0.94, 0.72),
    "silk": (0.62, 0.28, 0.42),
    "stone": (0.43, 0.44, 0.46),
    "wood": (0.36, 0.24, 0.17),
    "bone": (0.74, 0.70, 0.58),
}
SURFACE_SEPARATOR = "__surface__"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--catalog",
        type=Path,
        default=Path("Content/Artifacts/AppearanceCatalog"),
        help="AppearanceCatalog 路径",
    )
    checks = parser.add_mutually_exclusive_group()
    checks.add_argument("--check", action="store_true", help="检查现有 OBJ、材质语义和锚点，不比较旧 parts")
    checks.add_argument(
        "--check-generated",
        action="store_true",
        help="仅在首次迁移阶段检查模型是否仍与旧 parts 的生成结果完全一致",
    )
    parser.add_argument(
        "--force-legacy",
        action="store_true",
        help="确认用旧 parts 覆盖当前 OBJ；只用于首次迁移或诊断",
    )
    args = parser.parse_args(argv)

    if args.check:
        variants, problems = validate_runtime_models(args.catalog)
        if problems:
            for problem in problems[:30]:
                print(problem, file=sys.stderr)
            if len(problems) > 30:
                print(f"另有 {len(problems) - 30} 个问题", file=sys.stderr)
            return 1
        print(f"已验证 {variants} 个 variant 的 OBJ、材质语义和锚点")
        return 0

    if not args.check_generated and not args.force_legacy:
        parser.error("旧 parts 导出会覆盖重制模型；如确需迁移，请显式传入 --force-legacy")

    expected = build_outputs(args.catalog)
    mismatches = [path for path, content in expected.items() if not path.exists() or path.read_text("utf-8") != content]
    if args.check_generated:
        if mismatches:
            for path in mismatches[:20]:
                print(f"不一致: {path}", file=sys.stderr)
            if len(mismatches) > 20:
                print(f"另有 {len(mismatches) - 20} 个文件不一致", file=sys.stderr)
            return 1
    else:
        for path in mismatches:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(expected[path], encoding="utf-8")

    variants = len(expected) // 3
    verb = "已验证生成结果" if args.check_generated else "已导出"
    print(f"{verb} {variants} 个 variant，{len(expected)} 个模型文件")
    return 0


def build_outputs(catalog: Path) -> dict[Path, str]:
    outputs: dict[Path, str] = {}
    module_files = sorted(catalog.glob("modules*.json"))
    if not module_files:
        raise FileNotFoundError(f"找不到 modules*.json: {catalog}")
    for module_file in module_files:
        data = json.loads(module_file.read_text("utf-8"))
        model_root = str(data.get("model_root", "Models"))
        for module in data.get("modules", []):
            module_key = str(module["key"])
            expected_anchor_keys: set[str] | None = None
            for variant in module.get("variants", []):
                variant_key = str(variant["key"])
                anchors = {
                    str(anchor["key"]): [float(value) for value in anchor["position"]]
                    for anchor in variant.get("anchors", [])
                }
                keys = set(anchors)
                if expected_anchor_keys is None:
                    expected_anchor_keys = keys
                elif keys != expected_anchor_keys:
                    raise ValueError(f"{module_key}.{variant_key} 的锚点 key 与同模块 variant 不一致")
                faces = [face for part in variant.get("parts", []) for face in part_faces(part)]
                if not faces:
                    raise ValueError(f"{module_key}.{variant_key} 没有可导出的 parts")

                relative_model = variant.get("model") or f"{model_root}/{module_key}/{variant_key}.obj"
                obj_path = resolve_catalog_path(catalog, str(relative_model))
                outputs[obj_path] = render_obj(module_key, variant_key, obj_path.with_suffix(".mtl").name, faces)
                outputs[obj_path.with_suffix(".mtl")] = render_mtl(faces)
                outputs[obj_path.with_suffix(".anchors.json")] = (
                    json.dumps({"anchors": anchors}, ensure_ascii=False, indent=2) + "\n"
                )
    return outputs


def validate_runtime_models(catalog: Path) -> tuple[int, list[str]]:
    catalog = catalog.resolve()
    surfaces = load_surface_keys(catalog)
    problems: list[str] = []
    expected_models: set[Path] = set()
    variant_count = 0
    for module_file in sorted(catalog.glob("modules*.json")):
        data = json.loads(module_file.read_text("utf-8"))
        model_root = str(data.get("model_root", "Models"))
        for module in data.get("modules", []):
            module_key = str(module["key"])
            module_anchor_keys: set[str] | None = None
            for variant in module.get("variants", []):
                variant_count += 1
                variant_key = str(variant["key"])
                identity = f"{module_key}.{variant_key}"
                relative_model = variant.get("model") or f"{model_root}/{module_key}/{variant_key}.obj"
                try:
                    obj_path = resolve_catalog_path(catalog, str(relative_model))
                except ValueError as error:
                    problems.append(f"{identity}: {error}")
                    continue
                expected_models.add(obj_path)
                material_surfaces = {
                    str(key): str(value)
                    for key, value in variant.get("material_surfaces", {}).items()
                }
                problems.extend(validate_obj(obj_path, identity, material_surfaces, surfaces))

                mtl_path = obj_path.with_suffix(".mtl")
                if not mtl_path.is_file():
                    problems.append(f"{identity}: 缺少 Blockbench 预览材质 {mtl_path}")
                anchor_path = obj_path.with_suffix(".anchors.json")
                anchor_keys = validate_anchors(anchor_path, identity, problems)
                if anchor_keys:
                    if module_anchor_keys is None:
                        module_anchor_keys = anchor_keys
                    elif anchor_keys != module_anchor_keys:
                        problems.append(f"{identity}: 锚点 key 与同模块其他 variant 不一致")

    actual_models = set((catalog / "Models").rglob("*.obj")) if (catalog / "Models").is_dir() else set()
    for extra in sorted(actual_models - expected_models):
        problems.append(f"未被 module/variant 引用的 OBJ: {extra}")
    return variant_count, problems


def validate_obj(
    path: Path,
    identity: str,
    material_surfaces: dict[str, str],
    surfaces: set[str],
) -> list[str]:
    if not path.is_file():
        return [f"{identity}: 缺少 OBJ {path}"]
    problems: list[str] = []
    vertices = 0
    faces = 0
    material = "main"
    for line_number, raw in enumerate(path.read_text("utf-8").splitlines(), 1):
        line = raw.split("#", 1)[0].strip()
        if not line:
            continue
        values = line.split()
        command, payload = values[0], values[1:]
        try:
            if command == "v":
                if len(payload) < 3:
                    raise ValueError("顶点坐标不完整")
                tuple(float(value) for value in payload[:3])
                vertices += 1
            elif command == "usemtl":
                if not payload:
                    raise ValueError("材质名为空")
                encoded = " ".join(payload)
                if SURFACE_SEPARATOR in encoded:
                    material, surface = encoded.split(SURFACE_SEPARATOR, 1)
                else:
                    material = encoded
                    surface = material_surfaces.get(material, "neutral")
                if not material:
                    raise ValueError("材质角色为空")
                if surface not in surfaces:
                    raise ValueError(f"不存在的表面语义 {surface}")
            elif command == "f":
                if len(payload) < 3:
                    raise ValueError("面至少需要三个顶点")
                for token in payload:
                    raw_index = token.split("/", 1)[0]
                    index = int(raw_index)
                    resolved = index - 1 if index > 0 else vertices + index
                    if resolved < 0 or resolved >= vertices:
                        raise ValueError(f"面索引越界 {token}")
                faces += 1
        except ValueError as error:
            problems.append(f"{identity}: {path}:{line_number} {error}")
    if vertices == 0 or faces == 0:
        problems.append(f"{identity}: OBJ 没有有效几何体 {path}")
    return problems


def validate_anchors(path: Path, identity: str, problems: list[str]) -> set[str]:
    if not path.is_file():
        problems.append(f"{identity}: 缺少锚点 {path}")
        return set()
    try:
        anchors = json.loads(path.read_text("utf-8")).get("anchors", {})
    except (json.JSONDecodeError, AttributeError) as error:
        problems.append(f"{identity}: 无法读取锚点 {path}: {error}")
        return set()
    if not isinstance(anchors, dict) or not anchors:
        problems.append(f"{identity}: 没有锚点 {path}")
        return set()
    for key, position in anchors.items():
        if not isinstance(position, list) or len(position) != 3 or not all(
            isinstance(value, (int, float)) for value in position
        ):
            problems.append(f"{identity}: 锚点 {key} 必须是三维数值坐标")
    return {str(key) for key in anchors}


def load_surface_keys(catalog: Path) -> set[str]:
    keys: set[str] = set()
    for path in sorted(catalog.glob("surfaces*.json")):
        data = json.loads(path.read_text("utf-8"))
        keys.update(str(style["key"]) for style in data.get("styles", []))
    return keys or {"neutral"}


def resolve_catalog_path(catalog: Path, relative: str) -> Path:
    path = (catalog / relative).resolve()
    try:
        path.relative_to(catalog.resolve())
    except ValueError as error:
        raise ValueError(f"模型路径越过 AppearanceCatalog: {relative}") from error
    if path.suffix.lower() != ".obj":
        raise ValueError(f"当前只支持 OBJ: {relative}")
    return path


def render_obj(module: str, variant: str, mtl_name: str, faces: list[Face3D]) -> str:
    lines = [
        "# Cultiway artifact variant - Blockbench compatible OBJ",
        f"# module: {module}",
        f"# variant: {variant}",
        f"mtllib {mtl_name}",
        f"o {safe_name(variant)}",
        "s off",
    ]
    vertex_indices: dict[tuple[float, float, float], int] = {}
    vertices: list[tuple[float, float, float]] = []
    face_rows: list[tuple[str, tuple[int, ...]]] = []
    for face in faces:
        indices: list[int] = []
        for point in face.points:
            point = tuple(float(value) for value in point)
            index = vertex_indices.get(point)
            if index is None:
                vertices.append(point)
                index = len(vertices)
                vertex_indices[point] = index
            indices.append(index)
        material = safe_name(f"{face.material}{SURFACE_SEPARATOR}{face.surface}")
        face_rows.append((material, tuple(indices)))

    lines.extend(f"v {x:.6f} {y:.6f} {z:.6f}" for x, y, z in vertices)
    current_material = None
    for material, indices in face_rows:
        if material != current_material:
            current_material = material
            lines.append(f"usemtl {material}")
        lines.append("f " + " ".join(str(index) for index in indices))
    return "\n".join(lines) + "\n"


def render_mtl(faces: list[Face3D]) -> str:
    materials = sorted({(face.material, face.surface) for face in faces})
    lines = ["# Preview colors only; ArtifactAppearance supplies final Instance colors", ""]
    for material, surface in materials:
        name = safe_name(f"{material}{SURFACE_SEPARATOR}{surface}")
        red, green, blue = SURFACE_COLORS.get(surface, SURFACE_COLORS["neutral"])
        lines.extend((
            f"newmtl {name}",
            f"Ka {red * 0.18:.4f} {green * 0.18:.4f} {blue * 0.18:.4f}",
            f"Kd {red:.4f} {green:.4f} {blue:.4f}",
            "Ks 0.1800 0.1800 0.1800",
            "Ns 24.0000",
            "d 1.0000",
            "illum 2",
            "",
        ))
    return "\n".join(lines)


def safe_name(value: str) -> str:
    value = re.sub(r"[^0-9A-Za-z_.-]+", "_", value).strip("_")
    return value or "unnamed"


if __name__ == "__main__":
    raise SystemExit(main())
