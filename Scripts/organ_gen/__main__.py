"""运行：~/.venvs/cultiway/bin/python -m Scripts.organ_gen --export --preview --check。"""
import argparse
from . import pipeline


def main():
    parser = argparse.ArgumentParser(description="妖兽身体外观制作")
    parser.add_argument("--export", action="store_true", help="生成像素部件及共用目录")
    parser.add_argument("--preview", action="store_true", help="生成组合与动作对照图")
    parser.add_argument("--check", action="store_true", help="检查资源及组合规则")
    args = parser.parse_args()
    if args.export:
        pipeline.export()
    if args.preview:
        pipeline.gallery()
    if args.check:
        pipeline.check()
    if not any(vars(args).values()):
        parser.print_help()


if __name__ == "__main__":
    main()
