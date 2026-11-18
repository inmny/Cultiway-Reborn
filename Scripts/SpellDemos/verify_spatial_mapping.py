#!/usr/bin/env python3
"""复验生成形体、生灵和原法宝的双门转移。"""
import argparse
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('core', 'runtime', 'all', 'actor', 'actor-clear', 'artifact', 'artifact-clear', 'capacity'), default='all')
    section = parser.parse_args().section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'spatial-mapping' / section
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "spatial-mapping", "' + section + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.spatial-mapping.')
    variant = section.split('-', 1)[0]
    name = f'verify_spatial_mapping_{variant}.script' if variant in ('actor', 'artifact', 'capacity') else 'verify_spatial_mapping.script'
    body = Path(__file__).with_name(name).read_text()
    if variant == 'artifact':
        helpers = 'using Friflo.Engine.ECS;\n' + helpers
    script = folder / 'input.script'
    script.write_text(helpers + '\nconst string section = "' + section + '";\n' + body)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'空间转移未通过：{folder}')
    print(result)


if __name__ == '__main__':
    main()
