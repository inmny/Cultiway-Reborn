#!/usr/bin/env python3
"""5.05 C6：附加伤害和接触分支真实编辑、自主创作及新旧等待请求存读。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'ui', 'ui-tail', 'cast', 'cast-tail', 'cast-conditions', 'cast-kill', 'creation', 'save'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再验证')
    base = PROJECT / 'artifacts/skill_rebuild/trajectories/contact-results'
    for part in (('ui', 'cast', 'creation', 'save') if section == 'all' else (section,)):
        folder = base / part
        folder.mkdir(parents=True, exist_ok=True)
        if (folder / 'result.json').exists():
            prior = json.loads((folder / 'result.json').read_text())
            if not prior.get('passed'):
                import shutil
                failures = base / 'failures'
                failures.mkdir(exist_ok=True)
                index = 1
                while (failures / f'{part}-{index}').exists():
                    index += 1
                shutil.copytree(folder, failures / f'{part}-{index}')
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
        helpers = helpers.replace('test.body-footwork.', 'test.contact-results.')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.contact-results.")) contacts.Add(contact);', 'contacts.Add(contact);')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.contact-results."))', '')
        body = Path(__file__).with_name('verify_contact_results_' + ('ui' if part == 'ui-tail' else 'cast' if part in ('cast-tail', 'cast-conditions', 'cast-kill') else part) + '.script').read_text()
        if part == 'ui-tail':
            start = body.index('var definition =')
            end = body.index('ConstructedAbilityService.Register', start)
            body = body[:start] + 'var definition = JsonConvert.DeserializeObject<AbilityDefinition>(File.ReadAllText(Path.Combine(root, "artifacts/skill_rebuild/trajectories/contact-results/ui/direct-definition.json")));\n' + body[end:]
            start = body.index('    Click("Page4"); ToggleValue("CustomImpact"')
            end = body.index('Action("RemoveContactDamage");', start)
            body = body[:start] + '    Click("Page4"); ' + body[end:]
        if part in ('cast-tail', 'cast-conditions', 'cast-kill'):
            start = body.index('    var direct = ReadCase("direct")')
            end = body.index('    var random = ReadCase("random")', start)
            body = body[:start] + body[end:]
            if part in ('cast-conditions', 'cast-kill'):
                start = body.index('    var random = ReadCase("random")')
                end = body.index('    var conditional = ReadCase("conditional")', start)
                body = body[:start] + '    var random = ReadCase("random");\n' + body[end:]
            if part == 'cast-kill':
                start = body.index('    var conditional = ReadCase("conditional")')
                end = body.index('    await Use(conditional,.001f)', start)
                body = body[:start] + '    var conditional = ReadCase("conditional");\n' + body[end:]
        imports = 'using Cultiway.Core.EventSystem.Events;\nusing UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing Cultiway.Core.Persistence;\nusing Cultiway.Core.Components;\n'
        script = folder / 'input.script'
        script.write_text(imports + helpers + '\n' + body)
        write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
        write_json(folder / 'result.json', dict(passed=False, status='running'))
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('接触结果验证未通过：' + str(folder / 'checks.json'))
        print(json.dumps(dict(section=part, result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
