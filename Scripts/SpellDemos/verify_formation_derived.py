#!/usr/bin/env python3
"""用真实攻防和付费施法触发修炼派生，查看保护、回响和龙威。"""
import argparse
from pathlib import Path

from verify_legacy_variants import run

SECTIONS = {
    'ward': ('EarthWard', 'EarthWardImpact', 'BalancedAdaptation'),
    'casting': ('ReservoirOrb', 'SpiritEcho'),
    'survival': ('IllusionDecoy', 'InfantGuard', 'ChaosRebirth'),
    'dragon': ('DragonMight', 'DragonAspectMight'),
    'strikes': ('ChaosEcho', 'ChaosRebirthEcho', 'FivePhaseStrike'),
}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', *SECTIONS), default='all')
    section = parser.parse_args().section
    abilities = SECTIONS[section] if section != 'all' else sum(SECTIONS.values(), ())
    run(tuple(f'Cultiway.CoreFormation.Skill.{name}' for name in abilities),
        Path(__file__).with_suffix('.script'), [], section)
