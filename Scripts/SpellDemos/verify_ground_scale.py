#!/usr/bin/env python3
"""复用现成城市与平坦世界，检查4.32B2的两种规模和实际显示。"""
import argparse
import json
import os
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """指定一类或依次运行，两种显示使用同一冻结场景输入。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--film', action='store_true', help='只附加本项连续画面，不重跑已通过的额外交锋回归')
    parser.add_argument('--case', nargs='+', choices=['city', 'world'])
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else
                         'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏尚未加载本轮代码，请先正常退出该进程')
    for case in args.case or ['city', 'world']:
        folder = PROJECT / ('artifacts/skill_rebuild/ground-scale-films' if args.film else 'artifacts/skill_rebuild/ground-scale') / mode / case
        folder.mkdir(parents=True, exist_ok=True)
        save = 'artifacts/skill_rebuild/performance-baseline/cultiway/worlds/none'
        if not (PROJECT / save / 'map.wbox').is_file():
            raise RuntimeError('缺少现成演示世界：' + save)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
        helpers = helpers.replace('Scripts/WorldSaveTests/Fixtures/world-format-1', save)
        begin = helpers.index('for (int x = 60; x <= 75; x++)')
        end = helpers.index('var holdStats', begin)
        helpers = helpers[:begin] + helpers[end:]
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        fragment = Path(__file__).with_suffix('.script').read_text().replace('GROUND_SCALE_CASE', case)
        if args.film:
            helpers = helpers.replace('ControllableUnit.clear(false);\n    ControllableUnit.setControllableCreature(source);',
                'if (!ControllableUnit.isControllingUnit(source)) { ControllableUnit.clear(false); ControllableUnit.setControllableCreature(source); }')
            shutil.rmtree(folder / 'frames', ignore_errors=True)
            (folder / 'frames').mkdir()
            helpers = helpers.replace('    Config.paused = false;\n    double until',
                '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
            helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
            # 变量必须先于复用的推进函数声明；录像函数在原相机、场景与事件之后定义。
            helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
            film = Path(__file__).with_name('record_ground_scale.script').read_text().replace('bool filmCapturing = false, filmStop', 'bool filmStop')
            fragment = fragment.replace('ConstructedAbilityService.CastStarted += Started;', film + '\nConstructedAbilityService.CastStarted += Started;', 1)
            fragment = fragment.replace('await Advance(.15f); await Shot("before");',
                'await Advance(.15f); Ready(source); await Shot("before", true);\n    CaptureGroundFrame(); filmRecording = RecordGroundFrames();')
            # 后续检查保留同一全景，近景到全景的切换由连续取样在0.5秒完成。
            fragment = fragment.replace('await To(.25f); await Shot("source", true);', 'await To(.25f);')
            fragment = fragment.replace('if (cityCase && mod == null)', 'if (false && cityCase && mod == null)')
            fragment = fragment.replace('    File.WriteAllText(Path.Combine(folder, "definition.json"),',
                '    await To(6.3f); filmStop = true; if (filmRecording != null) await filmRecording;\n    Check(filmTimes.Count >= 60 && filmFrames.Count > 0 && finishes.Count == 1, "连续画面覆盖真实起势、传播与结束");\n    SaveGroundFilm();\n    File.WriteAllText(Path.Combine(folder, "definition.json"),', 1)
            fragment = fragment.replace('finally\n{\n    ConstructedAbilityService.CastStarted -= Started;',
                'finally\n{\n    filmStop = true;\n    try { if (filmRecording != null) await filmRecording; }\n    finally\n    {\n    SaveGroundFilm();\n    if (filmTimeMode.HasValue) filmSettings.GetType().GetField("TimeMode", F).SetValue(filmSettings, filmTimeMode.Value);\n    ConstructedAbilityService.CastStarted -= Started;', 1)
            fragment += '\n}\n'
            fragment = fragment.replace('GROUND_SCALE_CASE', case)
        script = folder / 'input.script'
        script.write_text(helpers + '\n' + fragment)
        write_json(folder / 'metadata.json', dict(connection=connected, view=mode, case=case, save=save, version=version_stamp()[0]))
        write_json(folder / 'result.json', dict(passed=False, status='running'))
        try:
            result = baseline.call('run', str(script), '600000' if args.film else '180000')
        except RuntimeError as error:
            (folder / 'error.json').write_text(str(error))
            write_json(folder / 'result.json', dict(passed=False))
            raise RuntimeError('规模检查未完成，保留同一进程和现场，见 ' + str(folder / 'error.json')) from None
        if result.get('view') != mode:
            result.update(passed=False, error='实际显示与请求不符：' + str(result.get('view')))
            write_json(folder / 'result.json', result)
            raise RuntimeError(result['error'])
        write_json(folder / 'result.json', result)
        (folder / 'error.json').unlink(missing_ok=True)
        print(json.dumps(dict(view=mode, case=case, result=result), ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
