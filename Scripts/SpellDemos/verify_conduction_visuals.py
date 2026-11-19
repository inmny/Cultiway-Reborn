#!/usr/bin/env python3
"""复验 6.12 每一波真实到达的水面、双视图与结束清理。"""
import argparse
import os
from pathlib import Path
import shutil

from common import PROJECT, encode, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('native', 'transferred'), required=True)
    parser.add_argument('--record', choices=('native', 'worldfall'), help='用同一条水道按原速录下放电与冻断')
    args = parser.parse_args()
    section, view = args.section, args.record
    if view and section != 'native':
        parser.error('水路录像复用 --section native')
    relative = f'conduction-film/route-{view}' if view else f'conduction-visuals/{section}'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    inputs = folder / 'inputs'
    inputs.mkdir(exist_ok=True)
    for name in ('循水连雷.json', '活动导电验证水流.json'):
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入' / name, inputs)
    scripts = Path(__file__).parent
    setup = (scripts / 'verify_conduction_pulses.script').read_text().split('var waves=new', 1)[0]
    setup = setup.replace('Path.Combine(root,"artifacts/skill_rebuild/conduction-pulses")',
                          f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    setup = setup.replace(f'Path.Combine(root,"{fixture}")', 'Path.Combine(folder,"input-world")')
    setup = setup.replace('Path.Combine(root,"Doc/技能系统全面重构/构造输入/"+name+".json")',
                          'Path.Combine(folder,"inputs",name+".json")')
    setup = setup.split('IList Casts()=', 1)[0]
    script = folder / 'input.script'
    recording_view = view or ''
    script.write_text('using Cultiway.Core.Visuals.Forms;\n' + setup +
                      f'\nstring section = "{section}", recordView = "{recording_view}";\n' +
                      (scripts / 'verify_conduction_visuals.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    game_log = folder.parents[2] / 'player.log'
    log_start = game_log.stat().st_size
    try:
        result = baseline.call('run', str(script), '160000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    with game_log.open('rb') as log:
        log.seek(log_start)
        result['retired_mesh_render_errors'] = log.read().count(b'Parameter name: mesh')
    write_json(folder / 'result.json', result)
    if not result.get('passed') or result['retired_mesh_render_errors']:
        raise RuntimeError('放电显示未通过：' + str(folder))
    if view:
        encode(folder.parent, dict(key=folder.name, name='循水连雷：完整与冻断', label='原速', variant=0,
                                  trajectory='', modifierLabels=[], modifiers=[]), keep_frames=True)
    print('实际水面放电：' + str(folder))


if __name__ == '__main__':
    main()
