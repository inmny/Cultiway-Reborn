#!/usr/bin/env python3
"""通过修炼主动入口检查四种自身法相的出手、状态与恢复。"""
import argparse
from pathlib import Path

from verify_legacy_variants import run

ABILITIES = ('SwordEmbryoAura', 'SpiritPlatform', 'PrimalBody', 'FivePhase')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('self', 'carrier'), default='self')
    run(tuple(f'Cultiway.CoreFormation.Skill.{name}' for name in ABILITIES),
        Path(__file__).with_suffix('.script'), [], parser.parse_args().case)
