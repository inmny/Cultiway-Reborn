#!/usr/bin/env python3
"""实机核对实际法印下落、触地范围、一次付费及中断归还。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sampling = parser.add_mutually_exclusive_group()
    sampling.add_argument('--skip-views', action='store_true', help='只核对实机行为与模型位置')
    sampling.add_argument('--views-only', action='store_true', help='只复验印身和震裂外缘的实机画面')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix='mountain-seal-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    script.write_text(IMPORTS + world_helpers(output, PROJECT / FIXTURE)
                      + (SCRIPTS / 'weapon_flight_scene.script').read_text()
                      + (SCRIPTS / 'verify_artifact_views.script').read_text()
                      + f'\nconst bool shots = {str(not args.skip_views).lower()};\n'
                      + f'const bool viewsOnly = {str(args.views_only).lower()};\n'
                      + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(script), '170000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'落印复验记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'落印复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
