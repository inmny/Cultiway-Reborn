#!/usr/bin/env python3
"""用正式世界保存并在另一游戏进程验证修炼组合、状态及冷却。"""
import argparse
from pathlib import Path

from verify_legacy_variants import run

ABILITIES = tuple(f'Cultiway.CoreFormation.Skill.{name}' for name in (
    'ReservoirOrb', 'CondensedRelease', 'SwordEmbryoAura', 'SwordEmbryoStrike', 'FivePhase', 'FivePhaseStrike',
))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('section', choices=('archive', 'cold'))
    parser.add_argument('--archive', type=Path, help='先前 archive 实测输出的目录')
    args = parser.parse_args()
    if (args.section == 'cold') != (args.archive is not None):
        parser.error('仅 cold 需要 --archive 指定先前输出目录')
    run(ABILITIES, Path(__file__).with_suffix('.script'), [], args.section, args.archive)
