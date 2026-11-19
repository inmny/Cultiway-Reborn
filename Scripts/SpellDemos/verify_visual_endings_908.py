#!/usr/bin/env python3
"""复验主体失去作用、移动目标和残留去向。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('motion', 'block', 'interrupt', 'material'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix=f'visual-endings-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    read = lambda name: (Path(__file__).parent / name).read_text()
    helper = read('verify_body_footwork.script').split('var bindingType =', 1)[0]
    helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helper = helper.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                            json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helper = helper.replace('test.body-footwork.', 'test.visual-ending.')
    if args.section == 'material':
        basis = PROJECT / 'Doc/技能系统全面重构/构造输入/流水凝固留置.json'
        (folder / 'material-basis.json').write_bytes(basis.read_bytes())
    script = folder / 'input.script'
    script.write_text(helper + read('verify_appearance_camera.script') + read('verify_visual_cleanup_camera.script') +
                      read('verify_visual_endings_case.script') + read(f'verify_visual_endings_{args.section}.script'))
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
