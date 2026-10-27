"""实验减面器：输入真实准备网格；输出仍须对固定高面来源验形。"""
import argparse
import importlib.metadata
import json
import sys
import time
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'Tools/Worldfall/meshlab-python'))
import pymeshlab

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--targets', type=int, nargs='+', required=True)
    parser.add_argument('--allow-normal-change', action='store_true')
    parser.add_argument('--already-separated', action='store_true', help='使用 Blender 已核验的分离结果，不重复修连接')
    args = parser.parse_args()
    data = np.load(args.input)
    points = np.asarray(data['vertices'], dtype=np.float64)
    faces = np.asarray(data['faces'], dtype=np.int32)
    quality = np.asarray(data['quality'], dtype=np.float64) if 'quality' in data else np.ones(len(points))
    args.output.mkdir(parents=True, exist_ok=True)
    prepared_path = args.output/'prepared.npz'
    if prepared_path.exists():
        raise RuntimeError(f'拒绝覆盖已有准备结果：{prepared_path}')
    meshset = pymeshlab.MeshSet()
    meshset.add_mesh(pymeshlab.Mesh(points, faces, v_scalar_array=quality))
    if not args.already_separated:
        meshset.meshing_repair_non_manifold_edges(method='Split Vertices')
        meshset.meshing_repair_non_manifold_vertices(vertdispratio=0)
    # 分离过滤器可能保留很多已不被任何面引用的点；删掉这些点不会改变表面。
    meshset.meshing_remove_unreferenced_vertices()
    prepared = meshset.current_mesh()
    pv, pf, pq = prepared.vertex_matrix(), prepared.face_matrix(), prepared.vertex_scalar_array()
    np.savez(prepared_path, vertices=pv, faces=pf, quality=pq)
    record = {'method':'PyMeshLab连接分离与软权重减面',
              'version':importlib.metadata.version('pymeshlab'),
              'input':str(args.input), 'source_vertices':len(points), 'source_triangles':len(faces),
              'prepared_vertices':len(pv), 'prepared_triangles':len(pf),
              'repair':'已在 Blender 分离；本工具只清除未引用点' if args.already_separated else '异常共边与顶点分离（位移0），清除未引用点；不删面、不填孔、不体素化',
              'parameters':{'qualitythr':0.0, 'preserveboundary':False, 'boundaryweight':3.0,
                            'preservenormal':not args.allow_normal_change, 'preservetopology':False,
                            'optimalplacement':True, 'planarquadric':False,
                            'qualityweight':True, 'autoclean':True},
              'outputs':[]}
    for target in args.targets:
        path = args.output/f'reduced-{target}.npz'
        if path.exists():
            raise RuntimeError(f'拒绝覆盖已有减面结果：{path}')
        started = time.perf_counter()
        current = pymeshlab.MeshSet()
        current.add_mesh(pymeshlab.Mesh(pv, pf, v_scalar_array=pq))
        current.meshing_decimation_quadric_edge_collapse(targetfacenum=target, **record['parameters'])
        mesh=current.current_mesh()
        np.savez(path, vertices=mesh.vertex_matrix(), faces=mesh.face_matrix())
        row={'target':target, 'triangles':mesh.face_number(), 'vertices':mesh.vertex_number(),
             'seconds':time.perf_counter()-started, 'path':str(path)}
        record['outputs'].append(row)
        print(json.dumps(row,ensure_ascii=False),flush=True)
    (args.output/'meshlab-record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

if __name__ == '__main__':
    main()
