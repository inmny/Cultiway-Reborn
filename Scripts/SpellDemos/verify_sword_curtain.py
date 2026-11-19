#!/usr/bin/env python3
"""9.12：正式剑幕的聚拢、拦截、阵外通过及退场。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    """复用现有世界、原已学入口和真实运行数据。"""
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中通过 dev.py exec 执行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("all", "contact", "exit"), default="all")
    args = parser.parse_args()
    import baseline_scenarios as baseline
    folder = Path(tempfile.mkdtemp(prefix='sword-curtain-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    directory = Path(__file__).parent
    helpers = (directory / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    world = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{world}")', json.dumps(str(PROJECT / world)))
    wall = (directory / 'verify_wall_migration.script').read_text().split('/// <summary>只保存本批主体画面。', 1)[0]
    wall = wall.replace('SkillEntities.EarthWall', 'SkillEntities.SwordCurtain')
    imports = ('using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Blueprints;\nusing Cultiway.Core.SkillLibV3.Settings;\n'
               'using Cultiway.Content.Components.Skill;\nusing Cultiway.Core.EventSystem.Events;\n'
               'using Friflo.Engine.ECS;\n')
    script = folder / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text()
    body = body.replace("/* pose */ true", str(args.section == "all").lower(), 1)
    body = body.replace("/* contact */ true", str(args.section in ("all", "contact")).lower(), 1)
    script.write_text(imports + helpers + wall + body)
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'剑幕复验未完成：{folder}')
    print(json.dumps(dict(result, output=str(folder)), ensure_ascii=False))


if __name__ == '__main__':
    main()
