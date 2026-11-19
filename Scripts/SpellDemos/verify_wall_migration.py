#!/usr/bin/env python3
"""5.05 B21至B25：验证原实体墙迁移及与旧法术、原版弹丸、人物运动的实际交互。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, version_stamp, write_json


def main():
    """复用已有世界、人物准备和调试通道，分别保存本批输入及实际结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('earth', 'earth-appearance', 'earth-walk', 'earth-motion', 'solid-walls', 'contact-walls', 'shields', 'water-curtain'), default='earth',
                        help='土墙完整交互、土墙外观及通行边缘、只复验土墙步行、土墙收势、实体墙、接触墙、随身护盾或水幕')
    section = parser.parse_args().section
    development = os.environ.get('CULTIWAY_DEV_ACTIVE')
    if not development:
        if section in ('earth-appearance', 'earth-walk', 'earth-motion'):
            parser.error('外观复验请通过 develop 的 dev.py exec 运行')
        install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
        os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
        os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(not development, validate_runtime=False)
    if development:
        folder = Path(tempfile.mkdtemp(prefix=f'wall-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    else:
        started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
        if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
            raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
        folder = PROJECT / 'artifacts/skill_rebuild/trajectories' / ('wall-migration' if section == 'earth' else section)
        folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder if development else folder.relative_to(PROJECT)))
    if development:
        world = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
        if not (world / 'map.wbox').is_file():
            raise FileNotFoundError(world / 'map.wbox')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                                  json.dumps(str(world)))
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
    if section in ('earth-appearance', 'earth-walk', 'earth-motion'):
        helpers = helpers.replace('y <= 78', 'y <= 80')
        camera = Path(__file__).with_name('verify_appearance_camera.script').read_text()
        appearance = Path(__file__).with_name('verify_earth_wall_appearance.script').read_text()
        if section != 'earth-appearance':
            appearance = appearance.replace('const bool captureWall = true;', 'const bool captureWall = false;')
        if section == 'earth-motion':
            appearance = appearance.replace('const bool verifyMotion = false;', 'const bool verifyMotion = true;')
        body = body.split('var activeCasts', 1)[0] + camera + appearance
    elif section in ('solid-walls', 'contact-walls', 'shields', 'water-curtain'):
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
        if section in ('solid-walls', 'water-curtain'):
            sources = ('new[]{(Asset:SkillEntities.IceWall,Form:"ice_wall",Matter:"ice",State:"solid"),'
                       '(Asset:SkillEntities.WoodWall,Form:"wood_wall",Matter:"wood",State:"solid")}'
                       if section == 'solid-walls' else
                       'new[]{(Asset:SkillEntities.WaterCurtain,Form:"water_curtain",Matter:"water",State:"liquid")}')
            suffix = 'var wallSources=' + sources + ';\n' + Path(__file__).with_name('verify_passive_walls.script').read_text()
        else:
            suffix = Path(__file__).with_name('verify_' + section.replace('-', '_') + '.script').read_text()
        body = 'SkillEntityAsset wallAsset=SkillEntities.IceWall;\n' + shared + suffix
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
        raise RuntimeError('原墙接入未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(dict(result, output=str(folder)) if development else result, ensure_ascii=False))


if __name__ == '__main__':
    main()
