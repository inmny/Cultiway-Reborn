#!/usr/bin/env python3
"""用正式世界存读复验法宝护盾、器灵复苏与宗门供奉。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


def archive_hash(directory):
    return hashlib.sha256((directory / 'archive/map.wbox').read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', required=True, choices=('prepare', 'cold'))
    parser.add_argument('--archive', type=Path, help='prepare 输出的目录')
    args = parser.parse_args()
    cold = args.section == 'cold'
    if cold != bool(args.archive):
        parser.error('仅冷启动阶段需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'artifact-passives-archive-{args.section}-',
                                   dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    origin = args.archive.resolve() if cold else output
    if cold:
        receipt = json.loads((origin / 'result.json').read_text())
        if not receipt['passed'] or receipt['archiveSha256'] != archive_hash(origin):
            parser.error('冷启动需要 prepare 写出的完整原档')
        settings = f'JObject saved = JObject.Parse(File.ReadAllText({json.dumps(str(origin / "expected.json"))}));\n'
    else:
        settings = 'JObject saved = null;\n'
    world = origin / 'archive' if cold else PROJECT / FIXTURE
    helpers = world_helpers(output, world, restore=cold)
    if cold:
        # 读取原档时保留原地表和殿前地块。
        begin = helpers.index('for (int x = 60; x <= 75; x++)')
        helpers = helpers[:begin] + helpers[helpers.index('var holdStats', begin):]
    program = output / 'input.script'
    program.write_text('using Cultiway;\nusing Cultiway.Const;\nusing strings;\n'
                       + IMPORTS + helpers
                       + (SCRIPTS / 'weapon_flight_scene.script').read_text()
                       + (SCRIPTS / 'verify_artifact_views.script').read_text()
                       + f'string originalWorld = {json.dumps(str(PROJECT / FIXTURE))};\n'
                       + settings + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(program), '170000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'法宝被动正式存读记录位于 {output}') from error
    result['archiveSha256'] = archive_hash(origin)
    write_json(output / 'result.json', result)
    if not result['passed'] or (cold and result['archiveSha256'] != receipt['archiveSha256']):
        raise SystemExit(f'法宝被动正式存读未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
