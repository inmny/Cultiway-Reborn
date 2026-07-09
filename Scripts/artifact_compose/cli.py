#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""法器贴图组合命令行入口。"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

if __package__ in (None, ""):
    sys.path.append(str(Path(__file__).resolve().parents[1]))
    from artifact_compose.compose import ArtifactComposer, stable_int
    from artifact_compose.compose3d import ArtifactComposer3D
    from artifact_compose.loaders import default_data_dir, load_catalog
    from artifact_compose.loaders3d import default_data3d_dir, load_catalog3d
    from artifact_compose.obj_writer3d import write_obj3d
    from artifact_compose.render import render_png, save_preview
    from artifact_compose.render3d import render_png3d, save_preview as save_preview3d
    from artifact_compose.svg_writer import write_svg
    from artifact_compose.validate import render_smoke, validate_catalog
    from artifact_compose.validate3d import render_smoke3d, validate_catalog3d
else:
    from .compose import ArtifactComposer, stable_int
    from .compose3d import ArtifactComposer3D
    from .loaders import default_data_dir, load_catalog
    from .loaders3d import default_data3d_dir, load_catalog3d
    from .obj_writer3d import write_obj3d
    from .render import render_png, save_preview
    from .render3d import render_png3d, save_preview as save_preview3d
    from .svg_writer import write_svg
    from .validate import render_smoke, validate_catalog
    from .validate3d import render_smoke3d, validate_catalog3d


DEFAULT_OUTPUT = Path("Scripts/artifact_compose/output")


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.command == "list":
        return cmd_list(args)
    if args.command == "list3d":
        return cmd_list3d(args)
    if args.command == "validate":
        return cmd_validate(args)
    if args.command == "validate3d":
        return cmd_validate3d(args)
    if args.command == "compose":
        return cmd_compose(args)
    if args.command == "compose3d":
        return cmd_compose3d(args)
    parser.print_help()
    return 1


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="组合矢量模块，生成法器 SVG 和 28x28 PNG。")
    parser.add_argument("--data", type=Path, default=default_data_dir(), help="模块和模板 JSON 所在目录")
    sub = parser.add_subparsers(dest="command")

    sub.add_parser("list", help="列出可用器形、模板和模块")
    list3d = sub.add_parser("list3d", help="列出可用 3D 器形、模板和模块")
    list3d.add_argument("--data3d", type=Path, default=default_data3d_dir(), help="3D 模块和模板 JSON 所在目录")

    validate = sub.add_parser("validate", help="校验配置，可选执行渲染冒烟检查")
    validate.add_argument("--render-smoke", action="store_true", help="渲染每个模板的首个样本")
    validate.add_argument("--output", type=Path, default=Path("Temp/artifact_compose_validate"), help="冒烟输出目录")

    validate3d = sub.add_parser("validate3d", help="校验 3D 配置，可选执行渲染冒烟检查")
    validate3d.add_argument("--data3d", type=Path, default=default_data3d_dir(), help="3D 模块和模板 JSON 所在目录")
    validate3d.add_argument("--render-smoke", action="store_true", help="渲染每个 3D 模板的首个样本")
    validate3d.add_argument("--output", type=Path, default=Path("Temp/artifact_compose_validate3d"), help="3D 冒烟输出目录")

    compose = sub.add_parser("compose", help="生成 SVG/PNG")
    compose.add_argument("--all", action="store_true", help="生成所有器形")
    compose.add_argument("--shape", action="append", help="指定器形 key，可重复")
    compose.add_argument("--template", help="指定单个模板 key")
    compose.add_argument("--count", type=int, default=3, help="每个器形或模板生成的样本数")
    compose.add_argument("--seed", default="cultiway-artifact-compose", help="确定性随机种子")
    compose.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="输出目录")
    compose.add_argument("--preview-scale", type=int, default=6, help="额外生成放大预览图倍数，0 表示不生成")
    compose.add_argument("--palette-colors", type=int, default=96, help="PNG 最大颜色数，0 表示不限制")
    compose.add_argument("--finish", choices=("artifact", "flat"), default="artifact", help="PNG 像素后处理风格")
    compose.add_argument("--supersample", type=int, default=1, help="矢量栅格化倍率；默认 1 保留硬像素边缘")

    compose3d = sub.add_parser("compose3d", help="生成 3D 法器 PNG")
    compose3d.add_argument("--data3d", type=Path, default=default_data3d_dir(), help="3D 模块和模板 JSON 所在目录")
    compose3d.add_argument("--all", action="store_true", help="生成所有 3D 器形")
    compose3d.add_argument("--shape", action="append", help="指定 3D 器形 key，可重复")
    compose3d.add_argument("--template", help="指定单个 3D 模板 key")
    compose3d.add_argument("--count", type=int, default=3, help="每个 3D 器形或模板生成的样本数")
    compose3d.add_argument("--seed", default="cultiway-artifact-compose-3d", help="3D 确定性随机种子")
    compose3d.add_argument("--output", type=Path, default=Path("Scripts/artifact_compose/output3d"), help="3D 输出目录")
    compose3d.add_argument("--preview-scale", type=int, default=6, help="额外生成放大预览图倍数，0 表示不生成")
    compose3d.add_argument("--palette-colors", type=int, default=0, help="PNG 最大颜色数，0 表示不限制")
    compose3d.add_argument("--no-obj", action="store_true", help="不导出 OBJ/MTL")
    return parser


