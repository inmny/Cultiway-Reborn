#!/usr/bin/env python3
"""从实际游戏取得共用形体清单，复验换材后的制作层次。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('catalog', 'surfaces'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix=f'form-combinations-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    read = lambda name: (Path(__file__).parent / name).read_text()
    if args.section == 'catalog':
        write_json(folder / 'ids.json', sorted(path.stem for path in (PROJECT / 'Content/Forms').glob('*.json')))
        header = read('verify_body_footwork.script').split('const BindingFlags F', 1)[0]
        header += f'\nstring folder = {json.dumps(str(folder))};\n'
    else:
        header = read('verify_body_footwork.script').split('var bindingType =', 1)[0]
        header = header.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
        header = header.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                                json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
        header = header.replace('test.body-footwork.', 'test.form-combination.')
        header += read('verify_appearance_camera.script') + read('verify_visual_cleanup_camera.script')
    script = folder / 'input.script'
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
