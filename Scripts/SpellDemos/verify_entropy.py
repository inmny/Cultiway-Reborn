#!/usr/bin/env python3
"""9.24：复验旧侵蚀场、真实部位解构和两种常速视角。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('legacy', 'clash'), required=True)
    parser.add_argument('--record', choices=('native', 'worldfall'))
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过 develop 的 dev.py exec 运行混沌解构复验')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'entropy-{args.section}-{args.record or "check"}-',
                                  dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(output))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    shared = (scripts / 'verify_wind_combat.script').read_text()
    shared = shared.replace('const string inputRoot = "";', 'const string inputRoot = ' +
                            json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    shared = shared.replace('const string recordView = "";', 'const string recordView = ' + json.dumps(args.record or '') + ';')
    shared = shared.replace('Cultiway.Verify.Wind.Mana', 'Cultiway.Verify.Entropy.Mana')
    shared = shared.replace('if (fact.SourceId != source.id) return;', 'if (fact.SourceId != source.id && fact.SourceId != victim.id) return;')
    imports = ('using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Settings;\n'
               'using Cultiway.Core.SkillLibV3.Blueprints;\nusing Cultiway.Content.Components.Skill;\n'
               'using Friflo.Engine.ECS;\n')
    script = output / 'input.script'
    script.write_text(imports + helpers + '\n' + shared + '\n' + (scripts / f'verify_entropy_{args.section}.script').read_text())
    write_json(output / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('混沌复验失败，请查看 ' + str(output / 'checks.json'))
    if args.record:
        encode(output.parent, dict(key=output.name, name='散相解构' if args.section == 'clash' else '旧侵蚀场',
                                   label='原速', variant=0, trajectory='', modifiers=[]), keep_frames=True)
    print(json.dumps(dict(section=args.section, output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
