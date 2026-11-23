#!/usr/bin/env python3
"""把部件及碎解的数值回归分开运行；实战沿用剑冢和既有候选的入口。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('parts', 'fracture', 'parallel'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    stage = args.section
    folder = Path(tempfile.mkdtemp(prefix=f'construction-parts-{stage}-',
                                   dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    name = {'parts': 'verify_construction_part_cost.script',
            'fracture': 'verify_fracture.script', 'parallel': 'verify_fracture_parallel.script'}[stage]
    source = Path(__file__).with_name(name).read_text()
    source = source.replace('Cultiway.ModClass.I.GetDeclaration().FolderPath', json.dumps(str(PROJECT)))
    source = source.replace('Path.Combine(root,"artifacts/skill_rebuild/fracture")', json.dumps(str(folder)))
    source = source.replace('Path.Combine(root,"artifacts/skill_rebuild/fracture/parallel.json")',
                            json.dumps(str(folder / 'comparison.json')))
    if stage == 'fracture':
        source = source.split('var sword=Read(', 1)[0] + '''
var ordinary = Cast(timed);
var controlled = Cast(animated);
ordinary.Advance(.31f, Vector3.zero);
controlled.Advance(.31f, Vector3.zero);
var sourceRows = (Array)typeof(AbilityCast).GetField("fractures", Flags).GetValue(ordinary);
var rigless = (Matrix4x4[][])typeof(AbilityCast).GetField("fractureSkins", Flags).GetValue(ordinary);
var rigged = (Matrix4x4[][])typeof(AbilityCast).GetField("fractureSkins", Flags).GetValue(controlled);
Check(sourceRows.Length == 1 && rigless.Single() == null && rigged.Single() != null,
     "四个分件只保存一份源体前态，有骨架时保存实际矩阵");
return new { passed = true, checks, slots = sourceRows.Length, riglessPieces = ordinary.Plan.FormCount - 1,
    savedRigControls = rigged.Single().Length };
'''
    script = folder / 'input.script'
    script.write_text(source, encoding='utf-8')
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '95000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'部件复验失败，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
