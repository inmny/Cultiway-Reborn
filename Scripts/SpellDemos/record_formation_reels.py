#!/usr/bin/env python3
"""从真实修炼主动入口录制七种法相，生成原版与 Worldfall 实录及章节索引。"""
import argparse
import json
from pathlib import Path
import subprocess
import sys

from common import checked_clip, file_sha256, recording_environment, recording_stamp, same_recording, write_json

CASES = (
    ('SwordEmbryoAura', '剑胎显锋', False),
    ('SpiritPlatform', '灵台显神', False),
    ('PrimalBody', '真身镇劫', False),
    ('FivePhase', '五相轮转', False),
    ('DragonAspectBurst', '龙相震击', True),
    ('PureYangDomain', '纯阳净域', True),
    ('MysteriousYinDomain', '玄阴寒域', True),
)
DEMO = Path(__file__).with_name('demo_spell.py')
PREFIX = 'Cultiway.CoreFormation.Skill.'


def run(*command, **kwargs):
    """缺少原片或编码失败时保留实际错误。"""
    return subprocess.run(command, check=True, **kwargs)


def duration(path):
    """读最终媒体时长，用相同的毫秒单位写章节与索引。"""
    result = run('ffprobe', '-v', 'error', '-show_entries', 'format=duration',
                 '-of', 'json', str(path), capture_output=True, text=True)
    return round(float(json.loads(result.stdout)['format']['duration']) * 1000)


def record(output, view):
    """每段独立保存。命令参数及完整录像相同才允许恢复。"""
    for skill, name, area in CASES:
        folder = output / view / skill
        folder.mkdir(parents=True, exist_ok=True)
        print(f'{view} {name}', flush=True)
        with (folder / 'record.log').open('ab') as log:
            run(sys.executable, str(DEMO), PREFIX + skill, '--cases', 'base',
                '--view', view, '--realm', '4', '--fps', '24',
                '--duration', '2.05' if area else '2.8',
                '--zoom', '33' if area else '15', '--target-offset', '5', '0',
                '--output', str(folder), '--resume', stdout=log, stderr=subprocess.STDOUT)


def assemble(output, view):
    """使用真实原片无损拼接，章节使用各段实际时长。"""
    folder = output / view
    chapters = []
    playlist = ['ffconcat version 1.0']
    metadata = [';FFMETADATA1']
    start = 0
    environment = None
    for skill, name, _ in CASES:
        case = folder / skill
        manifest = json.loads((case / 'manifest.json').read_text())
        assert manifest['view'] == view and len(manifest['cases']) == 1
        current = recording_environment(manifest)
        if environment is None:
            environment = current
        elif not same_recording(environment, current):
            raise ValueError(f'法相原片的运行版本或录制工具不同：{skill}')
        original = manifest['cases'][0]
        assert original['id'] == PREFIX + skill
        receipt = checked_clip(case, original['key'], recording_stamp(manifest))
        clip_dir = case / original['key']
        result_path = clip_dir / 'result.json'
        result = json.loads(result_path.read_text())
        assert result['passed'] and result['view'] == view and result['id'] == PREFIX + skill and result['times'], result_path
        clip = clip_dir / 'demo.mp4'
        end = start + duration(clip)
        chapters.append(dict(id=PREFIX + skill, name=name, startMs=start, endMs=end,
                             clip=str(clip), result=str(result_path), files=receipt['files']))
        playlist.append("file '" + str(clip.relative_to(folder)) + "'")
        metadata.extend(['[CHAPTER]', 'TIMEBASE=1/1000', f'START={start}', f'END={end}', f'title={name}'])
        start = end

    (folder / 'clips.ffconcat').write_text('\n'.join(playlist) + '\n')
    (folder / 'chapters.ffmetadata').write_text('\n'.join(metadata) + '\n')
    movie = folder / ('修炼法相-原版.mp4' if view == 'native' else '修炼法相-Worldfall.mp4')
    run('ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', 'clips.ffconcat',
        '-i', 'chapters.ffmetadata', '-map', '0:v:0', '-map_metadata', '1', '-map_chapters', '1',
        '-c', 'copy', '-movflags', '+faststart', str(movie), cwd=folder)
    run('ffmpeg', '-v', 'error', '-xerror', '-i', str(movie), '-f', 'null', '-')
    write_json(folder / 'chapters.json', dict(view=view, movie=str(movie), timeMs=duration(movie),
                                             movieSha256=file_sha256(movie), recordingEnvironment=environment,
                                             chapters=chapters))
    print(movie, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--view', choices=('native', 'worldfall', 'both'), default='both')
    parser.add_argument('--encode-only', action='store_true', help='从已完成原片合成，不启动游戏')
    args = parser.parse_args()
    for selected in ('native', 'worldfall') if args.view == 'both' else (args.view,):
        if not args.encode_only:
            record(args.output.resolve(), selected)
        assemble(args.output.resolve(), selected)
