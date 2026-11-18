#!/usr/bin/env python3
"""复验 6.16 的真实伤害来源、映像去重与水路停止。"""
import argparse
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('water', 'contact'), required=True)
    section = parser.parse_args().section
    relative = f'clash-termination/{section}'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    if section == 'water':
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/循水连雷.json', folder / '循水连雷.json')
        setup = (scripts / 'verify_conduction_pulses.script').read_text().split('var waves=new', 1)[0]
        setup = setup.replace('Path.Combine(root,"artifacts/skill_rebuild/conduction-pulses")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
        setup = setup.replace(f'Path.Combine(root,"{fixture}")', 'Path.Combine(folder,"input-world")')
        setup = setup.replace('Path.Combine(root,"Doc/技能系统全面重构/构造输入/"+name+".json")',
                              'Path.Combine(folder,name+".json")')
    else:
        setup = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        setup = setup.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
        setup = setup.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
        setup += (scripts / 'correspondence_case.script').read_text()
    header = 'using Cultiway.Core;\nusing Cultiway.Core.Combat;\nusing Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\n'
    script = folder / 'input.script'
    script.write_text(header + setup + '\n' + (scripts / 'clash_damage_observation.script').read_text() + '\n' +
                      (scripts / f'verify_clash_termination_{section}.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '150000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('交锋去重复验未通过：' + str(folder))
    print('交锋去重：' + str(folder))


if __name__ == '__main__':
    main()
