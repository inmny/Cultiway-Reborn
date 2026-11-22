#!/usr/bin/env python3
"""复验一次性法术物品的实际受理、目标失效和拒绝恢复。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', required=True, choices=('scroll', 'rejection', 'prepare', 'cold'))
    parser.add_argument('--archive', type=Path, help='prepare 输出目录，仅冷启动使用')
    args = parser.parse_args()
    restoring = args.section == 'cold'
    if restoring != bool(args.archive):
        parser.error('仅 cold 阶段需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'consumable-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    program = output / 'input.script'
    archival = args.section in ('prepare', 'cold')
    prepared = args.archive.resolve() if restoring else output
    archive = prepared / 'archive'
    world = archive if restoring else PROJECT / FIXTURE
    if restoring:
        receipt = json.loads((prepared / 'result.json').read_text())
        if not receipt['passed'] or hashlib.sha256((archive / 'map.wbox').read_bytes()).hexdigest() != receipt['archiveSha256']:
            parser.error('冷启动需要原封不动的正式验收存档')
    support = ''
    name = Path(__file__).with_suffix('.script')
    if archival:
        support = (Path(__file__).with_name('weapon_flight_scene.script')).read_text()
        support += ('var saved = JObject.Parse(File.ReadAllText(' + json.dumps(str(prepared / 'expected.json')) + '));\n'
                    if restoring else 'JObject saved = null;\n')
        name = Path(__file__).with_name('verify_consumable_archive.script')
    program.write_text(IMPORTS + 'using Cultiway.Const;\nusing Cultiway.Utils;\nusing Cultiway.Core.SkillLibV3.Utils;\n'
                       + 'using Cultiway.Core.SkillLibV3.Modifiers;\nusing Cultiway.Content.Components.Skill;\n'
                       + 'using Cultiway.Content.Behaviours;\n'
                       + world_helpers(output, world, restore=restoring) + support
                       + ('' if archival else f'const string section = "{args.section}";\n') + name.read_text())
    try:
        result = baseline.call('run', str(program), '90000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'一次性法术物品复验见 {output}') from error
    if archival and result['passed']:
        result['archiveSha256'] = hashlib.sha256((archive / 'map.wbox').read_bytes()).hexdigest()
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'一次性法术物品复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
