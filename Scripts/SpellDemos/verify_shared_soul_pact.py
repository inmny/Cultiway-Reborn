#!/usr/bin/env python3
"""实测多名承受者共同替负与正式跨进程恢复。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('cast', 'cold'), default='cast')
    parser.add_argument('--archive', type=Path, help='cast 段的输出目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')
    if (args.section == 'cold') != bool(args.archive):
        parser.error('仅 cold 需要 --archive')

    folder = Path(tempfile.mkdtemp(prefix=f'shared-pact-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    if args.archive:
        archive = args.archive.resolve()
        original = archive / 'archive'
        expected = json.loads((archive / 'result.json').read_text())
        with (original / 'map.wbox').open('rb') as save:
            digest = hashlib.file_digest(save, 'sha256').hexdigest()
        if not expected['passed'] or digest != (archive / 'archive.sha256').read_text().strip():
            parser.error('正式存档校验未通过')
        world = folder / 'input-world'
        shutil.copytree(original, world)
        helpers = world_helpers(folder, world, restore=True).split('for (int x = 60; x <= 75; x++)')[0]
        prelude = f'JObject expected = JObject.Parse({json.dumps(json.dumps(expected))});\n'
        body = 'verify_shared_soul_pact_cold.script'
    else:
        definitions = PROJECT / 'Doc/技能系统全面重构/构造输入'
        shutil.copy2(definitions / '同袍替负.json', folder / 'definition.json')
        shutil.copy2(definitions / '魂魄替负.json', folder / 'legacy.json')
        helpers = world_helpers(folder, PROJECT / FIXTURE).replace(
            'fact.AbilityId.StartsWith("test.body-footwork.")', 'fact.SourceId > 0')
        prelude = ''
        body = 'verify_shared_soul_pact.script'
    script = folder / 'input.script'
    script.write_text('using UnityEngine.UI;\nusing Cultiway.UI;\n' + helpers + '\n'
                      + prelude + (scripts / body).read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '135000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'共同替负复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'共同替负复验未通过，见 {folder}')
    if args.section == 'cast':
        with (folder / 'archive/map.wbox').open('rb') as save:
            (folder / 'archive.sha256').write_text(hashlib.file_digest(save, 'sha256').hexdigest() + '\n')
    print(folder)


if __name__ == '__main__':
    main()
