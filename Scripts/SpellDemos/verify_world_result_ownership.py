#!/usr/bin/env python3
"""用现成正式档核对器口原件、阵门与节点、防火条件的重复恢复。"""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, write_json
from verify_skill_archive_fixtures import sections
from verify_weapon_flight_archive import IMPORTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', required=True, choices=('weapon', 'facility', 'condition', 'switch'))
    parser.add_argument('--archive', required=True, type=Path, help='包含 archive/map.wbox 和 expected.json 的原测试输出')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请在独立游戏中使用 dev.py exec')
    import baseline_scenarios as baseline

    archive = args.archive.resolve()
    out = Path(tempfile.mkdtemp(prefix=f'world-result-{args.case}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = out / 'input-world'
    shutil.copytree(archive / 'archive', world)
    expected = out / 'expected.json'
    shutil.copy2(archive / 'expected.json', expected)
    raw = (archive / 'archive/map.wbox').read_bytes()
    helpers = world_helpers(out, world, restore=True)
    helpers = helpers[:helpers.index('for (int x = 60; x <= 75; x++)')]
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    script = out / 'input.script'
    body = Path(__file__).with_suffix('.script')
    if args.case == 'switch':
        other = PROJECT / 'Scripts/WorldSaveTests/Fixtures/skills-rebuild-current/map.wbox'
        other_raw = other.read_bytes()
        other_copy = out / 'other/map.wbox'
        other_copy.parent.mkdir()
        shutil.copy2(other, other_copy)
        body = body.with_name('verify_world_condition_switch.script')
    script.write_text('using Cultiway;\nusing Cultiway.Core.Pathfinding;\n' + IMPORTS
                      + helpers + f'const string scenario = "{args.case}";\n'
                      + body.read_text())
    write_json(out / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '100000')
        if not result.get('passed'):
            raise RuntimeError('重复读档检查未通过')
        if raw != (archive / 'archive/map.wbox').read_bytes() or raw != (world / 'map.wbox').read_bytes():
            raise RuntimeError('原档或输入副本发生改写')
        if args.case == 'switch':
            if other.read_bytes() != other_raw or other_copy.read_bytes() != other_raw:
                raise RuntimeError('另一个世界的原档或输入副本发生改写')
            for file in (out / 'other-saved/map.wbox', out / 'source-ended/map.wbox'):
                if 'constructed_world_conditions' in sections(file):
                    raise RuntimeError('另一世界或来源已销毁时，条件不应再写入正式文件')
            result['otherSha256'] = hashlib.sha256(other_raw).hexdigest()
    except RuntimeError as error:
        write_json(out / 'failure.json', {'passed': False, 'error': str(error)})
        raise
    result['originalSha256'] = hashlib.sha256(raw).hexdigest()
    write_json(out / 'result.json', result)
    print(out)


if __name__ == '__main__':
    main()
