#!/usr/bin/env python3
"""沿用 10.06 已通过的实际触发入口，按游戏世界时间生成各段原版和 Worldfall 录像。"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


CASES = {
    'shield': ('金钟护界', 'golden_bell_absorption', 'all', [
        ('    Check(Use(owner, handle, defender) && fees.Count == 1', 'await BeginFilm(defender, bell, "原钟开启", 3.55f, 1.62f);'),
        ('    await Strike("hold", .65f, () => Impact() != null);', 'filmStage = "剑气接触与全额吸收";'),
        ('    await Strike("break", 15f, () => BodyHits("break") > 0);', 'filmStage = "实际破盾与余伤";'),
        ('    var now = World.world.getCurWorldTime();', 'EndFilm();'),
    ]),
    'redirection': ('移花卸力', 'artifact_redirection', 'robe', [
        ('        float beforeX = attacker.current_position.x;', 'await BeginFilm(defender, subject, "来伤前", 3.55f, 2.15f, new Vector2(1.4f, 0f)); await Advance(.09f);'),
        ('        int before = defender.data.health, beforeReturn = attacker.data.health;', 'filmStage = "有效来伤与一次回送";'),
        ('        float wait = (float)(Math.Max(State(subject).cooldown_until', 'filmStage = "推离与风面收束"; await Advance(.43f); EndFilm();'),
    ]),
    'alchemy': ('鼎辅炼丹', 'artifact_production', 'alchemy', [
        ('        var ingredients = Herbs();', 'await BeginFilm(producer, ding, "实际开炉", 2.65f, 1.50f); await Advance(.08f);'),
        ('        var lot = warehouse.GetItems().Where', 'filmStage = "成丹交库";'),
        ('        Check(!StepVisible() && !ResultVisible(), "成丹反馈自然收束");', 'EndFilm();'),
    ]),
    'spirit': ('器灵觉醒', 'artifact_spirit_growth', 'growth', [
        ('Check(owner.EquipArtifact(item, locked: true', 'await BeginFilm(source, item, "原剑觉醒与显化", 2.8f, 1.45f); await Advance(.09f);'),
        ('        SetBalance(700f);', 'filmStage = "实际付费施法与成长";'),
        ('    double recoveryUntil = SpiritState().recovery_until;', 'filmStage = "卸装收回";'),
        ('    return new { passed = true, section, process', 'EndFilm();'),
    ]),
    'riding': ('御器载行', 'artifact_riding', 'all', [
        ('    bool started = Toggle();', 'await BeginFilm(source, item, "手动起飞", 3.15f, 1.92f, airborneCamera: true); await Advance(.09f);'),
        ('    Vector3 before = Pose();', 'filmStage = "正式搭载并前行";'),
        ('    Check(Toggle() && !Flying() && !Vehicle()', 'filmStage = "原入口落地与解座";'),
        ('    return new { passed = true, process', 'EndFilm();'),
    ]),
    'sect': ('镇宗宝脉', 'artifact_sect_vein', 'all', [
        ('    sect.AddSpecialItem(item);\n    await Advance(.31f);\n    var during', 'await BeginFilm(source, item, "原印入库供奉", 3.2f, 1.72f, new Vector2(0f, .5f)); await Advance(.09f);'),
        ('    sect.ExtractSpecialItem(item);', 'filmStage = "取回原印并撤去地脉";'),
        ('    sect.AddSpecialItem(item);\n    await Advance(.31f);\n    Check(Installations()', 'filmStage = "重新供奉恢复护持";'),
        ('    return new { passed = true, process', 'EndFilm();'),
    ]),
}


def program_for(case, view, folder):
    _, verifier, section, markers = CASES[case]
    body = (SCRIPTS / f'verify_{verifier}.script').read_text()
    for marker, hook in markers:
        if body.count(marker) != 1:
            raise ValueError(f'{case} 的实录位置已改动 {marker.strip()}')
        body = body.replace(marker, hook + '\n' + marker)
    helpers = world_helpers(folder, PROJECT / FIXTURE).replace('async Task Advance(float seconds)', '''
Func<Task> onFilmFrame = null;
async Task Advance(float seconds)
{
    if (onFilmFrame == null)
    {
        await AdvanceWorld(seconds);
        return;
    }
    double until = World.world.getCurWorldTime() + seconds;
    while (World.world.getCurWorldTime() < until)
    {
        double before = World.world.getCurWorldTime();
        await AdvanceWorld(Mathf.Min(.047f, (float)(until - before)));
        if (World.world.getCurWorldTime() <= before)
            throw new InvalidOperationException("世界时间未推进，停止本段采样");
        await onFilmFrame();
    }
}
async Task AdvanceWorld(float seconds)''')
    flight_scene = (SCRIPTS / 'weapon_flight_scene.script').read_text().replace(
        '    Config.paused = false;\n    await Game.WaitUntil(() => condition()',
        '''    if (onFilmFrame != null)
    {
        while (!condition() && World.world.getCurWorldTime() < deadline)
            await Advance(Mathf.Min(.047f, (float)(deadline - World.world.getCurWorldTime())));
        return;
    }
    Config.paused = false;
    await Game.WaitUntil(() => condition()''')
    settings = f'const string section = {json.dumps(section)}, view = {json.dumps(view)};\nconst bool shots = false;\n'
    return ('using Cultiway; using Cultiway.Const; using Cultiway.Content.Const; using strings;\n'
            'using Cultiway.Core.Combat; using Cultiway.Content.Events; using Cultiway.Content.Behaviours;\n'
            'using Cultiway.Core.SkillLibV3.Utils; using Cultiway.Utils;\n'
            + IMPORTS + helpers + flight_scene + (SCRIPTS / 'verify_artifact_views.script').read_text()
            + settings + Path(__file__).with_suffix('.script').read_text()
            + '\ntry {\n' + body + '''
} finally {
    RestoreFilmCamera();
    ConstructedAbilityService.CastEnded -= Ended;
    Config.paused = true;
    ControllableUnit.clear(false);
    World.world.save_manager.loadWorld(fixture);
    await Game.WaitUntil(() => !SmoothLoader.isLoading(), 1800);
    Config.paused = true;
    await Game.WaitUntil(Boundary, 1800);
    File.WriteAllText(Path.Combine(folder, "restore.json"), JsonConvert.SerializeObject(new { sceneRestored = true }));
}
''')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--view', required=True, choices=('native', 'worldfall'))
    parser.add_argument('--case', action='append', choices=tuple(CASES))
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 录像')

    import baseline_scenarios as baseline

    connection = baseline.connect(False, validate_runtime=False)
    version = version_stamp()[0]
    output = args.output.resolve() / args.view
    output.mkdir(parents=True, exist_ok=True)
    for case in args.case or CASES:
        folder = Path(tempfile.mkdtemp(prefix=f'artifact-passives-film-{case}-{args.view}-',
                                       dir=os.environ['CULTIWAY_DEV_OUTPUT']))
        program = folder / 'input.script'
        program.write_text(program_for(case, args.view, folder))
        write_json(folder / 'metadata.json', dict(view=args.view, case=case, connection=connection, version=version))
        try:
            result = baseline.call('run', str(program), '190000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error),
                        sceneRestored=(folder / 'restore.json').is_file()))
            raise RuntimeError(f'实录未完成，见 {folder}') from error
        result['sceneRestored'] = (folder / 'restore.json').is_file()
        write_json(folder / 'result.json', result)
        if not result['passed'] or not result['sceneRestored']:
            raise RuntimeError(f'实录验证未通过，见 {folder}')
        name = CASES[case][0]
        encode(case, folder, output, args.view == 'native', label=name, milestone='10.06 法宝被动能力',
               crop_worldfall=False)

    clips = [json.loads((output / f'{case}.json').read_text()) for case in CASES if (output / f'{case}.json').is_file()]
    complete = len(clips) == len(CASES)
    if complete:
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' +
            ''.join(f"file '{clip['case']}.mp4'\n" for clip in clips))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', 'movies.ffconcat',
                        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    write_json(output / 'manifest.json', dict(view=args.view, complete=complete, clips=clips,
                                               timeBasis='实际世界时间'))


if __name__ == '__main__':
    main()
