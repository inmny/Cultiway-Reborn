#!/usr/bin/env python3
"""复用掌击、铁剑和已有世界，核对4.34的局部表面资料与真实画面。"""
import argparse
import json
import os
from pathlib import Path
from common import PROJECT, version_stamp, write_json


def main():
    """同一份输入用于两端，失败保留现有进程及实际现场。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--batch', action='store_true', help='只复用已有GPU小探针核对带反馈的骨架和透明合批')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else
                         'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connection = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('Source晚于当前游戏，请先退出该独立进程')
    folder = PROJECT / 'artifacts/skill_rebuild/body-surface-feedback' / mode
    if args.batch:
        folder = PROJECT / 'artifacts/skill_rebuild/body-surface-feedback/batch'
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bridge =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    shared = Path(__file__).with_name('verify_weapon_connections.script').read_text().split('\ntry\n{', 1)[0]
    shared = shared.replace('test.body-footwork.weapon-connection.', 'test.body-footwork.')
    script = folder / 'input.script'
    if args.batch:
        fragment = Path(__file__).with_name('verify_form_batch.script').read_text().split('// 实际场景只发布共同状态；', 1)[0]
        fragment = fragment.replace('artifacts/skill_rebuild/form-batch', str(folder.relative_to(PROJECT)))
        fragment = fragment.replace('return item; }', 'if (i % 2 == 0) { var point = matrix.MultiplyPoint3x4(new Vector3(.2f, -.1f, 0f)); Set(item, "Contact", new Vector4(point.x, point.y, point.z, .6f)); var build = (Vector4)Get(item, "Build"); build.w = .75f; Set(item, "Build", build); } return item; }', 1)
        positive = Path(__file__).with_name('verify_surface_feedback_pixels.script').read_text()
        fragment = fragment.replace('            File.WriteAllBytes(Path.Combine(folder,', positive + '\n            File.WriteAllBytes(Path.Combine(folder,', 1)
        # 只核对这次影响的骨架、表面和透明提交，不重复规模、场景和资源生命周期。
        begin = fragment.index('    var many = Enumerable.Range(0, 1100)')
        end = fragment.index('\n}', begin)
        fragment = fragment[:begin] + fragment[end:]
        fragment += '\nvar report = new { passed = checks.All(c => (bool)Get(c, "pass")), checks }; File.WriteAllText(Path.Combine(folder, "checks.json"), JsonConvert.SerializeObject(report, Formatting.Indented)); return new { report.passed, process = System.Diagnostics.Process.GetCurrentProcess().Id };\n'
        script.write_text(fragment)
    else:
        script.write_text(helpers + shared + '\n' + Path(__file__).with_suffix('.script').read_text())
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connection, view=mode))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        raise RuntimeError('表面反馈检查未完成，保留现场：' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('表面反馈结果未通过，请查看 ' + str(folder / 'checks.json'))
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
