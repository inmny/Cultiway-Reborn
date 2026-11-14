#!/usr/bin/env python3
"""复用现有身体与器官部件，录制4.29真实出手、接触、收势及控制归还。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode

CASES = {"horn": "角与实际头部", "tail": "尾扫与四足跃进", "claw": "真实兽足爪击", "together": "同身尾翼独立发力", "parts": "同器官两条真实尾部件"}


def main():
    """每次只操作一个独立游戏，可指定原有动作或只编码已经取得的帧。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--native', action='store_true')
    parser.add_argument('--case', nargs='+', choices=list(CASES))
    parser.add_argument('--encode-only', action='store_true')
    args = parser.parse_args()
    mode = 'native' if args.native else 'worldfall'
    output = PROJECT / 'artifacts/skill_rebuild/nonhuman-body-films' / mode
    output.mkdir(parents=True, exist_ok=True)
    if not args.encode_only:
        install = PROJECT / ('artifacts/skill_rebuild/performance-baseline/cultiway' if args.native else 'artifacts/skill_rebuild/view-switch/environment/cultiway')
        os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
        os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
        import baseline_scenarios as baseline
        connected = baseline.connect(True, validate_runtime=False)
        started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
        if any(path.stat().st_mtime > started for path in (PROJECT / 'Source').rglob('*.cs')):
            raise RuntimeError('代码晚于当前独立游戏，请先退出该进程')
        version = version_stamp()[0]
    rows = []
    manifest = output / ('manifest' + ('-' + '-'.join(args.case) if args.case else '') + '.json')
    write_json(manifest, dict(passed=False, status='running', view=mode))
    for case in args.case or CASES:
        folder = output / case
        folder.mkdir(exist_ok=True)
        if not args.encode_only:
            shutil.rmtree(folder / 'frames', ignore_errors=True)
            for name in ('recording.json', 'result.json', 'checks.json', 'error.json'):
                (folder / name).unlink(missing_ok=True)
            helpers = Path(__file__).with_name('verify_body_organs.script').read_text().split('var editor =', 1)[0]
            helpers = helpers.replace('artifacts/skill_rebuild/body-organs', str(folder.relative_to(PROJECT)))
            helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
            helpers = helpers.replace('    Config.paused = false;\n    double until', '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
            helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,', 'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
            film = Path(__file__).with_name('record_body_connections.script').read_text()
            film = film.replace('source.hasTrait("giant") ? 1f : 2f', 'source.hasTrait("giant") ? 1f : 1.2f')
            film = film.replace('bool filmResponses = false;', 'bool filmResponses = true;\nvar filmCasts = new HashSet<long>();')
            film = film.replace('fact.AbilityId != filmAbility', '!fact.AbilityId.StartsWith("test.body-organs.film.", StringComparison.Ordinal)')
            film = film.replace('    filmCastId = fact.CastId;', '    if (filmCastId == 0) filmCastId = fact.CastId;\n    filmCasts.Add(fact.CastId);')
            film = film.replace('fact.CastId != filmCastId', '!filmCasts.Contains(fact.CastId)')
            film = film.replace('    filmEndedAt = fact.WorldTime;', '    if (!BodyControlState.IsBusy(filmSource.id)) filmEndedAt = fact.WorldTime;')
            film = film.replace('    filmCancelled = fact.Cancelled;', '    filmCancelled |= fact.Cancelled;')
            film = film.replace('    CombatStatusEffects.ApplyStatus(filmSource, filmIdle, 10000, filmSource);', '    if (!BodyControlState.IsBusy(filmSource.id)) CombatStatusEffects.ApplyStatus(filmSource, filmIdle, 10000, filmSource);')
            film = film.replace('    filmSource.updateStats();', '    if (!BodyControlState.IsBusy(filmSource.id)) { ControllableUnit.remove(filmSource); filmSource.finishStatusEffect("possessed"); filmSource.setPossessedMovement(false); }\n    filmSource.updateStats();')
            film = film.replace('AbilityActionEventKind.Completed => "原动作结束",', 'AbilityActionEventKind.Completed => "本部位收回",')
            fragment = Path(__file__).with_suffix('.script').read_text().replace('BODY_FILM_CASE', case)
            script = folder / 'input.script'
            script.write_text(helpers + '\n' + film + '\n' + fragment)
            write_json(folder / 'metadata.json', dict(version=version, connection=connected, case=case))
            try:
                result = baseline.call('run', str(script), '180000')
            except RuntimeError as error:
                (folder / 'error.json').write_text(str(error))
                write_json(folder / 'result.json', dict(passed=False))
                raise RuntimeError('非人身体录像未完成，见 ' + str(folder / 'error.json')) from None
            write_json(folder / 'result.json', result)
        if not json.loads((folder / 'result.json').read_text())['passed']:
            raise RuntimeError('原场景未通过，不能作为交付录像：' + str(folder))
        if json.loads((folder / 'recording.json').read_text())['view'] != mode:
            raise RuntimeError('实际录制视图与请求不符，不能套用该视图的编码与标题：' + str(folder))
        rows.append(encode(case, folder, output, args.native, CASES[case], '4.29 实际身体', 0 if args.native else 1))
    if not args.case:
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' + ''.join(f"file '{case}.mp4'\n" for case in CASES))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-safe', '0', '-f', 'concat', '-i', 'movies.ffconcat', '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(manifest, dict(passed=True, view=mode, cases=rows, timeBasis='实际世界时间；框架功能影片不替代后续流派动作美术'))


if __name__ == '__main__':
    main()
