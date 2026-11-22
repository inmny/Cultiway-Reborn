#!/usr/bin/env python3
"""用原葫芦的实际吞摄入库复验五灵倾泻。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    view = parser.add_mutually_exclusive_group()
    view.add_argument('--skip-views', action='store_true', help='只检查正式行为、形体与作用范围')
    view.add_argument('--views-only', action='store_true', help='通过同一原葫芦补采双视角及扇面边界')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix='gourd-outpouring-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    program = output / 'input.script'
    program.write_text(IMPORTS + world_helpers(output, PROJECT / FIXTURE)
                       + (SCRIPTS / 'weapon_flight_scene.script').read_text()
                       + (SCRIPTS / 'verify_artifact_views.script').read_text()
                       + (SCRIPTS / 'verify_artifact_model_anchor.script').read_text()
                       + (SCRIPTS / 'verify_gourd_scene.script').read_text()
                       + f'const bool shots = {str(not args.skip_views).lower()};\n'
                       + f'const bool viewsOnly = {str(args.views_only).lower()};\n'
                       + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(program), '170000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'五灵倾泻复验记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'五灵倾泻复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
