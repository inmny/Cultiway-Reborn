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
    'life-return': ('WoodLifeReturn', 'WoodVenomBloom'),
    'cleanse': ('YangCleanse', 'PureYangCleanse'),
    'blasts': ('FireEmberBurst', 'FireBrand', 'CondensedRelease', 'ReservoirOrb'),
    'blast-lost': ('FireEmberBurst',),
    'counters': ('BodyCounter', 'PrimalBodyCounter'),
    'sword-flight': ('SwordChase', 'SwordEmbryoStrike'),
    'yin-flight': ('YinDrain', 'MysteriousYinDrain'),
    'flight-lost': ('SwordChase',),
}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'status', 'support', 'tracking', *SECTIONS), default='all')
    section = parser.parse_args().section
    if section == 'all':
        abilities = tuple(dict.fromkeys(sum(SECTIONS.values(), ())))
    elif section == 'status':
        abilities = sum((SECTIONS[name] for name in ('armor', 'poison', 'frost', 'burn')), ())
    elif section == 'support':
        abilities = SECTIONS['life-return'] + SECTIONS['cleanse']
    elif section == 'tracking':
        abilities = SECTIONS['sword-flight'] + SECTIONS['yin-flight']
    else:
        abilities = SECTIONS[section]
    run(tuple(f'Cultiway.CoreFormation.Skill.{name}' for name in abilities),
        Path(__file__).with_suffix('.script'), [], section)
