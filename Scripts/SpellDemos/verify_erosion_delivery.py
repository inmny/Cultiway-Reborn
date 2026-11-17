#!/usr/bin/env python3
"""5.13 C3a复用真实交付世界，两次跨进程集中恢复。"""
import argparse
import json
import os
from pathlib import Path
import shutil
from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('resume', 'completed'), required=True)
    parser.add_argument('--family', choices=('requests', 'materials', 'equipment', 'inventory', 'all'), default='all')
    parser.add_argument('--evidence', type=Path, default=Path.home() / '.local/state/cultiway-dev/evidence')
    parser.add_argument('--state', type=Path, help='completed读取前一次erosion-cold/resume目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    if args.section == 'completed' and not args.state:
        parser.error('completed需要--state指定上次完成世界')
    import baseline_scenarios as baseline
    connected = baseline.connect(False, validate_runtime=False)
    sources = {
        'requests': args.evidence / 'erosion-creation/runs/20261117T164637.349221Z-6368e674/output/erosion-creation/save',
        'materials': args.evidence / 'erosion-partial/run/output/partial-erosion/save',
        'equipment': args.evidence / 'erosion-equipment/run/output/equipment-erosion/save',
        'inventory': args.evidence / 'aging-inventory/runs/20261117T154814.948426Z-6a0a5344/output/inventory-aging/save',
    }
    for family in tuple(sources) if args.family == 'all' else (args.family,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'erosion-cold' / args.section / family
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        source = sources[family] if args.section == 'resume' else args.state / family
        result = json.loads((source / 'result.json').read_text())
        if not result.get('passed'):
            raise RuntimeError('需要已通过的真实世界 ' + str(source))
        shutil.copytree(source, folder / 'input-state', dirs_exist_ok=True)
        write_json(folder / 'origin.json', dict(family=family, source=str(source), process=result['process']))
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "erosion-cold/' + args.section + '/' + family + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        inventory = Path(__file__).with_name('inventory_case.script').read_text()
        body = Path(__file__).with_name('verify_erosion_delivery_' + args.section + '.script').read_text()
        script = folder / 'input.script'
        script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\nusing Cultiway.Core.Persistence;\n' + helpers + '\n' + inventory + '\nconst string family = "' + family + '";\n' + Path(__file__).with_name('erosion_delivery_case.script').read_text() + '\n' + body)
        write_json(folder / 'connection.json', connected)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('侵蚀老化冷启动未通过 ' + str(folder))
        print(json.dumps(dict(family=family, **result), ensure_ascii=False))


if __name__ == '__main__':
    main()
