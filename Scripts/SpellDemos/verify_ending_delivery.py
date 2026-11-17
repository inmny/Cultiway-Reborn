#!/usr/bin/env python3
"""5.11 C3集中跨进程恢复，直接复用各批实际正式世界。"""
import argparse
import json
import os
from pathlib import Path
import shutil
from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('pending', 'inventory', 'permanent', 'completed'), required=True)
    parser.add_argument('--family', choices=('ending', 'inventory', 'material', 'all'), default='all')
    parser.add_argument('--evidence', type=Path, default=Path.home() / '.local/state/cultiway-dev/evidence')
    parser.add_argument('--state', type=Path, help='completed读取上次新进程的ending-cold输出')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    families = dict(ending=options.evidence / 'ending-creation/output/save',
                    inventory=options.evidence / 'inventory-creation/output/inventory-creation/save',
                    material=options.evidence / 'material-creation/output/material-creation/save')
    names = tuple(families) if options.section == 'pending' and options.family == 'all' else (options.family,) if options.section == 'pending' else (options.section,)
    for name in names:
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'ending-cold' / options.section / name
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        if options.section == 'pending':
            source = families[name]
            shutil.copytree(source / 'pending-save', folder / 'save', dirs_exist_ok=True)
            shutil.copy2(source / 'pending-expected.json', folder / 'expected.json')
        if options.section == 'inventory':
            source = options.evidence / 'inventory-inputs/output/inventory/save'
            shutil.copytree(source / 'borrowed-save', folder / 'save', dirs_exist_ok=True)
            shutil.copy2(source / 'expected.json', folder / 'expected.json')
        if options.section == 'permanent':
            source = options.evidence / 'material-transfer/runs/20261117T103126.799499Z-71099590/output/material-transfer/runtime'
            shutil.copytree(source / 'successful-save', folder / 'save', dirs_exist_ok=True)
            shutil.copy2(source / 'saved-expected.json', folder / 'expected.json')
        if options.section == 'completed':
            if not options.state:
                parser.error('completed需要--state指定上次真正新进程的输出')
            shutil.copytree(options.state, folder / 'input-state', dirs_exist_ok=True)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "ending-cold/' + options.section + '/' + name + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        common = Path(__file__).with_name('ending_case.script').read_text()
        shared = common[common.index('IEnumerable<ConstructionNode> Nodes('):]
        if options.section == 'inventory':
            shared += '\n' + Path(__file__).with_name('inventory_case.script').read_text()
        body = Path(__file__).with_name('verify_ending_delivery_' + options.section + '.script').read_text()
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Components;\nusing Cultiway.Utils;\nusing Friflo.Engine.ECS;\nusing Cultiway.Core.Persistence;\n' + helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('结束去向冷启动复验未通过 ' + str(folder))
        print(json.dumps(dict(name=name, **result), ensure_ascii=False))


if __name__ == '__main__':
    main()
