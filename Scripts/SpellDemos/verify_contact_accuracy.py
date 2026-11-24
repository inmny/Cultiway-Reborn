#!/usr/bin/env python3
"""6.19：按实际尺寸核对扇形误差与共用调试轮廓。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('accuracy', 'outline', 'isolated', 'water'), required=True)
    parser.add_argument('--output', type=Path, help='结果目录')
    args = parser.parse_args()
    section = args.section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    folder = args.output.resolve() if args.output else Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'contact-accuracy' / section
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    setup = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    setup = setup.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                          json.dumps(str(folder)))
    setup = setup.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    setup = setup.replace('test.body-footwork.', 'test.contact-accuracy.')
    camera = (scripts / 'verify_visual_boundary.script').read_text().split('\ntry\n', 1)[0]
    camera = camera.replace('test.visual-boundary.', 'test.contact-accuracy.')
    regions = (scripts / 'verify_part_contact_regions.script').read_text().split('var generation', 1)[1].split('Actor source =', 1)[0]
    script = folder / 'input.script'
    script.write_text(setup + camera + '\nvar generation' + regions + f'\nstring section = "{section}";\n' +
                      (scripts / 'verify_contact_accuracy.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '155000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('形状精度复验未通过：' + str(folder))
    print(folder)


if __name__ == '__main__':
    main()
