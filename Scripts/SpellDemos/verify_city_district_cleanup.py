#!/usr/bin/env python3
"""真实城市分区净化的推进、断路、付费及正式读回。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import write_json
from verify_weapon_flight_archive import world_helpers
from world_progress_capture import encode_progress, progress_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True, help='11.03 已通过的城市用术存档准备目录')
    parser.add_argument('--record', action='store_true', help='录制原版常速进度')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    archive = args.archive.resolve()
    prepared = json.loads((archive / 'result.json').read_text())
    if not prepared['passed']:
        parser.error('城市档的准备段未通过')
    folder = Path(tempfile.mkdtemp(prefix='city-district-clean-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = folder / 'input-world'
    shutil.copytree(archive / 'archive', world)
    script = folder / 'input.script'
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Core.CollectiveProjects;\n'
               'using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\nusing Cultiway.Core.SkillLibV3.Utils;\n')
    script.write_text(imports + world_helpers(folder, world, restore=True)
                      + f'\nlong sourceId = {prepared["actor"]};\n'
                      + progress_helpers(args.record) + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'分区净化复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'分区净化复验未通过，见 {folder}')
    if args.record:
        encode_progress(folder, {'first': '城市分区净化 · 首轮与断路', 'resumed': '城市分区净化 · 正式读回与复工'}, result)
    print(folder)


if __name__ == '__main__':
    main()
