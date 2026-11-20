#!/usr/bin/env python3
"""在独立游戏里复验剑冢开天的部件、实际作用与连贯收尾。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("all", "finish", "visual", "fracture", "blocked", "interrupted", "save", "native", "worldfall"), default="all")
    section = parser.parse_args().section
    view = section if section in ('native', 'worldfall') else ''
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'sword-grave-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    documents = PROJECT / 'Doc/技能系统全面重构/构造输入'
    shutil.copy2(documents / '剑冢开天.json', folder / 'definition.json')
    if section == 'save':
        shutil.copy2(documents / '剑冢开天·逆锋.json', folder / 'variant.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.SwordGrave", StringComparison.Ordinal)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    script = folder / 'input.script'
    body = (scripts / 'verify_sword_grave.script').read_text()
    setup, verify = body.split('// 实机入口。', 1)
    fracture = (scripts / 'verify_sword_grave_fracture.script').read_text()
    camera = (scripts / 'verify_wind_combat.script').read_text().split('object cameraSettings', 1)[1]
    camera = 'object cameraSettings' + camera
    camera = camera[:camera.index('void ReleaseChecks()')] + camera[camera.index('async Task SetCamera('):]
    delivery = (scripts / 'verify_sword_grave_delivery.script').read_text()
    body = setup + fracture + f'\nconst string recordView = {json.dumps(view)};\n' + camera + delivery + verify
    script.write_text('using Cultiway.Const;\n' + helpers + f'\nconst string section = {json.dumps(section)};\n' +
                      body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'剑冢开天 {section} 未通过：{folder}')
    if view:
        encode(folder.parent, dict(key=folder.name, name='剑冢开天：起兵、合刃、重斩与落片', label='原速',
                                   variant=0, trajectory='', modifiers=[], caption='外刃在实际接触后带斩势飞出'), keep_frames=True)
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
