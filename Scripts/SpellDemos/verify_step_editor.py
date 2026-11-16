#!/usr/bin/env python3
"""5.05 C1：命名步骤与同部位混合操作的真实编辑及保存。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """复用现有世界和调试通道，不重开身体装备样本。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'runtime', 'capture'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
    folder = PROJECT / 'artifacts/skill_rebuild/trajectories/step-editor'
    if section != 'all':
        folder = folder / section
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('test.body-footwork.', 'test.step-editor.')
    imports = 'using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n'
    script = folder / 'input.script'
    body = Path(__file__).with_suffix('.script') if section == 'all' else Path(__file__).with_name('verify_step_editor_runtime.script')
    contents = body.read_text()
    if section == 'capture':
        contents = contents.split('// UI_CAPTURE_BEGIN', 1)[1].split('// UI_CAPTURE_END', 1)[0]
        setup = ('Actor source = Make(64); source.GetExtend().AddComponent(new Xian { level = 2, wakan = 10000 });\n'
                 'var definition = JsonConvert.DeserializeObject<AbilityDefinition>(File.ReadAllText(Path.Combine(root, "artifacts/skill_rebuild/trajectories/step-editor/saved-definition.json")));\n'
                 'ConstructedAbilityService.Register(definition); ConstructedAbilityService.Learn(source.GetExtend(), definition.Id);\n')
        contents = setup + 'try {\n' + contents + '\nreturn new { passed = true };\n} finally { ConstructedAbilityService.ContactConfirmed -= Hit; ConstructedAbilityService.CastEnded -= Ended; Config.paused = true; ControllableUnit.clear(false); }'
    script.write_text(imports + helpers + '\n' + contents)
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('步骤编辑验证未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
