#!/usr/bin/env python3
"""5.05 C2：自主飞行创作、实际运动及正式新旧请求存读。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """复用独立安装、现成世界及共同人物辅助，不扩大身体装备样本。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("all", "tail", "requests", "save", "cast", "flow"), default="all")
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再验证')
    folder = PROJECT / 'artifacts/skill_rebuild/trajectories/flight-creation'
    if section != 'all':
        folder = folder / section
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('test.body-footwork.', 'test.flight-creation.')
    script = folder / 'input.script'
    contents = Path(__file__).with_suffix('.script').read_text()
    if section == 'tail':
        begin = contents.index('var waveDefinition =')
        end = contents.index('ConstructionOperation outward =')
        contents = contents[:begin] + 'representatives.Add("wave", Definition(wave)); representatives.Add("arc", Definition(arc));\n' + contents[end:]
    if section in ('requests', 'save'):
        begin = contents.index('var pathMethod =')
        end = contents.index('// 名称、条件和输出依赖')
        contents = contents[:begin] + 'var representatives = new Dictionary<string, AbilityDefinition>();\n' + contents[end:]
    if section == 'save':
        begin = contents.index('// 名称、条件和输出依赖')
        end = contents.index('int selectedSeed =')
        contents = contents[:begin] + contents[end:]
    if section == 'cast':
        begin = contents.index('    Check(inspiration.Rules')
        end = contents.index('    owner.GetCultisys<Xian>().wakan = source.stats')
        setup = '''    int selectedSeed = 9; long authorId = source.id;
    var representatives = new Dictionary<string, AbilityDefinition>();
    string created = ConstructedAbilityService.Create(owner, selectedSeed);
    var learned = access.Read(created);
    Check(learned.Body == null && Nodes(learned.Root).Any(node => node.Operations.Any(operation => operation.Path != null)), "实际创造并掌握可支付的完整曲线构造");
'''
        contents = contents[:begin] + setup + contents[end:]
    if section == 'flow':
        contents = contents[:contents.index('try\n{')] + Path(__file__).with_name('verify_flight_creation_flow.script').read_text()
    script.write_text('using Cultiway.Core.Persistence;\n' + helpers + '\n' + contents)
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('自主飞行创作未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
