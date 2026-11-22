#!/usr/bin/env python3
"""实测构造术的感知、真显影与范围传讯。"""
import argparse
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('cast', 'editor'), default='cast')
    section = parser.parse_args().section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    folder = Path(tempfile.mkdtemp(prefix=f'perception-relay-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    definitions = PROJECT / 'Doc/技能系统全面重构/构造输入'
    for name, target in [('巡影传讯', 'definition'), ('照影传讯', 'legacy')]:
        shutil.copy2(definitions / f'{name}.json', folder / f'{target}.json')

    helpers = world_helpers(folder, PROJECT / FIXTURE).replace('test.body-footwork.', 'Cultiway.Demo.PatrolSignal')
    script = folder / 'input.script'
    body = Path(__file__).with_name('verify_perception_relay_editor.script' if section == 'editor'
                                   else 'verify_perception_relay.script')
    imports = ('using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n' if section == 'editor'
               else 'using Cultiway.Core.Combat.Tactical;\n')
    script.write_text(imports + helpers + '\n' + body.read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '120000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'感知传讯复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'感知传讯复验未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
