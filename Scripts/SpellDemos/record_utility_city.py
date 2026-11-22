#!/usr/bin/env python3
"""冷读同一实际城市任务并录制正常世界时钟下的继续工作。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import write_json
from record_body_connections import encode
from verify_weapon_flight_archive import world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', required=True, type=Path, help='城市工程已通过的输出目录')
    parser.add_argument('--view', choices=('native', 'worldfall'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过对应的独立游戏 dev.py exec 运行')

    import baseline_scenarios as baseline

    work = args.work.resolve()
    receipt = json.loads((work / 'result.json').read_text())
    archive = work / 'work-save'
    with (archive / 'map.wbox').open('rb') as save:
        digest = hashlib.file_digest(save, 'sha256').hexdigest()
    if not receipt['passed'] or digest != (work / 'work-save.sha256').read_text().strip():
        parser.error('须使用已验证的实际城市工程档')

    output = Path(tempfile.mkdtemp(prefix=f'utility-city-film-{args.view}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    input_world = output / 'input-world'
    shutil.copytree(archive, input_world)
    helpers = world_helpers(output, input_world, restore=True)
    start = helpers.index('for (int x = 60;')
    helpers = helpers[:start] + helpers[helpers.index('var holdStats =', start):]
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    setup = f'\nstring viewRequested = "{args.view}";\n'
    setup += f'JObject saved = JObject.Parse(File.ReadAllText({json.dumps(str(work / "result.json"))}));\n'
    camera = Path(__file__).with_name('record_independent_casting_camera.script').read_text()
    script = output / 'input.script'
    script.write_text('using Cultiway.Core.CollectiveProjects;\n' + helpers + setup + camera +
                      Path(__file__).with_suffix('.script').read_text())

    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '225000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'城市冷读录像失败，见 {output}') from None
    result['sha256'] = digest
    write_json(output / 'result.json', result)
    if not result.get('passed') or result['view'] != args.view:
        raise SystemExit(f'城市冷读录像未通过，见 {output}')
    encode('city-utility-' + args.view, output, output, args.view == 'native',
           label='城市净土工程', milestone='10.13 实机记录', crop_worldfall=False)
    print(output)


if __name__ == '__main__':
    main()
