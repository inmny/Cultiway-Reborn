#!/usr/bin/env python3
"""5.05 C14：真实旧轨迹构造转换和局部失败恢复。"""
import argparse
import json
import os
import shutil
import hashlib
import zlib
import copy
from pathlib import Path

from common import PROJECT, version_stamp, write_json



def prepare_world(base):
    """只在现成世界注入真实旧区段与一份独立坏定义。"""
    records = json.loads((Path(__file__).parent / 'fixtures/legacy-trajectory-save.json').read_text())
    for record in records.values():
        source = PROJECT / record['source']['map']
        if source.exists() and hashlib.sha256(source.read_bytes()).hexdigest() != record['source']['sha256']:
            raise RuntimeError('真实历史存档的来源校验值发生变化：' + str(source))
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1/map.wbox'
    world = json.loads(zlib.decompress(fixture.read_bytes()))
    custom = world['mapStats']['custom_data']['custom_data_string']
    envelope = json.loads(custom['cultiway.world'])
    section = copy.deepcopy(records['curve']['section'])
    bad = copy.deepcopy(section['data']['Definitions'][0])
    bad['Id'] = 'test.legacy-trajectory.missing'
    bad['Root']['Form'] = 'missing.legacy-trajectory.form'
    section['data']['Definitions'].append(bad)
    envelope['sections']['ability_constructions'] = section
    custom['cultiway.world'] = json.dumps(envelope, ensure_ascii=False, separators=(',', ':'))
    folder = base / 'input-world'
    folder.mkdir(parents=True, exist_ok=True)
    payload = zlib.compress(json.dumps(world, ensure_ascii=False, separators=(',', ':')).encode())
    (folder / 'map.wbox').write_bytes(payload)
    write_json(base / 'input-world-expected.json', dict(sha256=hashlib.sha256(payload).hexdigest(), base=str(fixture.relative_to(PROJECT))))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'save', 'runtime'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再验证')
    base = PROJECT / 'artifacts/skill_rebuild/trajectories/legacy-trajectory-save'
    prepare_world(base)
    for part in (('save',) if section == 'all' else (section,)):
        folder = base / part
        folder.mkdir(parents=True, exist_ok=True)
        if (folder / 'result.json').exists() and not json.loads((folder / 'result.json').read_text()).get('passed'):
            failures = base / 'failures'
            failures.mkdir(exist_ok=True)
            index = 1
            while (failures / f'{part}-{index}').exists():
                index += 1
            shutil.copytree(folder, failures / f'{part}-{index}')
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
        helpers = helpers.replace('test.body-footwork.', 'test.legacy-trajectory-save.')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.legacy-trajectory-save."))', '')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.legacy-trajectory-save.")) contacts.Add(contact);', 'contacts.Add(contact);')
        body = Path(__file__).with_name('verify_legacy_trajectory_save.script').read_text()
        if part == 'runtime':
            fire = helpers.split('async Task Fire(', 1)[1].split('void Ready(', 1)[0]
            helpers = helpers.split('Game.Resume();', 1)[0] + 'async Task Fire(' + fire
            setup = 'var curve=access.Read(curveId); var steps=access.Read(stepsId); string inputHash=(string)expectedInput["sha256"];\n'
            tail = body.split('// 正式施放及完成存读', 1)[1]
            tail = tail.replace('Actor source=Make(64),enemy=Make(72,"wolf"); var owner=source.GetExtend(); owner.NewCultisys(Cultisyses.Xian); owner.GetCultisys<Xian>().level=2;', 'Actor source=World.world.units.getSimpleList().Single(a=>ConstructedAbilityService.Knows(a.GetExtend(),curveId)),enemy=World.world.units.getSimpleList().Single(a=>a.asset.id=="wolf" && a.current_position.x>60); var owner=source.GetExtend();')
            ready = 'var hold=Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold");\nvoid Place(Actor a,float x,float y=72) { a.stopMovement(); a.stopForce(); a.batch.c_action_landed.Remove(a); a.current_position=new Vector2(x,y); a.setCurrentTile(World.world.GetTileSimple((int)x,(int)y)); a.updatePos(); }\nvoid Ready(Actor a) { CombatStatusEffects.RemoveStatus(a,hold); a.updateStats(); ControllableUnit.clear(false); ControllableUnit.setControllableCreature(a); a.attack_timer=0; }\n'
            body = body.split('try\n{', 1)[0] + ready + 'try\n{\n' + setup + tail
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Persistence;\nusing UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n' + helpers + '\n' + body)
        write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
        write_json(folder / 'result.json', dict(passed=False, status='running'))
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('旧构造资料转换验证未通过：' + str(folder / 'checks.json'))
        print(json.dumps(dict(section=part, result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