def cmd_list(args) -> int:
    catalog = load_catalog(args.data)
    print("器形:")
    for shape in catalog.shapes:
        templates = ", ".join(template.key for template in sorted(catalog.templates_for_shape(shape), key=lambda item: item.key))
        print(f"  {shape}: {templates}")
    print("\n模块:")
    for module in sorted(catalog.modules.values(), key=lambda item: item.key):
        variants = ", ".join(variant.key for variant in module.variants)
        print(f"  {module.key}: {variants}")
    return 0


def cmd_validate(args) -> int:
    catalog = load_catalog(args.data)
    errors, warnings = validate_catalog(catalog)
    for warning in warnings:
        print(f"警告: {warning}")
    if args.render_smoke and not errors:
        errors.extend(render_smoke(catalog, args.output))
    if errors:
        for error in errors:
            print(f"错误: {error}", file=sys.stderr)
        return 2
    print("校验通过")
    if args.render_smoke:
        print(f"冒烟输出: {args.output}")
    return 0


def cmd_list3d(args) -> int:
    catalog = load_catalog3d(args.data3d)
    print("3D 器形:")
    for shape in catalog.shapes:
        templates = ", ".join(template.key for template in sorted(catalog.templates_for_shape(shape), key=lambda item: item.key))
        print(f"  {shape}: {templates}")
    print("\n3D 模块:")
    for module in sorted(catalog.modules.values(), key=lambda item: item.key):
        variants = ", ".join(variant.key for variant in module.variants)
        print(f"  {module.key}: variants=[{variants}]")
    print("\n3D 颜色方案:")
    for scheme in sorted(catalog.color_schemes.values(), key=lambda item: item.key):
        print(f"  {scheme.key}: {', '.join(sorted(scheme.colors.keys()))}")
    return 0


def cmd_validate3d(args) -> int:
    catalog = load_catalog3d(args.data3d)
    errors, warnings = validate_catalog3d(catalog)
    for warning in warnings:
        print(f"警告: {warning}")
    if args.render_smoke and not errors:
        errors.extend(render_smoke3d(catalog, args.output))
    if errors:
        for error in errors:
            print(f"错误: {error}", file=sys.stderr)
        return 2
    print("3D 校验通过")
    if args.render_smoke:
        print(f"3D 冒烟输出: {args.output}")
    return 0


def cmd_compose(args) -> int:
    catalog = load_catalog(args.data)
    errors, warnings = validate_catalog(catalog)
    for warning in warnings:
        print(f"警告: {warning}")
    if errors:
        for error in errors:
            print(f"错误: {error}", file=sys.stderr)
        return 2

    composer = ArtifactComposer(catalog)
    templates = resolve_templates(args, catalog, composer)
    args.output.mkdir(parents=True, exist_ok=True)
    manifest: dict = {
        "size": catalog.canvas,
        "seed": args.seed,
        "finish": args.finish,
        "palette_colors": args.palette_colors,
        "items": []
    }

    for template, sample_index in templates:
        composed = composer.compose(template, args.seed, sample_index)
        suffix = f"{sample_index:02d}_{stable_int(f'{args.seed}|{template.key}|{sample_index}') & 0xffff:04x}"
        folder = args.output / template.shape
        stem = f"{template.key}_{suffix}"
        svg_path = folder / f"{stem}.svg"
        png_path = folder / f"{stem}.png"
        preview_path = folder / f"{stem}_preview.png"

        write_svg(composed, svg_path, catalog.canvas)
        image = render_png(
            composed,
            png_path,
            catalog.canvas,
            supersample=args.supersample,
            palette_colors=args.palette_colors,
            finish=args.finish,
        )
        save_preview(image, preview_path, args.preview_scale)
        manifest["items"].append(
            {
                "shape": template.shape,
                "artifact": template.artifact,
                "template": template.key,
                "sample": sample_index,
                "svg": str(svg_path.relative_to(args.output)).replace("\\", "/"),
                "png": str(png_path.relative_to(args.output)).replace("\\", "/"),
                "preview": str(preview_path.relative_to(args.output)).replace("\\", "/") if args.preview_scale > 1 else "",
                "variants": composed.variant_map,
            }
        )

    manifest_path = args.output / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"生成 {len(manifest['items'])} 个组合结果")
    print(f"输出目录: {args.output}")
    print(f"索引文件: {manifest_path}")
    return 0


