#!/usr/bin/env python3
"""从原版附体动作按钮入口复验站姿。"""
import argparse
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    checks = parser.add_mutually_exclusive_group()
    checks.add_argument('--observe', action='store_true', help='记录原行为，包括越过站姿的结果')
    checks.add_argument('--arm', action='store_true', help='单独检查只占手臂时仍可走动')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏 dev.py exec 运行')

    import baseline_scenarios as baseline

    output = Path(tempfile.mkdtemp(prefix='possession-stance-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    stance = Path(__file__).with_suffix('.script').read_text()
    script.write_text('using HarmonyLib;\n' + world_helpers(output, PROJECT / FIXTURE) +
                      f'\nbool observe = {str(args.observe).lower()};\n'
                      f'bool armOnly = {str(args.arm).lower()};\n' + stance)
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '65000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'原版附体复验失败，见 {output}') from None
    write_json(output / 'result.json', result)
    print(output)
    if not result['blocked'] and not args.observe:
        raise SystemExit('实际附体输入越过了共同站姿')


if __name__ == '__main__':
    main()
