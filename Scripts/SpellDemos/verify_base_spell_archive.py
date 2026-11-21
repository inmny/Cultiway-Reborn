#!/usr/bin/env python3
"""按制作清单在一个正式世界里保存并重新读回全部旧法术索引。"""
import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path
from tempfile import TemporaryDirectory

from common import PROJECT
from verify_legacy_variants import run


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('archive', 'cold'), required=True)
    parser.add_argument('--archive', type=Path)
    args = parser.parse_args()
    if args.case == 'cold' and (args.archive is None or not
            all((args.archive / name).is_file() for name in ('expected.json', 'archive/map.wbox'))):
        parser.error('需要含正式世界及预期资料的输出目录')

    variants = defaultdict(list)
    catalog_path = PROJECT / 'Doc/技能系统全面重构/制作与验证条目.csv'
    with catalog_path.open(encoding='utf-8-sig', newline='') as catalog:
        for row in csv.DictReader(catalog):
            if row['类别'] == '旧法术变体':
                identifier = row['原登记编号'].removeprefix('skill:')
                variants[identifier].append(int(row['旧变体索引']))
    if not variants or any(sorted(indices) != list(range(len(indices))) for indices in variants.values()):
        raise ValueError('旧索引清单必须从零连续列出，每种法术的索引各出现一次')

    folder = Path(__file__).parent
    common = (folder / 'verify_projectile_variants.script').read_text().split('var casts = ', 1)[0]
    start, end = common.index('var catalog ='), common.index('bool CanTestTracking')
    spells = ',\n'.join(f'    (Id: {json.dumps(name)}, Count: {len(indices)})' for name, indices in variants.items())
    entries = 'var catalog = new[]\n{\n' + spells + '\n}.Select(row =>\n{\n'
    entries += '''    var asset = Cultiway.ModClass.I.SkillV3.SkillLib.get(row.Id);
    Check(asset != null && asset.Animations.Count == row.Count, "旧索引与实际法术登记一致", row);
    return (Asset: asset, Count: row.Count);
}).ToArray();
'''
    body = common[:start] + entries + common[end:]
    body += '''
var assets = Learned().OrderBy(item => item.GetComponent<SkillContainer>().Asset.id, StringComparer.Ordinal)
    .ThenBy(item => item.GetComponent<SkillContainer>().AnimationIndex)
    .Select(item => new { id = item.GetComponent<SkillContainer>().Asset.id,
        index = item.GetComponent<SkillContainer>().AnimationIndex }).ToArray();
Check(assets.Length == catalog.Sum(entry => entry.Count) &&
      assets.Select(row => (row.id, row.index)).Distinct().Count() == assets.Length,
    "重新启动后逐一核对完整索引");
ConstructedAbilityService.CastEnded -= Ended;
ConstructedAbilityService.ContactConfirmed -= Hit;
return new { passed = true, process, skills = assets.Length, assets };
'''
    with TemporaryDirectory(prefix='full-base-spell-archive-') as tmp:
        script = Path(tmp) / 'verify_base_spell_archive.script'
        script.write_text(body)
        run(tuple(variants), script, [], args.case, args.archive)


if __name__ == '__main__':
    main()
