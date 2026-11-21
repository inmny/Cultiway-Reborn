#!/usr/bin/env python3
"""实测三种修炼范围主动的落点、敌我规则和实际支付。"""
import argparse
from pathlib import Path

from verify_legacy_variants import run

ABILITIES = ('DragonAspectBurst', 'PureYangDomain', 'MysteriousYinDomain')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--spell', choices=ABILITIES, action='append')
    args = parser.parse_args()
    run(tuple(f'Cultiway.CoreFormation.Skill.{name}' for name in (args.spell or ABILITIES)),
        Path(__file__).with_suffix('.script'), [], 'area')
