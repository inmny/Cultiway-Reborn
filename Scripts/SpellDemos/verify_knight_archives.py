#!/usr/bin/env python3
"""复验迁移前保存的骑士学习资料，以及新战技等待档。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('section', choices=('prepare-legacy', 'restore-legacy', 'restore-study'))
    parser.add_argument('--archive', type=Path, help='前次生成的正式存档和预期目录')
    args = parser.parse_args()
    if (args.section != 'prepare-legacy') != (args.archive is not None):
        parser.error('读档阶段需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    archive = args.archive.resolve() if args.archive else None
    study = args.section == 'restore-study'
    record = 'pending-expected.json' if study else 'expected.json'
    world = archive / ('pending-save' if study else 'archive') if archive else PROJECT / fixture
    if archive and not ((world / 'map.wbox').is_file() and (archive / record).is_file()):
        parser.error(f'找不到正式世界与预期文件：{archive}')

    output = Path(tempfile.mkdtemp(prefix='knight-' + args.section + '-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(world)))
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Content.KnightCombat;\n'
               'using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\n')
    if archive:
        helpers = helpers[:helpers.index('foreach (Actor a in World.world.units.getSimpleList().ToArray())')] + helpers[helpers.index('Actor Make('):]
        settings = f'\nvar saved = JObject.Parse(File.ReadAllText({json.dumps(str(archive / record))}));\n'
        settings += f'const bool study = {str(study).lower()};\n'
        body = (scripts / 'verify_knight_archive_restore.script').read_text()
    else:
        settings = ''
        body = (scripts / 'verify_knight_archive_prepare.script').read_text()
    script = output / 'input.script'
    script.write_text(imports + helpers + settings + body)

    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'骑士世界复验记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'骑士世界复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
