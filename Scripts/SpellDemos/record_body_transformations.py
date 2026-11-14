#!/usr/bin/env python3
"""复用实际身体、原法宝和连续取样，录制4.31的延伸及承载投送。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode

CASES = {'extension': '真实末端延伸与结果收势', 'carrier': '原法宝承载、离手与独立命中'}


def main():
    """每种显示各录两个既有案例，指定或重新编码时不扩大场景。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--native', action='store_true')
    parser.add_argument('--case', nargs='+', choices=list(CASES))
    parser.add_argument('--encode-only', action='store_true')
    args = parser.parse_args()
    mode = 'native' if args.native else 'worldfall'
    output = PROJECT / 'artifacts/skill_rebuild/body-transform-films' / mode
    output.mkdir(parents=True, exist_ok=True)
    if not args.encode_only:
        install = PROJECT / ('artifacts/skill_rebuild/performance-baseline/cultiway' if args.native else
                             'artifacts/skill_rebuild/view-switch/environment/cultiway')
        os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
        os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
        import baseline_scenarios as baseline
        connected = baseline.connect(True, validate_runtime=False)
        started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
        if any(path.stat().st_mtime > started for path in (PROJECT / 'Source').rglob('*.cs')):
            raise RuntimeError('本轮代码晚于独立游戏，请先退出该进程')
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
            helpers = 'using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\nusing Cultiway.Core.SkillLibV3.Components;\n' + helpers
            helpers = helpers.replace('artifacts/skill_rebuild/body-organs', str(folder.relative_to(PROJECT)))
            helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
            helpers = helpers.replace('    Config.paused = false;\n    double until', '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
            helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,', 'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
            film = Path(__file__).with_name('record_body_connections.script').read_text()
            film = film.replace('bool filmResponses = false;', 'bool filmResponses = true;\ndouble transformStarted = 0d, transformHitAt = -1d;')
            begin = film.index('        if (filmResponses)\n        {\n            var spatial', film.index('void CaptureRelationFrame()'))
            end = film.index('        var muted', begin)
            film = film[:begin] + film[end:]
            film = film.replace('    RenderTexture previous = RenderTexture.active;', '    QuietReturnedBody();\n    RenderTexture previous = RenderTexture.active;', 1)
            film = film.replace('    filmCastId = fact.CastId;', '    filmCastId = fact.CastId; transformStarted = fact.WorldTime;')
            film = film.replace(' || fact.Fact.Node >= 0', '')
            film = film.replace('fact.WorldTime, fact.Fact.Age,', 'fact.WorldTime, fact.Fact.Age, fact.Fact.Node,')
            film = film.replace('    filmFacts.Add(new { kind = "contact",', '    if (fact.CastId == filmCastId) transformHitAt = transformStarted + fact.Age;\n    filmFacts.Add(new { kind = "contact",')
            film = film.replace('time = World.world.getCurWorldTime(), fact.Age,', 'time = transformStarted + fact.Age, fact.Node, fact.Age,')
            film = film.replace('        stage = filmResponses ?', '        stage = TransformStage(),\n        bodyStage = filmResponses ?')
            film = film.replace('item = itemState, body = bodyState', 'item = itemState, body = bodyState, artifact = ArtifactState()')
            film = film.replace('    filmSource.updateStats();\n    SaveRelationFilm();', '    filmSource.updateStats();\n    ControllableUnit.remove(filmSource); filmSource.finishStatusEffect("possessed"); filmSource.setPossessedMovement(false);\n    SaveRelationFilm();')
            if case == 'carrier':
                film = film.replace('source.hasTrait("giant") ? 1f : 2f', '4f')
            else:
                film = film.replace('source.hasTrait("giant") ? 1f : 2f', '1.2f')
                film = film.replace('        if (filmCarrying) {', '        filmZoom = Mathf.Max(filmZoom, 3f);\n        if (filmCarrying) {', 1)
            fragment = Path(__file__).with_suffix('.script').read_text().replace('TRANSFORM_FILM_CASE', case)
            script = folder / 'input.script'
            script.write_text(helpers + '\n' + film + '\n' + fragment)
            write_json(folder / 'metadata.json', dict(version=version, connection=connected, case=case))
            try:
                result = baseline.call('run', str(script), '180000')
            except RuntimeError as error:
                (folder / 'error.json').write_text(str(error))
                write_json(folder / 'result.json', dict(passed=False))
                raise RuntimeError('跨形态录像未完成，见 ' + str(folder / 'error.json')) from None
            write_json(folder / 'result.json', result)
        if not json.loads((folder / 'result.json').read_text())['passed']:
            raise RuntimeError('场景未通过，不能作为录像交付：' + str(folder))
        if json.loads((folder / 'recording.json').read_text())['view'] != mode:
            raise RuntimeError('实际录制视图与请求不符')
        rows.append(encode(case, folder, output, args.native, CASES[case], '4.31 跨形态作用',
                           0 if args.native else 1, crop_worldfall=False))
    if not args.case:
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' + ''.join(f"file '{case}.mp4'\n" for case in CASES))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-safe', '0', '-f', 'concat', '-i', 'movies.ffconcat',
                        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(manifest, dict(passed=True, view=mode, cases=rows, timeBasis='实际世界时间；功能影片不替代最终流派美术'))


if __name__ == '__main__':
    main()
