#!/usr/bin/env python3
"""在实际游戏复验 6.17 的指定送达、结果接续与存读。"""
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'direct-signal'
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    setup = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    setup = setup.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                          'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "direct-signal")')
    setup = setup.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    setup = setup.replace('test.body-footwork.', 'test.direct-signal.')
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Core.Components;\n' + setup + '\n' +
                      (scripts / 'verify_direct_signal.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '150000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('直接传讯复验未通过：' + str(folder))
    print('指定送达：' + str(folder))


if __name__ == '__main__':
    main()
