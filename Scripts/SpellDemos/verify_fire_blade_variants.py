#!/usr/bin/env python3
"""沿原已学入口实测火刃的四份旧索引。"""
import argparse
from pathlib import Path

from verify_legacy_variants import run


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--variant', type=int, choices=range(4), action='append', help='只复验指定旧索引，可重复指定')
    args = parser.parse_args()
    run(('Cultiway.FireBlade',), Path(__file__).with_suffix('.script'), args.variant or range(4))


if __name__ == '__main__':
    main()
