#!/usr/bin/env python3
"""在原版或 Worldfall 内连续录制 5.15 的三个正式场景。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode

SCRIPTS = Path(__file__).parent
CLIPS = {'shape': '同一形体穿过双门', 'actor': '生灵暂存与抵达', 'artifact': '借用法宝转移与归还'}


def swap(text, old, new):
    if text.count(old) != 1:
        raise RuntimeError(f'录制入口依赖的复验段已变化：{old}')
    return text.replace(old, new, 1)


def add_recording(case):
    name = {'shape': 'verify_spatial_mapping.script', 'actor': 'verify_spatial_mapping_staging.script',
            'artifact': 'verify_spatial_mapping_artifact.script'}[case]
    body = (SCRIPTS / name).read_text()
    # 入口用青色裂隙、出口用琥珀色裂隙；花色不参与双门接触。
    if case == 'shape':
        body = swap(body, '    definition.Binding.Height = .25f;', '''    definition.Binding.Height = .25f;
    definition.Root.Children = definition.Root.Children.Concat(new[] { GateOpening(definition.Root.Children[0], true, definition.Lifetime),
        GateOpening(definition.Root.Children[1], false, definition.Lifetime) }).ToArray();''')
        body = swap(body, '    int gapBefore =', '''    int initialHp = arrival.data.health;
    endingCaption = "自然结束 · 门间无伤，出口扣血";
    await BeginFilm(source, arrival, 70, 73, definition, "cross",
        () => arrival.data.health < initialHp ? "出口真实命中 · 门间目标未损血" : "同一剑形从出口继续飞行",
        () => new { gap = gap.data.health, exit = arrival.data.health });
    Place(source, 64); Place(gap, 70.5f, 73); Place(arrival, 75, 74.1f);
    int gapBefore =''')
        body = swap(body, '    Check(endings.Any', '    await Advance(.36f);\n    Check(endings.Any')
    elif case == 'actor':
        body = swap(body, 'var closing = Gates(.48f);', '''definition.Root.Children = definition.Root.Children.Concat(new[] { GateOpening(definition.Root.Children[0], true, definition.Lifetime),
    GateOpening(definition.Root.Children[1], false, definition.Lifetime) }).ToArray();''')
        at = body.index('    SetExit(true);')
        body = body[:at] + body[body.index('    return new { passed', at):]
        body = swap(body, '    AbilityCast opens =', '''    if (mod != null) ControllableUnit.clear(false);
    Place(source, 64); Place(passenger, waitingAt.x, waitingAt.y);
    entranceCaption = () => Held() ? "同一生灵在入口短时停步" : "真实施放 · 入口待通行";
    endingCaption = "自然结束 · 生灵已恢复行动";
    await BeginFilm(source, passenger, (64 + exitX) * .5f, 72.5f, definition, "carry",
        () => "同一生灵实际落地 · 移动归还", () => new { passengerId, held = Held(), position = passenger.current_position.ToString() });
    AbilityCast opens =''')
        body = swap(body, '    await Advance(1f);', '    await Advance(1.24f);')
    else:
        body = swap(body, '        Place(source, 64);', '''        if (mod != null) ControllableUnit.clear(false);
        Place(source, 64);
        endingCaption = "自然结束 · 同一法宝归还";
        await BeginFilm(source, foe, (64 + exitTile.pos.x) * .5f, (72 + exitTile.pos.y) * .5f + .5f, Gates(ActualPose()), "carry",
            () => "原法宝于出口转向 · 保持借用和装备",
            () => new { borrowed = Borrowed(), equipped = Equipped(), pose = Point(ActualPose().GetColumn(3)) });
        Place(source, 64);''')
        body = swap(body, '        AbilityDefinition definition = Gates(before);', '''        AbilityDefinition definition = Gates(before);
        definition.Root.Children = definition.Root.Children.Concat(definition.Root.Children.Select((gate, index) =>
        {
            var opening = GateOpening(gate, index == 0, definition.Lifetime);
            opening.Operations = gate.Operations.Concat(opening.Operations).ToArray();
            return opening;
        })).ToArray();''')
        body = swap(body, '    if (section == "artifact") await Cast(true);\n    await Cast(false);\n    await Cast(false);\n    Check(receiptIds.Count == 2 && !string.IsNullOrEmpty(receiptIds[0]) && receiptIds[0] == receiptIds[1], "再次施放仍搬运同一稳定编号的原法宝", receiptIds);',
                    '    await Cast(false);\n    await Advance(.30f);')
    body = swap(body, '    return new { passed = true, process =', '    return new { passed = true, view, process =')
    body = swap(body, '    ConstructedAbilityService.ContactConfirmed -= Hit;', '    await EndFilm();\n    ConstructedAbilityService.ContactConfirmed -= Hit;')
    return body


def film_helpers():
    common = (SCRIPTS / 'record_delivery.script').read_text()
    capture = common[common.index('void Capture()'):common.index('/// <summary>暂停只等待')]
    capture = swap(capture, 'scenario == "contact" ? 1.5f : scenario == "orbit" ? 5.2f : scenario == "ground" ? 9f : scenario == "flight" ? 5.5f : 3.5f', '4.6f')
    at, end = capture.index('        stage = '), capture.index('        hand = ')
    capture = capture[:at] + '''        stage = Stage(), age = live?.Age,
        source = new[] { filmSource.current_position.x, filmSource.current_position.y, filmSource.position_height },
        target = new[] { filmTarget.current_position.x, filmTarget.current_position.y, filmTarget.position_height },
        detail = filmDetail?.Invoke(),
''' + capture[end:]
    record = common[common.index('async Task Record()'):common.index('try\n{\n    World.world.console')]
    camera = common[common.index('    World.world.console.Hide()'):common.index('    ConstructedAbilityService.CastStarted += Started;')]
    at, end = camera.index('    if (mod == null) Ready'), camera.index('    MoveCamera.clearFocusUnitOnly()')
    camera = camera[:at] + camera[end:]
    camera = swap(camera, 'scenario == "orbit" ? source : target', 'null')
    camera = swap(camera, 'scenario == "orbit" ? source.current_position : target.current_position', 'centre')
    camera = camera.replace('scenario == "contact" ? 3.5f : scenario == "orbit" ? 8.5f : scenario == "ground" ? 15f : scenario == "flight" ? 11f : 6.5f', '6.6f')
    camera = camera.replace('scenario == "ground" ? .65f : .35f', '.42f').replace('"_godTurn", -.25f', '"_godTurn", .1f')
    return ((SCRIPTS / 'record_spatial_mapping.script').read_text()
            .replace('// RECORD_SHARED', capture + record.replace('1d / 30d', '1d / 15d'))
            .replace('// CAMERA_SHARED', camera))


def run_clip(case, mode, folder, baseline, connection):
    helpers = (SCRIPTS / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "spatial-mapping-films", "{mode}", "{case}")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.spatial-mapping.')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until', '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    (folder / 'frames').mkdir(exist_ok=True)
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\n' + helpers + '\nconst string section = "runtime";\n'
                      + film_helpers() + '\nawait ClearFilmSet();\n' + add_recording(case))
    write_json(folder / 'metadata.json', dict(connection=connection, version=version_stamp()[0], scenario=case))
    try:
        result = baseline.call('run', str(script), '240000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=(*CLIPS, 'all'), default='all')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--encode-only', type=Path, help='从指定视角的已有输出重新编码')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    if not args.encode_only:
        if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
            parser.error('请通过 dev.py exec 录制')
        import baseline_scenarios as baseline
        connection = baseline.connect(False, validate_runtime=False)
        installed = baseline.call('eval', 'return new { worldfall=System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType("FirstPerson.WorldBoxMod",false)!=null), renderer=UnityEngine.SystemInfo.graphicsDeviceName };')
        if installed['worldfall'] != args.worldfall:
            raise RuntimeError('独立安装实际视角与参数不符')
        connection['renderer'] = installed['renderer']
    output = args.encode_only or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'spatial-mapping-films' / mode
    for case in CLIPS if args.case == 'all' else (args.case,):
        folder = output / case
        if not args.encode_only:
            run_clip(case, mode, folder, baseline, connection)
        result = json.loads((folder / 'result.json').read_text())
        if not result.get('passed') or result.get('view') != mode:
            raise RuntimeError(f'未经通过的实机录制：{folder}')
        evidence = encode(case, folder, output, not args.worldfall, CLIPS[case], '5.15', crop_worldfall=False)
        if len(evidence['stages']) < 4:
            raise RuntimeError(f'视频缺少过程阶段：{folder}')


if __name__ == '__main__':
    main()
