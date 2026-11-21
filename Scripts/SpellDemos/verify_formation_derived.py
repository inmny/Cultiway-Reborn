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
    'armor': ('IronSeverance',),
    'poison': ('WoodVenomBloom',),
    'frost': ('WaterFrostBind',),
    'burn': ('FireBrand',),
}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'status', *SECTIONS), default='all')
    section = parser.parse_args().section
    if section == 'all':
        abilities = sum(SECTIONS.values(), ())
    elif section == 'status':
        abilities = sum((SECTIONS[name] for name in ('armor', 'poison', 'frost', 'burn')), ())
    else:
        abilities = SECTIONS[section]
    run(tuple(f'Cultiway.CoreFormation.Skill.{name}' for name in abilities),
        Path(__file__).with_suffix('.script'), [], section)
