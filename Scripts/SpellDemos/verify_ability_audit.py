#!/usr/bin/env python3
"""补查现有能力逐项验收中缺少实际结果的入口。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('sect',), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    output = Path(tempfile.mkdtemp(prefix=f'ability-audit-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    body = Path(__file__).with_name(f'verify_ability_audit_{args.section}.script').read_text()
    imports = (
        'using Cultiway;\nusing Cultiway.Content.Sects;\nusing Cultiway.Content.Const;\n'
        'using Cultiway.Core.Libraries;\nusing Cultiway.Core.Semantics;\n'
    )
    script.write_text(IMPORTS + imports + world_helpers(output, PROJECT / FIXTURE) +
                      '\ntry\n{\n' + body + '\n}\nfinally\n{\n'
                      '    ConstructedAbilityService.CastEnded -= Ended;\n'
                      '    ConstructedAbilityService.ContactConfirmed -= Hit;\n'
                      '    Config.paused = true;\n}\n')
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '95000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'实机记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
