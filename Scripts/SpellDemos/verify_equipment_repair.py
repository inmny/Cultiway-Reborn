#!/usr/bin/env python3
"""5.17 C：原装备修复、真实材料扣除与正式存读。"""
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'equipment-repair'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "equipment-repair")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.equipment-repair.')
    inventory = (scripts / 'inventory_case.script').read_text()
    material = 'Entity Material(' + inventory.split('Entity Material(', 1)[1].split('JObject Original(', 1)[0]
    identity = 'string Stable(' + inventory.split('string Stable(', 1)[1].split('object Running(', 1)[0]
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\n' +
                      helpers + '\n' + material + identity + (scripts / 'verify_equipment_repair.script').read_text())
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'装备修复复验未通过：{folder}')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
