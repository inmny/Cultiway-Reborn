#!/usr/bin/env python3
"""7.21：同一套正式战技的阅读、城市操练、恢复和自主交战。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', choices=('practice', 'restore'))
    parser.add_argument('--state', type=Path)
    parser.add_argument('--source', type=Path, default=Path.home() / '.local/state/cultiway-dev/evidence/martial-study/prepare-cast')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 dev.py exec 运行')
    prior = args.source if args.stage == 'practice' else args.state
    if not prior or not json.loads((prior / 'result.json').read_text()).get('passed'):
        parser.error('需要通过的 7.20 实战样本或 7.21 practice 输出目录')
    relative = 'martial-practice/' + args.stage
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    source_map = 'pending-save' if args.stage == 'practice' else 'save'
    source_record = 'pending-expected.json' if args.stage == 'practice' else 'expected.json'
    shutil.copytree(prior / source_map, folder / 'world', dirs_exist_ok=True)
    shutil.copyfile(prior / source_record, folder / 'input.json')
    scripts = Path(__file__).parent
    template = (scripts / 'verify_body_footwork.script').read_text()
    helpers = template.split('Game.Resume();', 1)[0]
    # 两段都用相同的正常世界推进和落地准备；不重新生成训练所学的战技。
    scene = template[template.index('Game.Resume();'):template.index('async Task Fire(')]
    scene = scene.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "world")')
    scene = scene.replace("    Place(a, 12, 12);\n", "")
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))', '')
    script = folder / 'input.script'
    script.write_text('using Cultiway.Content.KnightCombat;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\n' + helpers + scene +
                      (scripts / f'verify_martial_practice_{args.stage}.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '165000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'战技传承及操练尚未通过：{folder}')
    print(folder)


if __name__ == '__main__':
    main()
