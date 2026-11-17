#!/usr/bin/env python3
"""5.05 C16：只验证新增辅助轨迹、领域涡旋和灵藤显现的真实入口。"""
import argparse
import json
import os
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """复用已有世界、人物准备和调试通道，分别保存本批输入及实际结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'continuation', 'vine'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False) if section != 'continuation' else {'reuse': True}
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
    base = PROJECT / 'artifacts/skill_rebuild/trajectories/trajectory-edges'
    folder = base if section == 'all' else base / section
    if (folder / 'result.json').exists() and not json.loads((folder / 'result.json').read_text()).get('passed'):
        failures = folder.with_name('trajectory-edges-failures')
        failures.mkdir(exist_ok=True)
        index = 1
        while (failures / str(index)).exists():
            index += 1
        shutil.copytree(folder, failures / str(index))
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    imports = ('using Cultiway.Core.SkillLibV3.Editor;\n'
               'using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Settings;\nusing Cultiway.Core.SkillLibV3.Blueprints;\n'
               'using Cultiway.Content.Components.Skill;\nusing Cultiway.Core.EventSystem.Events;\n'
               'using Friflo.Engine.ECS;\n')
    body = Path(__file__).with_suffix('.script').read_text()
    if section == 'vine':
        body = (body.split('    var healing=Build(', 1)[0] +
                '    int hp,paid;\n    current=SkillEntities.SpiritVine;' +
                body.split('    current=SkillEntities.SpiritVine;', 1)[1])
    if section == 'continuation':
        place = helpers.split('void Place(', 1)[1].split('foreach (Actor a', 1)[0]
        hit_ready = helpers.split('void Hit(', 1)[1]
        helpers = (helpers.split('Game.Resume();', 1)[0] +
                   'var hold=Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold");\n' +
                   'void Place(' + place + 'void Hit(' + hit_ready)
        body = body[body.index('IReadOnlyList<Entity> Learned()'):]
        people_file = json.dumps(str(base / 'people.json'))
        body = ('var people=JObject.Parse(File.ReadAllText(' + people_file + '));\n'
                'if((int)people["process"]!=System.Diagnostics.Process.GetCurrentProcess().Id) throw new InvalidOperationException("续接必须使用原游戏进程");\n'
                'Actor source=World.world.units.get((long)people["source"]),enemy=World.world.units.get((long)people["enemy"]),friend=World.world.units.get((long)people["friend"]),along=World.world.units.get((long)people["along"]);\n'
                'var owner=source.GetExtend();\n' + body)
        body = (body.split('    var healing=Build(', 1)[0] +
                '    var healing=Learned().Single(s=>s.GetComponent<SkillContainer>().Asset==SkillEntities.HealingLight);float mana;int friendHp;\n'
                '    Reset(friend);mana=' + body.split('    Reset(friend);mana=', 1)[1])
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
        raise RuntimeError('新接入轨迹实机未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
