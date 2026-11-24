#!/usr/bin/env python3
"""从实际游戏取得共用形体清单，复验换材、布局、轨迹和视距。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, file_sha256, write_json


def release_bundles(root):
    """核对三套发布包的文件头，并为实机结果记录来源。"""
    bundles = root / 'Content/AssetBundles'
    records = {}
    for kind in ('forms', 'shaders'):
        for platform in ('linux', 'windows', 'osx'):
            name = f'cultiway_{kind}_{platform}'
            path = bundles / name
            if not path.is_file():
                raise ValueError(f'缺少发布资源包：{path}')
            with path.open('rb') as stream:
                if stream.read(8) != b'UnityFS\0' or path.stat().st_size <= 8:
                    raise ValueError(f'发布资源包文件头无效：{path}')
            records[name] = dict(size=path.stat().st_size, sha256=file_sha256(path))
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('catalog', 'surfaces', 'layouts', 'scale-meshes', 'scale-frames', 'scale-worldfall'), required=True)
    parser.add_argument('--forms', help='取景编号，逗号分隔；省略时按结构分组取全部可见形体')
    parser.add_argument('--full-reference', action='store_true', help='给指定原版取景补拍同距离的完整细节对照')
    parser.add_argument('--output', type=Path, help='结果目录')
    args = parser.parse_args()
    if args.full_reference and (args.section != 'scale-frames' or not args.forms):
        parser.error('--full-reference 只用于明确指定的原版形体')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline

    folder = args.output.resolve() if args.output else Path(tempfile.mkdtemp(
        prefix=f'form-combinations-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    folder.mkdir(parents=True, exist_ok=True)
    read = lambda name: (Path(__file__).parent / name).read_text()
    if args.section == 'catalog':
        write_json(folder / 'bundles.json', release_bundles(PROJECT))
        write_json(folder / 'ids.json', sorted(path.stem for path in (PROJECT / 'Content/Forms').glob('*.json')))
        header = read('verify_body_footwork.script').split('const BindingFlags F', 1)[0]
        header += f'\nstring folder = {json.dumps(str(folder))};\n'
    elif args.section == 'scale-meshes':
        header = read('verify_form_detail.script').split('long[] total =', 1)[0]
        header = header.replace('Path.Combine(root, "artifacts/skill_rebuild/form-detail")', json.dumps(str(folder)))
    else:
        header = read('verify_body_footwork.script').split('var bindingType =', 1)[0]
        header = header.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
        header = header.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                                json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
        header = header.replace('test.body-footwork.', 'test.form-combination.')
        header += read('verify_appearance_camera.script') + read('verify_visual_cleanup_camera.script')
    if args.section in ('scale-frames', 'scale-worldfall'):
        if args.forms:
            ids = args.forms.split(',')
        elif args.section == 'scale-worldfall':
            ids = ('tidal_wave water_flow water_wave warhammer ritual_mirror spirit_colossus wood_wall water_whale '
                   'sword fireball ice_lance wind_vortex earth_ridge lightning_strike poison_mist yang_bolt '
                   'yin_blade chaos_orb').split()
        else:
            descriptions = [(path.stem, json.loads(path.read_text())['Structure']['Kind'])
                            for path in sorted((PROJECT / 'Content/Forms').glob('*.json'))
                            if path.stem not in ('pivot', 'world_domain')]
            ids = [key for key, _ in sorted(descriptions, key=lambda pair: pair[::-1])] + ['fire_body']
        if not ids or len(ids) != len(set(ids)):
            parser.error('取景编号必须互不重复')
        stride = 1 if args.section == 'scale-worldfall' else 3
        write_json(folder / 'frames-input.json', [ids[start:start + stride] for start in range(0, len(ids), stride)])
    script = folder / 'input.script'
    if args.section == 'scale-frames':
        header += '\nconst bool fullReference = %s;\n' % str(args.full_reference).lower()
    script.write_text(header + read(f'verify_form_combinations_{args.section}.script'))
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    print(folder, flush=True)
    try:
        result = baseline.call('run', str(script), '190000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise RuntimeError(f'检查未通过，现场位于 {folder}')


if __name__ == '__main__':
    main()
