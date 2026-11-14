#!/usr/bin/env python3
"""复用4.32B2的城域和全域输入，录制足底发力到传播结束的连续画面。"""
import argparse
import json
from pathlib import Path
import subprocess
import sys

from common import PROJECT, write_json
from record_body_connections import encode

CASES = {'city': '城域地脉传力', 'world': '全域地脉传力'}


def main():
    """指定同一场景录制或只编码已有帧，不扩展身体装备样本。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--native', action='store_true')
    parser.add_argument('--case', nargs='+', choices=list(CASES))
    parser.add_argument('--encode-only', action='store_true')
    args = parser.parse_args()
    mode = 'native' if args.native else 'worldfall'
    output = PROJECT / 'artifacts/skill_rebuild/ground-scale-films' / mode
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    manifest = output / ('manifest' + ('-' + '-'.join(args.case) if args.case else '') + '.json')
    write_json(manifest, dict(passed=False, status='running', view=mode))
    for case in args.case or CASES:
        folder = output / case
        if not args.encode_only:
            command = [sys.executable, str(Path(__file__).with_name('verify_ground_scale.py')), '--film', '--case', case]
            if not args.native:
                command.append('--worldfall')
            with (output / (case + '-run.log')).open('w') as log:
                process = subprocess.run(command, cwd=PROJECT, stdout=log, stderr=subprocess.STDOUT)
            if process.returncode:
                raise RuntimeError('同一场景录制未完成，见 ' + str(output / (case + '-run.log')))
        result = json.loads((folder / 'result.json').read_text())
        data = json.loads((folder / 'recording.json').read_text())
        if not result['passed'] or result['view'] != mode or data['view'] != mode:
            raise RuntimeError('实际运行或视图不符，不能作为交付：' + str(folder))
        rows.append(encode(case, folder, output, args.native, CASES[case], '4.32 宏观接触发力', crop_worldfall=False))
    if not args.case:
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' + ''.join(f"file '{case}.mp4'\n" for case in CASES))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-safe', '0', '-f', 'concat', '-i', 'movies.ffconcat',
                        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(manifest, dict(passed=True, view=mode, cases=rows,
        timeBasis='按真实世界时间编码；取景等待不计入运动时长，功能影片不替代最终美术或性能验收'))


if __name__ == '__main__':
    main()
