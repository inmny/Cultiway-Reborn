#!/usr/bin/env python3
"""复用同组八类投送和现有连续录像，交付原版及Worldfall画面。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode

CASES = dict(contact='真实接触掌击', flight='离体飞行', ground='沿地厚浪', fall='真实底部下落',
             ray='有限立体射线', orbit='来源周围环绕', follow='真实来源及挂点附着', direct='直接指定位置')


def compare():
    """同种投送逐段并排，较早结束的一侧只保持最终帧，不改变实际运动速度。"""
    root = PROJECT / 'artifacts/skill_rebuild/delivery/films'
    output = root / 'comparison'
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    for case in CASES:
        paths = [root / mode / (case + '.mp4') for mode in ('native', 'worldfall')]
        durations = []
        for mode, path in zip(('native', 'worldfall'), paths):
            if not json.loads((root / mode / case / 'result.json').read_text())['passed']:
                raise RuntimeError('实际场景未通过：' + str(path))
            probe = subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'json', str(path)],
                capture_output=True, text=True, check=True)
            durations.append(float(json.loads(probe.stdout)['format']['duration']))
        duration = max(durations)
        filters = ';'.join(f'[{i}:v]setpts=PTS-STARTPTS,tpad=stop_mode=clone:stop_duration={duration - seconds:.6f}[v{i}]'
            for i, seconds in enumerate(durations)) + ';[v0][v1]hstack=inputs=2[v]'
        movie = output / (case + '.mp4')
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', str(paths[0]), '-i', str(paths[1]),
            '-filter_complex', filters, '-map', '[v]', '-t', str(duration), '-r', '24', '-c:v', 'libx264',
            '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
        rows.append(dict(case=case, movie=str(movie), sourceDurations=durations, duration=duration))
    (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' + ''.join(f"file '{case}.mp4'\n" for case in CASES))
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-safe', '0', '-f', 'concat', '-i', 'movies.ffconcat',
        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(output / 'manifest.json', dict(passed=True, cases=rows, timeBasis='各段按同次施放开始并排；只补结束后的静止帧，不变速'))
    print(output / 'all.mp4', flush=True)


def main():
    """指定一段或录制整批；只编码时不重新运行已通过场景。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--case', nargs='+', choices=list(CASES))
    parser.add_argument('--encode-only', action='store_true')
    parser.add_argument('--compare-only', action='store_true', help='只合成已有双端影片，不启动游戏')
    args = parser.parse_args()
    if args.compare_only:
        compare()
        return
    mode = 'worldfall' if args.worldfall else 'native'
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else
                         'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    output = PROJECT / 'artifacts/skill_rebuild/delivery/films' / mode
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    connected = None if args.encode_only else baseline.connect(True, validate_runtime=False)
    if connected:
        started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
        if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
            raise RuntimeError('当前游戏未加载本轮代码，请先正常退出这一独立进程')
    manifest = output / ('manifest' + ('-' + '-'.join(args.case) if args.case else '') + '.json')
    write_json(manifest, dict(passed=False, view=mode, status='running'))
    for case in args.case or CASES:
        folder = output / case
        folder.mkdir(exist_ok=True)
        if not args.encode_only:
            shutil.rmtree(folder / 'frames', ignore_errors=True)
            (folder / 'frames').mkdir()
            helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
            helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
            helpers = helpers.replace('    Config.paused = false;\n    double until',
                '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
            helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
            helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
            fragment = Path(__file__).with_suffix('.script').read_text().replace('DELIVERY_CASE', case)
            script = folder / 'input.script'
            script.write_text(helpers + '\n' + Path(__file__).with_name('delivery_cases.script').read_text() + '\n' + fragment)
            write_json(folder / 'metadata.json', dict(connection=connected, view=mode, case=case, version=version_stamp()[0]))
            write_json(folder / 'result.json', dict(passed=False, status='running'))
            try:
                result = baseline.call('run', str(script), '180000')
            except RuntimeError as error:
                (folder / 'error.json').write_text(str(error))
                raise RuntimeError('投送连续画面未完成，保留原进程和现场：' + str(folder / 'error.json')) from None
            if result.get('view') != mode:
                raise RuntimeError('当前实际显示与请求不符：' + str(result))
            write_json(folder / 'result.json', result)
            (folder / 'error.json').unlink(missing_ok=True)
        result = json.loads((folder / 'result.json').read_text())
        if not result['passed']:
            raise RuntimeError('实际场景未通过，不能编码交付：' + str(folder))
        rows.append(encode(case, folder, output, not args.worldfall, CASES[case], '5.04 运动与投送', crop_worldfall=case == 'contact'))
    if not args.case:
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' + ''.join(f"file '{case}.mp4'\n" for case in CASES))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-safe', '0', '-f', 'concat', '-i', 'movies.ffconcat',
                        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(manifest, dict(passed=True, view=mode, cases=rows,
        timeBasis='按真实世界时间编码；取景等待不计运动时长，功能画面不替代后续美术验收'))


if __name__ == '__main__':
    main()
