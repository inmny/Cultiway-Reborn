#!/usr/bin/env python3
"""5.13 C2侵蚀老化独立创作、原术衍生及新旧正式请求。"""
import argparse
import json
import os
from pathlib import Path
import shutil
from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('creation', 'independent', 'runtime', 'save', 'all'), default='all')
    parser.add_argument('--basis', type=Path, help='creation读取C1已通过窗口保存的正式成品')
    parser.add_argument('--creation', type=Path, help='复用已经通过的creation输入')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for section in ('creation', 'independent', 'runtime', 'save') if options.section == 'all' else (options.section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'erosion-creation' / section
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        shutil.copy2(Path(__file__).parent / 'fixtures/erosion-creation-rule27.json', folder / 'rule27.json')
        if section == 'creation':
            if not options.basis:
                parser.error('creation需要--basis指定C1已通过的真实窗口成品')
            shutil.copy2(options.basis, folder / 'input-definition.json')
        if section in ('runtime', 'save'):
            creation = options.creation or folder.parent / 'creation'
            if not json.loads((creation / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已通过的创作输入')
            shutil.copytree(creation, folder / 'input-creation', dirs_exist_ok=True)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "erosion-creation/' + section + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        common = Path(__file__).with_name('ending_case.script').read_text()
        shared = common[common.index('IEnumerable<ConstructionNode> Nodes('):]
        body = Path(__file__).with_name('verify_erosion_creation_' + section + '.script').read_text()
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Persistence;\n' + helpers + '\n' + shared + '\n' + Path(__file__).with_name('erosion_creation_case.script').read_text() + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('侵蚀老化创作复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
