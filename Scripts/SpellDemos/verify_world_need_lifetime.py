#!/usr/bin/env python3
"""在原版独立游戏复验日常回收需求消失、恢复及来源失效。"""
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    """复用地点用术的现成世界、人物和定义。"""
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 在独立游戏中运行')
    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix='world-need-lifetime-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = (scripts / 'verify_world_use.script').read_text().split('var temp=', 1)[0]
    helpers = helpers.replace('Path.Combine(root,"artifacts/skill_rebuild/world-use")', json.dumps(str(folder)))
    load_path = 'Path.Combine(root,"Scripts/WorldSaveTests/Fixtures/world-format-1")'
    helpers = helpers.replace('Config.paused=true;Game.Resume();', f'string fixture = {json.dumps(str(fixture))};\n'
                              'Config.paused=true;Game.Resume();')
    helpers = helpers.replace(load_path, 'fixture')
    helpers = helpers.replace('var other=Make(65);other.Base.setKingdom(owner.Base.kingdom);', '')
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + Path(__file__).with_suffix('.script').read_text())
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '135000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error),
                    sceneRestored=(folder / 'restore.json').is_file()))
        raise
    result['sceneRestored'] = (folder / 'restore.json').is_file()
    write_json(folder / 'result.json', result)
    if not result.get('passed') or not result['sceneRestored']:
        raise RuntimeError(f'日常回收未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
