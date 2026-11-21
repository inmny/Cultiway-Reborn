#!/usr/bin/env python3
"""录制正式御器、飞剑和分光剑阵，从起势一直到全部归还。"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers

MOVES = {
    'arrow-rain': '高弧箭雨',
    'sky-volley': '天空齐射',
    'sweep': '御剑横扫',
    'thrust': '御枪突刺',
    'crush': '御锤重砸',
    'flying-sword': '原物飞剑',
    'sword-array': '分光剑阵',
}


def movies(folder, view, completed, output):
    output.mkdir(parents=True, exist_ok=True)
    entries = []
    for key in MOVES:
        if key in completed:
            entries.append(encode(key, folder / key, output, view == 'native',
                                  label=MOVES[key], milestone='御器、飞剑与投射物',
                                  crop_worldfall=key not in ('arrow-rain', 'sky-volley', 'sword-array'),
                                  worldfall_crop='crop=iw/2:ih/2:iw*0.32:ih/4'
                                  if key in ('thrust', 'flying-sword') else None))
        elif (output / f'{key}.json').is_file():
            entry = json.loads((output / f'{key}.json').read_text())
            if entry['view'] != view or not Path(entry['movie']).is_file():
                raise RuntimeError(f'输出目录已有其他视角或缺少视频：{key}')
            entries.append(entry)
    if len(entries) == len(MOVES):
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' +
                                                ''.join(f"file '{key}.mp4'\n" for key in MOVES))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', 'movies.ffconcat',
                        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(output / 'manifest.json', dict(view=view, complete=len(entries) == len(MOVES), cases=entries,
               timeBasis='实际世界时间', source=str(folder)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--view', choices=('native', 'worldfall'), required=True)
    parser.add_argument('--move', action='append', choices=MOVES)
    parser.add_argument('--output', type=Path, help='分段验证后可汇总到同一输出目录')
    parser.add_argument('--encode-only', type=Path, help='重新编码已拍到的连续画面')
    args = parser.parse_args()
    if args.encode_only:
        folder = args.encode_only.resolve()
        completed = [key for key in args.move or MOVES if (folder / key / 'result.json').exists()]
    else:
        if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
            parser.error('请通过独立游戏的 dev.py exec 录制')
        import baseline_scenarios as baseline

        folder = Path(tempfile.mkdtemp(prefix=f'weapon-flight-film-{args.view}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
        connection = baseline.connect(False, validate_runtime=False)
        installed = baseline.call('eval', 'return System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(), '
                                  'a => a.GetType("FirstPerson.WorldBoxMod", false) != null);')
        if installed != (args.view == 'worldfall'):
            parser.error('请选择与录像视角对应的独立游戏环境')
        version = version_stamp()[0]
        chosen = args.move or list(MOVES)
        settings = f'\nconst string view = "{args.view}";\n'
        settings += f'var selected = new string[] {{ {", ".join(map(json.dumps, chosen))} }};\n'
        for key in chosen:
            clip = folder / key
            (clip / 'frames').mkdir(parents=True)
            write_json(clip / 'metadata.json', dict(view=args.view, move=key, connection=connection, version=version))
        program = folder / 'input.script'
        program.write_text(IMPORTS + world_helpers(folder, PROJECT / FIXTURE)
                           + (SCRIPTS / 'weapon_flight_scene.script').read_text() + settings
                           + Path(__file__).with_suffix('.script').read_text())
        try:
            result = baseline.call('run', str(program), '570000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise RuntimeError(f'御器录像记录位于 {folder}') from error
        write_json(folder / 'result.json', result)
        completed = result['completed']
    output = args.output.resolve() if args.output else folder / 'videos'
    for key in completed:
        result = json.loads((folder / key / 'result.json').read_text())
        if not result['passed'] or result['view'] != args.view:
            raise RuntimeError(f'实机输入未通过或视角不匹配：{key}')
    movies(folder, args.view, completed, output)
    print(json.dumps(dict(output=str(output), source=str(folder), completed=completed), ensure_ascii=False))


if __name__ == '__main__':
    main()
