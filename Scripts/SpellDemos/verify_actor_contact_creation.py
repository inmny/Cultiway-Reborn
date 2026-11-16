#!/usr/bin/env python3
"""5.05 C10：人物接触组规则、命中收势编辑及新旧请求。"""
import argparse
import json
import os
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'ui', 'cast', 'cast-tail', 'creation', 'save'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再验证')
    base = PROJECT / 'artifacts/skill_rebuild/trajectories/actor-contact-creation'
    for part in (('ui', 'cast', 'creation', 'save') if section == 'all' else (section,)):
        folder = base / part
        folder.mkdir(parents=True, exist_ok=True)
        if (folder / 'result.json').exists() and not json.loads((folder / 'result.json').read_text()).get('passed'):
            failures = base / 'failures'
            failures.mkdir(exist_ok=True)
            index = 1
            while (failures / f'{part}-{index}').exists():
                index += 1
            shutil.copytree(folder, failures / f'{part}-{index}')
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
        helpers = helpers.replace('test.body-footwork.', 'test.actor-contact-creation.')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.actor-contact-creation."))', '')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.actor-contact-creation.")) contacts.Add(contact);', 'contacts.Add(contact);')
        if part == 'cast-tail':
            functions = helpers.split('async Task Fire(', 1)[1]
            place = 'void Place(' + helpers.split('void Place(', 1)[1].split('foreach (Actor a in World.world', 1)[0]
            helpers = helpers.split('Game.Resume();', 1)[0] + '\nvar hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold");\n' + place + 'async Task Fire(' + functions
        helpers += """
object[] ContactRows(IEnumerable<ConstructedContact> values) => values.Select(hit => (object)new
{
    hit.CastId, hit.AbilityId, hit.SourceId, hit.TargetId, hit.Node, hit.Age,
    hit.TargetContactNumber, hit.GroupContactNumber, hit.DamageAttempted, hit.Result,
    position = new[] { hit.Position.x, hit.Position.y, hit.Position.z }
}).ToArray();
"""
        body = Path(__file__).with_name('verify_actor_contact_creation_' + part + '.script').read_text()
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Persistence;\nusing UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n' + helpers + '\n' + body)
        write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
        write_json(folder / 'result.json', dict(passed=False, status='running'))
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('人物接触规则验证未通过：' + str(folder / 'checks.json'))
        print(json.dumps(dict(section=part, result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
