#!/usr/bin/env python3
"""9.13 灵藤：在独立游戏中用原已学入口验证生成、实际接触和退场。"""
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中通过 dev.py exec 执行')
    import baseline_scenarios as baseline
    folder = Path(tempfile.mkdtemp(prefix='wood-growth-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    directory = Path(__file__).parent
    helpers = (directory / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    world = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{world}")', json.dumps(str(PROJECT / world)))
    helpers = helpers.replace('fact.AbilityId.StartsWith("test.body-footwork.")', 'fact.AbilityId == SkillEntities.SpiritVine.id')
    helpers = helpers.replace('contact.AbilityId.StartsWith("test.body-footwork.")', 'contact.AbilityId == SkillEntities.SpiritVine.id')
    imports = ('using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Blueprints;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Content.Components.Skill;\n'
               'using Friflo.Engine.ECS;\n')
    script = folder / 'input.script'
    script.write_text(imports + helpers + Path(__file__).with_suffix('.script').read_text())
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'灵藤复验未完成：{folder}')
    print(json.dumps(dict(result, output=str(folder)), ensure_ascii=False))


if __name__ == '__main__':
    main()
