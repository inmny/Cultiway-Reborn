#!/usr/bin/env python3
"""按正式法宝配方录制主动能力的真实起势、作用和收束。"""
import argparse
import csv
import json
import os
from pathlib import Path
import subprocess
import tempfile

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers

CATALOG = json.loads(Path(__file__).with_name('artifact_active_recordings.json').read_text())
MOVES = {key: batch for batch, recipe in CATALOG.items() for key in recipe['moves']}
PREFIX = 'Cultiway.ArtifactAbility.'


def encode_clips(folder, output, selected, view):
    output.mkdir(parents=True, exist_ok=True)
    with (PROJECT / 'Locales/ui.csv').open() as stream:
        names = {row[0][len(PREFIX):]: row[1] for row in csv.reader(stream)
                 if len(row) > 1 and row[0].startswith(PREFIX)}
    clips = []
    for key in MOVES:
        if key in selected:
            result = json.loads((folder / key / 'result.json').read_text())
            if not result['passed'] or result['view'] != view:
                raise RuntimeError('记录未完整通过或视角不同 ' + key)
            close = key in ('GoldenBellBarrier', 'HeavenlyConcealment', 'SoulCapturingStasis', 'MeridianSealing')
            clips.append(encode(key, folder / key, output, view == 'native', label=names[key],
                                milestone='法宝主动能力', crop_worldfall=close,
                                worldfall_crop='crop=iw/3:ih/3' if close else None))
        elif (output / f'{key}.json').is_file():
            previous = json.loads((output / f'{key}.json').read_text())
            if previous['view'] != view or not Path(previous['movie']).is_file():
                raise RuntimeError('已有记录的视角不同或缺少影片 ' + key)
            clips.append(previous)
    if len(clips) == len(MOVES):
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' +
                                                ''.join(f"file '{row['case']}.mp4'\n" for row in clips))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', 'movies.ffconcat',
                        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(output / 'manifest.json', dict(view=view, complete=len(clips) == len(MOVES),
               reused=['FlyingSwordAttack', 'SplittingSwordArray'], cases=clips, timeBasis='实际世界时间'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--view', choices=('native', 'worldfall'), required=True)
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument('--batch', action='append', choices=CATALOG)
    selection.add_argument('--ability', action='append', choices=MOVES)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--encode-only', type=Path)
    args = parser.parse_args()
    selected = args.ability or [key for key, batch in MOVES.items() if not args.batch or batch in args.batch]
    if args.encode_only:
        folder = args.encode_only.resolve()
        selected = [key for key in selected if (folder / key / 'result.json').is_file()]
    else:
        if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
            parser.error('请通过独立游戏的 dev.py exec 录制')
        import baseline_scenarios as baseline

        connection = baseline.connect(False, validate_runtime=False)
        folder = Path(tempfile.mkdtemp(prefix=f'artifact-actives-film-{args.view}-',
                                     dir=os.environ['CULTIWAY_DEV_OUTPUT']))
        version = version_stamp()[0]
        for key in selected:
            (folder / key / 'frames').mkdir(parents=True)
            write_json(folder / key / 'metadata.json', dict(view=args.view, move=key, connection=connection, version=version))
        cases = [dict(CATALOG[MOVES[key]], key=key) for key in selected]
        settings = ('const string view = ' + json.dumps(args.view) + ';\n' +
                    'var selected = JArray.Parse(' + json.dumps(json.dumps(cases)) + ');\n')
        program = folder / 'input.script'
        program.write_text(IMPORTS + world_helpers(folder, PROJECT / FIXTURE)
                           + (SCRIPTS / 'weapon_flight_scene.script').read_text()
                           + (SCRIPTS / 'verify_artifact_views.script').read_text()
                           + settings + Path(__file__).with_suffix('.script').read_text())
        try:
            result = baseline.call('run', str(program), '570000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise RuntimeError(f'法宝录像记录位于 {folder}') from error
        write_json(folder / 'result.json', result)
        selected = result['completed']
    if not selected:
        parser.error('所选能力没有已完成的录像')
    output = args.output.resolve() if args.output else folder / 'videos'
    encode_clips(folder, output, selected, args.view)
    print(json.dumps(dict(output=str(output), source=str(folder), completed=selected), ensure_ascii=False))


if __name__ == '__main__':
    main()
