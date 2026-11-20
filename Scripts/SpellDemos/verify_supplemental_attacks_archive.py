#!/usr/bin/env python3
"""在新游戏进程读回原物、器官、铁甲及世界材料。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    kinds = ('weapon', 'poison', 'district', 'organs', 'seal', 'parts')
    for kind in kinds:
        parser.add_argument(f'--{kind}', type=Path, help='对应复验的输出目录')
    args = parser.parse_args()
    archives = {kind: getattr(args, kind).resolve() for kind in kinds if getattr(args, kind)}
    if not archives:
        parser.error('至少指定一份已经写好的正式档')
    for folder in archives.values():
        if not (folder / 'archive' / 'map.wbox').is_file() or not (folder / 'expected.json').is_file():
            parser.error(f'缺少正式世界或其记录：{folder}')
    if not os.getenv('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix='supplemental-attacks-cold-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('Game.Resume();', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    source = ('using Friflo.Engine.ECS;\nusing Cultiway.Content.Extensions;\n'
              'using Cultiway.Content.YaoBeasts;\nusing Cultiway.Content.Components;\n')
    entries = ', '.join(f'{{ "{kind}", {json.dumps(str(folder))} }}' for kind, folder in archives.items())
    path = output / 'input.script'
    path.write_text(source + helpers + f'\nvar archives = new Dictionary<string, string> {{ {entries} }};\n'
                    + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(path), '225000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'正式冷启动复验位于 {output}') from error
    write_json(output / 'result.json', result)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('正式读档未通过')


if __name__ == '__main__':
    main()
