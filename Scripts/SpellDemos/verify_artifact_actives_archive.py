#!/usr/bin/env python3
"""分两次游戏进程验证法宝正式存档中的部署、连珠和穿戴活动。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('prepare', 'cold'), required=True)
    parser.add_argument('--case', choices=('actives', 'structure'), default='actives')
    parser.add_argument('--archive', type=Path, help='prepare 输出的目录')
    args = parser.parse_args()
    if (args.section == 'cold') != bool(args.archive):
        parser.error('仅冷启动阶段需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'artifact-{args.case}-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    source = args.archive.resolve() if args.archive else output
    restoring = args.section == 'cold'
    world = source / 'archive' if restoring else PROJECT / FIXTURE
    settings = ('var saved = JObject.Parse(File.ReadAllText(' + json.dumps(str(source / 'expected.json')) + '));\n'
                if restoring else 'JObject saved = null;\n')
    if restoring:
        receipt = json.loads((source / 'result.json').read_text())
        if not receipt['passed'] or hashlib.sha256((world / 'map.wbox').read_bytes()).hexdigest() != receipt['archiveSha256']:
            parser.error('冷启动需要 prepare 验证通过并写出的原档')
    program = output / 'input.script'
    body = Path(__file__).with_suffix('.script') if args.case == 'actives' else SCRIPTS / 'verify_artifact_structure_archive.script'
    anchors = ''.join((SCRIPTS / name).read_text() for name in (
        'verify_artifact_views.script', 'verify_artifact_model_anchor.script')) if args.case == 'structure' else ''
    program.write_text(IMPORTS + world_helpers(output, world, restore=restoring)
                       + (SCRIPTS / 'weapon_flight_scene.script').read_text() + anchors
                       + settings + body.read_text())
    try:
        result = baseline.call('run', str(program), '150000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'法宝正式存档复验记录位于 {output}') from error
    result['archiveSha256'] = hashlib.sha256(((source / 'archive') / 'map.wbox').read_bytes()).hexdigest()
    write_json(output / 'result.json', result)
    if not result['passed'] or (restoring and result['archiveSha256'] != receipt['archiveSha256']):
        raise SystemExit(f'法宝正式存档复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
