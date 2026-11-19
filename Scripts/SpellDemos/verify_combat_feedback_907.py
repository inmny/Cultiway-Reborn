#!/usr/bin/env python3
"""复验 9.07 的实际受击声音、破防短停，以及切换视角不重播。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('weapon', 'clash', 'landing'), required=True)
    parser.add_argument('--material', choices=('wood', 'iron'), help='只补某一种御器材质')
    parser.add_argument('--part', choices=('ground', 'break'), help='只补实际落地或破防')
    args = parser.parse_args()
    section = args.section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请使用 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline

    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'combat-feedback-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    read = lambda name: (Path(__file__).parent / name).read_text()
    if section != 'clash':
        helper = read('verify_source_sessions.script').split('var mutations =', 1)[0]
        helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/source-sessions")', json.dumps(str(folder)))
    else:
        helper = read('verify_clashes.script').split('void Record(', 1)[0]
        helper = helper.replace('void Check(bool value,string name)', 'void Check(bool value,string name,object data=null)')
        helper = helper.replace('Lifetime=.12f,Power=', 'Lifetime=.3f,TargetRange=20,Power=')
        helper = helper.replace('World.world.getCurWorldTime()>=until,1800',
                                'World.world.getCurWorldTime()>=until && (double)typeof(ConstructedAbilityService)'
                                '.GetProperty("ConfirmedWorldTime", F).GetValue(null)>=until,1800')
        helper = helper.replace('string root=Cultiway.ModClass.I.GetDeclaration().FolderPath;',
                                'const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;\n'
                                'string root=Cultiway.ModClass.I.GetDeclaration().FolderPath;')
        helper = helper.replace('var checks=new List<string>();var records=new List<object>();',
                                f'string folder = {json.dumps(str(folder))};\nvar checks = new List<object>();')
        helper = helper.replace('if(!value)throw new InvalidOperationException(name);checks.Add(name);',
                                'checks.Add(new { pass = value, name, data }); '
                                'File.WriteAllText(Path.Combine(folder, "checks.json"), JsonConvert.SerializeObject(checks)); '
                                'if (!value) throw new InvalidOperationException(name);')
    helper = helper.replace('Path.Combine(root,"Scripts/WorldSaveTests/Fixtures/world-format-1")',
                            'Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")')
    helper = helper.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                            json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helper = ('using Cultiway.Core.SkillLibV3.Utils;\nusing System.Collections;\n' if section != 'clash' else '') + helper
    selection = (f'\nstring materialOnly = {json.dumps(args.material)};\n' if section == 'weapon'
                 else f'\nstring part = {json.dumps(args.part)};\n')
    script = folder / 'input.script'
    script.write_text('using HarmonyLib;\n' + helper + read('verify_appearance_camera.script') +
                      read('verify_visual_cleanup_camera.script') + selection + read(f'verify_combat_feedback_{section}.script') +
                      read('combat_feedback_playback_probe.script'))
    write_json(folder / 'connection.json', connection)
    print(folder, flush=True)
    try:
        result = baseline.call('run', str(script), '190000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise RuntimeError(f'检查未通过，现场位于 {folder}')


if __name__ == '__main__':
    main()