def cmd_compose3d(args) -> int:
    catalog = load_catalog3d(args.data3d)
    errors, warnings = validate_catalog3d(catalog)
    for warning in warnings:
        print(f"警告: {warning}")
    if errors:
        for error in errors:
            print(f"错误: {error}", file=sys.stderr)
        return 2

    composer = ArtifactComposer3D(catalog)
    templates = resolve_templates3d(args, catalog, composer)
    args.output.mkdir(parents=True, exist_ok=True)
    manifest: dict = {
        "size": catalog.canvas,
        "seed": args.seed,
        "pipeline": "3d",
        "palette_colors": args.palette_colors,
        "items": []
    }

    for template, sample_index in templates:
        instance = composer.compose(template, args.seed, sample_index)
        suffix = f"{sample_index:02d}_{stable_int(f'{args.seed}|3d|{template.key}|{sample_index}') & 0xffff:04x}"
        folder = args.output / template.shape
        stem = f"{template.key}_{suffix}"
        png_path = folder / f"{stem}.png"
        preview_path = folder / f"{stem}_preview.png"
        obj_path = folder / f"{stem}.obj"
        image = render_png3d(instance, png_path, catalog.canvas, palette_colors=args.palette_colors)
        save_preview3d(image, preview_path, args.preview_scale)
        exported_obj = ""
        exported_mtl = ""
        if not args.no_obj:
            obj_file, mtl_file = write_obj3d(instance, obj_path)
            exported_obj = str(obj_file.relative_to(args.output)).replace("\\", "/")
            exported_mtl = str(mtl_file.relative_to(args.output)).replace("\\", "/")
        manifest["items"].append(
            {
                "shape": template.shape,
                "artifact": template.artifact,
                "template": template.key,
                "sample": sample_index,
                "instance": f"{template.key}:{sample_index:02d}",
                "png": str(png_path.relative_to(args.output)).replace("\\", "/"),
                "preview": str(preview_path.relative_to(args.output)).replace("\\", "/") if args.preview_scale > 1 else "",
                "obj": exported_obj,
                "mtl": exported_mtl,
                "variants": instance.variant_map,
                "colors": instance.color_map,
            }
        )

    manifest_path = args.output / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"生成 {len(manifest['items'])} 个 3D 组合结果")
    print(f"3D 输出目录: {args.output}")
    print(f"3D 索引文件: {manifest_path}")
    return 0


def resolve_templates(args, catalog, composer: ArtifactComposer):
    if args.template:
        if args.template not in catalog.templates:
            raise SystemExit(f"模板不存在: {args.template}")
        return [(catalog.templates[args.template], i) for i in range(args.count)]

    if args.all:
        shapes = catalog.shapes
    else:
        shapes = args.shape or []
    if not shapes:
        raise SystemExit("需要指定 --all、--shape 或 --template")

    result = []
    for shape in shapes:
        for i in range(args.count):
            result.append((composer.pick_template(shape, args.seed, i), i))
    return result


def resolve_templates3d(args, catalog, composer: ArtifactComposer3D):
    if args.template:
        if args.template not in catalog.templates:
            raise SystemExit(f"3D 模板不存在: {args.template}")
        return [(catalog.templates[args.template], i) for i in range(args.count)]
    if args.all:
        shapes = catalog.shapes
    else:
        shapes = args.shape or []
    if not shapes:
        raise SystemExit("需要指定 --all、--shape 或 --template")
    result = []
    for shape in shapes:
        for i in range(args.count):
            result.append((composer.pick_template(shape, args.seed, i), i))
    return result


if __name__ == "__main__":
    raise SystemExit(main())
