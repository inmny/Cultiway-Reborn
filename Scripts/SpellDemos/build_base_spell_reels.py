#!/usr/bin/env python3
"""按旧索引合成已验证的双视角实机影片，并留下逐项取证索引。"""
import argparse
import csv
import json
import subprocess
from concurrent.futures import ThreadPoolExecutor
from itertools import groupby
from pathlib import Path

from common import PROJECT, file_sha256, write_json

VIEWS = {'native': '原版', 'worldfall': 'Worldfall'}


def references(data):
    if isinstance(data, dict):
        for value in data.values():
            yield from references(value)
    elif isinstance(data, list):
        for value in data:
            yield from references(value)
    elif isinstance(data, str) and data.startswith('/'):
        path = Path(data)
        if path.name in ('demo.mp4', 'result.json'):
            yield path.with_name('result.json')


def delivered_clips(evidence):
    clips = {}
    for deliverable in sorted(evidence.glob('*/delivery.json')):
        data = json.loads(deliverable.read_text())
        media = [data.get(key) for key in ('footage', 'recording', 'recordings', 'movies')]
        for result in set(references(media)):
            clip = result.with_name('demo.mp4')
            if not result.is_file() or not clip.is_file():
                raise ValueError(f'缺少已登记的实录或报告 {result}')
            report = json.loads(result.read_text())
            item = report.get('item') or {}
            view = report.get('view')
            if not report.get('passed') or view not in VIEWS or item.get('mode') != 'base':
                continue
            key = (view, report['id'], item['variant'])
            if key not in clips or result.stat().st_mtime > clips[key][2].stat().st_mtime:
                clips[key] = (clip, deliverable, result)
    return clips


def probe(clip):
    parameters = ['ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries',
                  'stream=codec_name,width,height,pix_fmt,time_base:format=duration', '-of', 'json']
    info = json.loads(subprocess.check_output(parameters + [str(clip)]))
    return info['streams'][0], float(info['format']['duration'])


def publish(view, rows, clips, output):
    sources = [clips[view, asset, index] for asset, index, _ in rows]
    with ThreadPoolExecutor(max_workers=4) as pool:
        tracks = list(pool.map(probe, [source[0] for source in sources]))
    if any(track != tracks[0][0] for track, _ in tracks):
        raise ValueError(f'{VIEWS[view]}的画面尺寸或时间单位不同，请核对原片')
    playlist = ['ffconcat version 1.0']
    segments = []
    elapsed = 0.0
    for (asset, index, title), (clip, delivery, evidence), (_, duration) in zip(rows, sources, tracks):
        escaped = str(clip).replace("'", "'\\''")
        playlist.extend((f"file '{escaped}'", f'duration {duration:.6f}'))
        segments.append(dict(asset=asset, variant=index, title=title, start=round(elapsed, 3),
                             seconds=duration, video=str(clip), report=str(evidence), delivery=str(delivery),
                             fileSha256=file_sha256(clip)))
        elapsed += duration
    chapters = [';FFMETADATA1']
    for _, group in groupby(segments, key=lambda item: item['asset']):
        shots = list(group)
        last = shots[-1]
        title = shots[0]['title'].split(' / ', 1)[0]
        chapters.extend(('[CHAPTER]', 'TIMEBASE=1/1000', f'START={round(shots[0]["start"] * 1000)}',
                         f'END={round((last["start"] + last["seconds"]) * 1000)}', f'title={title}'))
    inputs = output / f'{view}.ffconcat'
    marks = output / f'{view}.ffmeta'
    inputs.write_text('\n'.join(playlist) + '\n')
    marks.write_text('\n'.join(chapters) + '\n')
    path = output / f'10.01-基础法术与旧变体-{VIEWS[view]}.mp4'
    temporary = path.with_suffix('.tmp.mp4')
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', str(inputs),
                    '-f', 'ffmetadata', '-i', str(marks), '-map', '0:v:0', '-map_chapters', '1',
                    '-c', 'copy', '-movflags', '+faststart', str(temporary)], check=True)
    _, actual = probe(temporary)
    if abs(actual - elapsed) > .25:
        raise ValueError(f'{VIEWS[view]}合辑时长不符，预期 {elapsed:.3f} 秒，实际 {actual:.3f} 秒')
    temporary.replace(path)
    return dict(video=str(path), seconds=actual, clips=segments)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    catalog = PROJECT / 'Doc/技能系统全面重构/制作与验证条目.csv'
    with catalog.open(encoding='utf-8-sig', newline='') as file:
        rows = [(row['原登记编号'].removeprefix('skill:'), int(row['旧变体索引']), row['名称'])
                for row in csv.DictReader(file) if row['类别'] == '旧法术变体']
    clips = delivered_clips(args.evidence)
    missing = [(view, asset, index) for view in VIEWS for asset, index, _ in rows
               if (view, asset, index) not in clips]
    if missing:
        raise ValueError(f'以下索引缺少双视角验证录像 {missing}')
    args.output.mkdir(parents=True, exist_ok=True)
    results = {view: publish(view, rows, clips, args.output.resolve()) for view in VIEWS}
    write_json(args.output / '双视角索引.json', dict(historical=True, spells=len({asset for asset, _, _ in rows}),
               variants=len(rows), views=results))


if __name__ == '__main__':
    main()
