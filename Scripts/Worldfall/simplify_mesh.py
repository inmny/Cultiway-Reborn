"""对真实点/三角面数组做独立减面，后续回 Blender 重投色、适配与实测。"""
import argparse
import json
import sys
import time
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'Tools/Worldfall/simplify-python'))
import fast_simplification

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--targets', type=int, nargs='+', required=True)
    parser.add_argument('--preserve-border', action='store_true')
    args = parser.parse_args()
    data = np.load(args.input)
    vertices, faces = data['vertices'], data['faces']
    args.output.mkdir(parents=True, exist_ok=True)
    record = {'method': 'fast-simplification', 'version': fast_simplification.__version__,
              'input': str(args.input), 'vertices': len(vertices), 'triangles': len(faces),
              'agg': 7.0, 'preserve_border': args.preserve_border, 'outputs': []}
    for target in args.targets:
        path = args.output/f'reduced-{target}.npz'
        if path.exists():
            raise RuntimeError(f'拒绝覆盖已有减面结果：{path}')
        started = time.perf_counter()
        points, triangles = fast_simplification.simplify(
            vertices, faces, target_count=target, agg=7.0,
            preserve_border=args.preserve_border)
        np.savez(path, vertices=points, faces=triangles)
        row = {'target': target, 'triangles': len(triangles), 'vertices': len(points),
               'seconds': time.perf_counter()-started, 'path': str(path)}
        record['outputs'].append(row)
        print(json.dumps(row, ensure_ascii=False), flush=True)
    (args.output/'simplifier-record.json').write_text(
        json.dumps(record, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

if __name__ == '__main__':
    main()
