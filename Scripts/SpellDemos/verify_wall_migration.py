#!/usr/bin/env python3
"""5.05 B21至B24：验证原实体墙迁移及与旧法术、原版弹丸、人物运动的实际交互。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """复用已有世界、人物准备和调试通道，分别保存本批输入及实际结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('earth', 'solid-walls', 'contact-walls', 'shields'), default='earth',
                        help='土墙完整交互、实体墙、接触墙或随身护盾')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
    folder = PROJECT / 'artifacts/skill_rebuild/trajectories' / ('wall-migration' if section == 'earth' else section)
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    imports = ('using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Settings;\nusing Cultiway.Core.SkillLibV3.Blueprints;\n'
               'using Cultiway.Content.Components.Skill;\nusing Cultiway.Core.EventSystem.Events;\n'
               'using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\n')
    motion = Path(__file__).with_name('verify_body_footwork.script').read_text().split('AbilityDefinition Motion(', 1)[1].split('var movementType =', 1)[0]
    helpers += '\nAbilityDefinition Motion(' + motion
    script = folder / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text()
    if section in ('solid-walls', 'contact-walls', 'shields'):
        shared = body.split('var activeCasts', 1)[0].replace('SkillEntities.EarthWall', 'wallAsset')
        shared = shared.replace('camera.orthographicSize=9', 'camera.orthographicSize=12')
        shared = shared.replace('new Vector3(68,74,', 'new Vector3(68,72,')
        if section == 'shields':
            helpers = helpers.replace('x <= 75', 'x <= 85')
            shared = shared.replace('new ActiveAbilityTarget(target,target.GetSimPos(),selectionArea:new SkillTargetSelectionArea(true,target.GetSimPos(),.1f),worldScope:scope)',
                                    'new ActiveAbilityTarget(source,source.GetSimPos(),worldScope:scope)')
        if section == 'contact-walls':
            shared = shared.replace('Entity Build(bool boost=false,bool extra=false)',
                                    'Entity Build(bool boost=false,bool extra=false,bool contactEnabled=true)')
            shared = shared.replace('string signature=SkillContainerSignature.Build(e);',
                                    'if(!contactEnabled)e.AddComponent(new SkillSettings{AreaMultiplier=1,PowerMultiplier=1,ContactEnabled=false});string signature=SkillContainerSignature.Build(e);')
        body = ('SkillEntityAsset wallAsset=SkillEntities.IceWall;\n' + shared +
                Path(__file__).with_name('verify_' + section.replace('-', '_') + '.script').read_text())
    script.write_text(imports + helpers + '\n' + body)
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    result = baseline.call('run', str(script), '180000')
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('原墙接入未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
